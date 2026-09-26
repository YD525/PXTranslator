using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NIM.FileManagement;
using NIM.ApplicationLayer;
using NIM.SkyrimManagement;
using NIM.TranslateManage;
using NIM.UIManage;
using NIM.UIManagement;
using NIM.YDControls;
using PexInterface;
using NIMEngine;
using NIMEngine.Common;
using NIMEngine.Events;
using NIMEngine.Language;
using NIMEngine.Platform.LocalAI;
using NIMEngine.Platform;
using NIMEngine.Translate;
using System.Windows.Threading;
using NIM.UIManagement.Preview;
using NIM.ModParser;

namespace NIM
{
    /// <summary>
    /// Interaction logic for LexGui.xaml
    /// </summary>
    public partial class NIMGui : Window
    {
        private readonly TranslationPresetCoordinator _translationPresetCoordinator;
        private bool _isUpdatingTranslationPresetControls;
        private readonly PreviewDiagnosticService _diagnostics;
        internal NIMGui(PreviewDiagnosticService diagnostics)
        {
            _translationPresetCoordinator = new TranslationPresetCoordinator(
                new TranslationPresetService(),
                new LegacyTranslationPresetStore());
            InitializeComponent();

            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        private PageSwitcher InfoPage = null;
        private PageSwitcher MainPage = null;
        public TranslateConfig TranslateConfigView = null;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                NIMApp.Init(this);

                TranslatorInterface.Init();

                InfoPage = new PageSwitcher(this, InFoPages);
                MainPage = new PageSwitcher(this, Views);

                ShowView("TransHub");

                UIHelper.SyncNodes(Nodes);

                //If you like anime, you can place a CG.png in the program's installation directory, making sure the dimensions are correct. It will display an anime character at the top of the software.
                string CheckCGPath = NIMApp.GetFullPath(@"\CG.png");
                if (File.Exists(CheckCGPath))
                {
                    NIMApp.CG = new CGView();
                    NIMApp.CG.Hide();
                    NIMApp.CG.CG.Source = new BitmapImage(new Uri(CheckCGPath));

                    NIMApp.CG.Owner = this;
                    NIMApp.CG.Show();
                    SyncCGLocation();
                }

                YDChart.SetAction(
                  new Action<RealtimeLineChart>((Ref) =>
                  {
                      Ref.PushValue(NIMApp.ChartDataRef.GetCurrent());
                  }),
                  new Action<RealtimeLineChart>((Ref) =>
                  {
                      Ref.PushValue(NIMApp.ChartDataRef.Total);
                  }),
                  NIMApp.ChartDataRef
                 );

                EngineEvents.SetBookTranslateCallback += BookTransCallBack;

                SelectFristSettingNav();
                InfoPage.SwitchPageByHorizontal(0);

                if (TranslateConfigView == null)
                {
                    TranslateConfigView = new TranslateConfig(this);
                    TranslateConfigView.Hide();
                }

                LastSetLogButton = InputLogButton;
            }
            catch (Exception Ex)
            {
                WpfPreviewDialogService DialogService = new WpfPreviewDialogService(() => this);
                DialogService.Show(new PreviewDialogRequest("Error",Ex.Message,PreviewDialogSeverity.Error,false));
            }
        }

        private void ShowTranslateConfigView(object sender, MouseButtonEventArgs e)
        {
            TranslateConfigView.Owner = this;
            TranslateConfigView.Init();
            TranslateConfigView.Show();

            if (ActiveTab != null)
            {
                if (ActiveTab.TransListView.RealLines.Count > 0)
                {
                    TranslateConfigView.SFrom.SelectedValue = ActiveTab.Mod.P_Translator.From.ToString();
                }
                else
                {
                    TranslateConfigView.SFrom.SelectedValue = Languages.English.ToString();
                }
            }
            else
            {
                TranslateConfigView.SFrom.SelectedValue = Languages.English.ToString();
            }

            TranslateConfigView.STo.SelectedValue = NIMApp.SelfSetting.TargetLanguage.ToString();
        }

        public void BookTransCallBack(string Key, string CurrentText)
        {
            //if (Key.Equals(LastSetKey) && CurrentText.Length > 0)
            //{
            //    ToStr.Dispatcher.Invoke(new Action(() =>
            //    {
            //        ToStr.Text = CurrentText;
            //    }));
            //}
        }


        public void SyncCGLocation()
        {
            if (NIMApp.CG != null)
            {
                NIMApp.CG.Top = (this.Top - NIMApp.CG.ActualHeight) + 1;
                NIMApp.CG.Left = this.Left + 100;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (CurrentNav == "TransHub")
            {
                if (ActiveTab != null)
                {
                    ActiveTab.Window_PreviewKeyDown(sender, e);
                }
            }
        }


        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActiveTab != null)
            {
                this.ActiveTab.SyncListView();
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {

        }

        private void Window_Activated(object sender, EventArgs e)
        {

        }


        private void OpenUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string GetUrl = "";
            string GetTag = "";

            if (sender is Label)
            {
                GetUrl = "";
                GetTag = P_Convert.ObjToStr(((Label)sender).Tag);

                if (GetTag.Length > 0)
                {
                    GetUrl = GetTag;
                }
                else
                {
                    GetUrl = P_Convert.ObjToStr(((Label)sender).Content);
                }
            }
            if (sender is Run)
            {
                GetUrl = "";
                GetTag = P_Convert.ObjToStr(((Run)sender).Tag);

                if (GetTag.Length > 0)
                {
                    GetUrl = GetTag;
                }
                else
                {
                    GetUrl = P_Convert.ObjToStr(((Run)sender).Text);
                }
            }

            if (GetUrl.Length > 0)
            {
                if (MessageBoxExtend.Show(this, "Prompt", "Do you want to open your default browser and visit\n " + GetUrl + "\n?",PreviewDialogSeverity.Information,true))
                {
                    ExplorerHelper.OpenUrl(GetUrl);
                }
            }
        }

        #region Effect

        public Storyboard XTGlowLoopStoryboard = null;
        private void StartLexGlowLoop()
        {
            XTGlowLoopStoryboard = (Storyboard)FindResource("XTGlowLoop");
            XTGlowLoopStoryboard.Begin();
        }
        private void StopLexGlowLoop()
        {
            XTGlowLoopStoryboard?.Stop();
        }

        #endregion

        #region WinControl

        public void UI(Action Action)
        {
            if (Dispatcher.CheckAccess())
                Action();
            else
                Dispatcher.BeginInvoke(Action);
        }

        public bool IsLeftMouseDown = false;

        private void WinHead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IsLeftMouseDown = true;
            }

            if (IsLeftMouseDown)
            {
                try
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.DragMove();
                    }));

                    IsLeftMouseDown = false;
                }
                catch { }
            }
        }

        private void Min_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        public int SizeChangeState = 0;
        private double OriginalLeft;
        private double OriginalTop;
        private double OriginalWidth;
        private double OriginalHeight;
        private void AutoMax_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var Screen = SystemParameters.WorkArea;

            if (SizeChangeState == 0)
            {
                OriginalLeft = this.Left;
                OriginalTop = this.Top;
                OriginalWidth = this.Width;
                OriginalHeight = this.Height;

                double TargetWidth = Screen.Width - 100;
                double TargetHeight = Screen.Height - 100;

                this.Width = TargetWidth;
                this.Height = TargetHeight;

                this.Left = Screen.Left + (Screen.Width - TargetWidth) / 2;
                this.Top = Screen.Top + (Screen.Height - TargetHeight) / 2;

                SizeChangeState = 1;
            }
            else
            {
                this.Left = OriginalLeft;
                this.Top = OriginalTop;
                this.Width = OriginalWidth;
                this.Height = OriginalHeight;

                SizeChangeState = 0;
            }
        }

        public bool CanExit = true;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CanExit)
            {
                NIMApp.CloseAny();
            }
        }

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            NIMApp.CloseAny();
        }

        #endregion

        #region ViewSwitch
        private void ChangeInFoPage(object sender, MouseButtonEventArgs e)
        {
            int Index = P_Convert.ObjToInt((sender as Ellipse).Tag);

            if (sender is Ellipse)
            {
                InfoPage.SwitchPageByHorizontal(Index);

                foreach (var Child in ((sender as Ellipse).Parent as StackPanel).Children)
                {
                    if (Child is Ellipse OtherEllipse)
                    {
                        OtherEllipse.Style = (Style)FindResource("PageBtn");
                    }
                }

                (sender as Ellipse).Style = (Style)FindResource("PageBtnSelected");
            }
        }

        private void ShowView(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border ClickedMenu)) return;

            ShowView(ClickedMenu.Tag?.ToString());
        }

        public string CurrentNav = "";
        public void ShowView(string Name)
        {
            UI(() =>
            {
                int PageIndex = 0;
                CurrentNav = Name;


                switch (Name)
                {
                    case "InFo":
                        {
                            if (IsNodeExpanded)
                            {
                                SyncAnimation();
                                ShowNodeMenu(false);
                                LogView.Visibility = Visibility.Collapsed;
                            }

                            if (TranslateView.CurrentHistory != null)
                            {
                                TranslateView.CurrentHistory.Visibility = Visibility.Hidden;
                            }

                            YDChart.Stop();
                            PageIndex = 0;
                            StartLexGlowLoop();
                            NIMVer.Content = NIMApp.CurrentVersion;
                            EngineVer.Content = Phoenix.Version;
                            PEXAnalysisVer.Content = PexHeuristicAnalysis.Version;
                            PEXReaderVer.Content = PexInterop.Version;
                            ESPReaderVer.Content = EspReader.Version;
                            DSDConvertVer.Content = DSDConverter.Version;

                            NodeManage.Visibility = Visibility.Hidden;

                            MultiWindowController.HideAll();
                        }
                        break;
                    case "DashBoard":
                        {
                            if (IsNodeExpanded)
                            {
                                SyncAnimation();
                                ShowNodeMenu(false);
                                LogView.Visibility = Visibility.Collapsed;
                            }

                            if (TranslateView.CurrentHistory != null)
                            {
                                TranslateView.CurrentHistory.Visibility = Visibility.Hidden;
                            }

                            PageIndex = 1;
                            StopLexGlowLoop();

                            if (ActiveTab?.Mod?.TranslationStatus == StateControl.Run)
                            {
                                YDChart.Start();
                            }
                            else
                            {
                                YDChart.Clear();
                            }

                            NodeManage.Visibility = Visibility.Hidden;

                            MultiWindowController.HideAll();
                        }
                        break;
                    case "TransHub":
                        {
                            if (TranslateView.CurrentHistory != null)
                            {
                                TranslateView.CurrentHistory.Visibility = Visibility.Visible;
                            }

                            YDChart.Stop();
                            PageIndex = 2;
                            StopLexGlowLoop();

                            UpdateTabShowState();

                            NodeManage.Visibility = Visibility.Visible;

                            MultiWindowController.ShowAll();
                        }
                        break;
                    case "Settings":
                        {
                            if (IsNodeExpanded)
                            {
                                SyncAnimation();
                                ShowNodeMenu(false);
                                LogView.Visibility = Visibility.Collapsed;
                            }

                            if (TranslateView.CurrentHistory != null)
                            {
                                TranslateView.CurrentHistory.Visibility = Visibility.Hidden;
                            }

                            YDChart.Stop();
                            PageIndex = 3;
                            StopLexGlowLoop();

                            NodeManage.Visibility = Visibility.Hidden;

                            MultiWindowController.HideAll();
                        }
                        break;

                    default: return;
                }

                MainPage.SwitchPageByVertical(PageIndex);

                foreach (var Child in MainNav.Children)
                {
                    if (Child is Grid RowGrid)
                    {
                        foreach (var SubChild in RowGrid.Children)
                        {
                            if (SubChild is Grid MenuContainer)
                            {
                                var Border = MenuContainer.Children.OfType<Border>().FirstOrDefault();
                                if (Border != null)
                                {
                                    SetMenuSelectedState(Border, Border.Tag?.ToString() == Name);
                                }
                            }
                        }
                    }
                }
            });
        }

        private void SetMenuSelectedState(Border MenuBorder, bool IsSelected)
        {
            if (MenuBorder.Child is Grid InternalGrid)
            {
                var Icons = InternalGrid.Children.OfType<Viewbox>().ToList();
                var Grids = InternalGrid.Children.OfType<Grid>().ToList();

                if (Grids.Count >= 2 && Icons.Count > 0)
                {
                    var IndicatorBar = Grids[0];
                    var BGMask = Grids[1];
                    var Icon = Icons[0];

                    if (IsSelected)
                    {
                        IndicatorBar.Visibility = Visibility.Visible;
                        BGMask.Visibility = Visibility.Visible;
                        Icon.Opacity = 1;
                    }
                    else
                    {
                        IndicatorBar.Visibility = Visibility.Hidden;
                        BGMask.Visibility = Visibility.Hidden;
                        Icon.Opacity = 0.6;
                    }
                }
            }
        }

        #endregion

        private void ApplyTheme(string mode)
        {
            var resources = this.Resources;

            switch (mode)
            {
                case "Dark":
                    Application.Current.Resources["BackgroundColor"] = "#FF282828";
                    Application.Current.Resources["ForegroundColor"] = "White";
                    Application.Current.Resources["BorderColor"] = "#FF555555";
                    Application.Current.Resources["AccentColor"] = "#FF4D8CF7";
                    Application.Current.Resources["PanelBackground"] = "#FF3D3D3D";
                    break;

                case "Light":
                    Application.Current.Resources["BackgroundColor"] = "#FFF5F5F5";
                    Application.Current.Resources["ForegroundColor"] = "Black";
                    Application.Current.Resources["BorderColor"] = "#FFCCCCCC";
                    Application.Current.Resources["AccentColor"] = "#FF4D8CF7";
                    Application.Current.Resources["PanelBackground"] = "White";
                    break;
            }
        }

        #region FileTabs
        private Border _BtnScrollLeft;
        private Border _BtnScrollRight;
        private ScrollViewer _TabScrollViewer;
        private void NIMTabs_Loaded(object sender, RoutedEventArgs e)
        {
            NIMTabs.ApplyTemplate();

            _BtnScrollLeft = NIMTabs.Template?.FindName("BtnScrollLeft", NIMTabs) as Border;
            _BtnScrollRight = NIMTabs.Template?.FindName("BtnScrollRight", NIMTabs) as Border;
            _TabScrollViewer = NIMTabs.Template?.FindName("TabScrollViewer", NIMTabs) as ScrollViewer;

            if (_BtnScrollLeft != null)
                _BtnScrollLeft.PreviewMouseDown += ScrollTabsLeft_PreviewMouseDown;

            if (_BtnScrollRight != null)
                _BtnScrollRight.PreviewMouseDown += ScrollTabsRight_PreviewMouseDown;

            if (_TabScrollViewer != null)
            {
                _TabScrollViewer.PreviewMouseWheel += TabScrollViewer_PreviewMouseWheel;
                _TabScrollViewer.ScrollChanged += TabScrollViewer_ScrollChanged;
            }

            UpdateScrollButtonsState();
        }

        public void LoadFile()
        {
            ModFileDialog.Instance.Owner = this;
            ModFileDialog.Instance.Show();
        }

        public void LoadFile(string Path)
        {
            AddTab(Path, true);
            UpdateTabShowState();
        }
        private void SelectFile(object sender, MouseButtonEventArgs e)
        {
            LoadFile();
        }

        private void UpdateTabShowState()
        {
            bool IsEmpty = NIMTabs.Items.Count == 0;

            EmptyTabView.Visibility = IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            Tab.Visibility = IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        }
        public class FileTabContext
        {
            public string Path { get; set; }
            public TranslateView View { get; set; }
        }


        private TabItem _DraggedTab;
        private Point _DragStartPoint;
        private Border _DraggedTabBg;
        private AdornerLayer _AdornerLayer;
        private DragAdorner _DragAdorner;

        private void NIMTabs_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _DragStartPoint = e.GetPosition(null);
            _DraggedTab = FindAncestor<TabItem>(e.OriginalSource as DependencyObject);

            if (_DraggedTab == null)
            {
                WinHead_MouseLeftButtonDown(sender, e);
                return;
            }
        }

        private void NIMTabs_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_DraggedTab == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point CurrentPos = e.GetPosition(null);
            if (Math.Abs(CurrentPos.X - _DragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(CurrentPos.Y - _DragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_DraggedTabBg == null)
            {
                _DraggedTabBg = FindTabBg(_DraggedTab);
                AnimateOpacity(_DraggedTab, 0.35, 120);
                _DraggedTab.Cursor = Cursors.SizeWE;

                _AdornerLayer = AdornerLayer.GetAdornerLayer(NIMTabs);
                _DragAdorner = new DragAdorner(NIMTabs, _DraggedTab, e.GetPosition(NIMTabs));
                _AdornerLayer.Add(_DragAdorner);

                Mouse.Capture(NIMTabs, CaptureMode.SubTree);
            }

            _DragAdorner.UpdatePosition(e.GetPosition(NIMTabs));

            TabItem TargetTab = HitTestTabItem(e.GetPosition(NIMTabs));
            if (TargetTab == null || TargetTab == _DraggedTab)
                return;

            int DraggedIndex = NIMTabs.Items.IndexOf(_DraggedTab);
            int TargetIndex = NIMTabs.Items.IndexOf(TargetTab);
            if (DraggedIndex < 0 || TargetIndex < 0)
                return;

            NIMTabs.Items.RemoveAt(DraggedIndex);
            NIMTabs.Items.Insert(TargetIndex, _DraggedTab);
            NIMTabs.SelectedItem = _DraggedTab;

            FlashSwap(TargetTab);
        }

        private TabItem HitTestTabItem(Point PosInTabs)
        {
            HitTestResult Result = VisualTreeHelper.HitTest(NIMTabs, PosInTabs);
            if (Result == null)
                return null;

            return FindAncestor<TabItem>(Result.VisualHit);
        }

        private void NIMTabs_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_DraggedTab != null)
            {
                AnimateOpacity(_DraggedTab, 1.0, 150);
                _DraggedTab.ClearValue(FrameworkElement.CursorProperty);
            }

            if (Mouse.Captured == NIMTabs)
                Mouse.Capture(null);

            if (_AdornerLayer != null && _DragAdorner != null)
            {
                _AdornerLayer.Remove(_DragAdorner);
            }

            _DraggedTab = null;
            _DraggedTabBg = null;
            _AdornerLayer = null;
            _DragAdorner = null;
        }

        private void AnimateOpacity(TabItem Tab, double ToValue, int DurationMs)
        {
            DoubleAnimation Anim = new DoubleAnimation(ToValue, TimeSpan.FromMilliseconds(DurationMs));
            Tab.BeginAnimation(TabItem.OpacityProperty, Anim);
        }

        private void FlashSwap(TabItem Tab)
        {
            Border TabBg = FindTabBg(Tab);
            if (TabBg == null)
                return;

            SolidColorBrush FlashBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2A2A"));
            TabBg.Background = FlashBrush;

            ColorAnimation Anim = new ColorAnimation
            {
                From = (Color)ColorConverter.ConvertFromString("#FFFAE306"),
                To = (Color)ColorConverter.ConvertFromString("#FF2A2A2A"),
                Duration = TimeSpan.FromMilliseconds(280)
            };
            FlashBrush.BeginAnimation(SolidColorBrush.ColorProperty, Anim);
        }

        private Border FindTabBg(TabItem Tab)
        {
            Tab.ApplyTemplate();
            return Tab.Template?.FindName("TabBg", Tab) as Border;
        }

        private static T FindAncestor<T>(DependencyObject Current) where T : DependencyObject
        {
            while (Current != null && !(Current is T))
                Current = VisualTreeHelper.GetParent(Current);
            return Current as T;
        }

        public TranslateView ActiveTab = null;
        private void NIMTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var Tab = NIMTabs.SelectedItem as TabItem;
            if (Tab == null)
                return;

            var CTX = Tab.Tag as FileTabContext;
            if (CTX == null)
                return;

            foreach (UIElement Child in TabViews.Children)
            {
                Child.Visibility = Visibility.Hidden;
            }

            if (CTX.View != null)
            {
                if (!TabViews.Children.Contains(CTX.View))
                {
                    TabViews.Children.Add(CTX.View);
                }

                CTX.View.Visibility = Visibility.Visible;
                ActiveTab = CTX.View;
                ActiveTab.Active();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MultiWindowController.AttachMod(ActiveTab.LastSetKey, this, ActiveTab.Mod);

                    if (TranslateView.CurrentHistory != null)
                    {
                        ActiveTab.ShowHistory();
                    }
                }), DispatcherPriority.ContextIdle);

                TranslateConfigView.ChangeTab();
            }

            ScrollToSelectedTab();
        }

        private void NIMTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var Dep = e.OriginalSource as DependencyObject;

            if (Dep == null)
                return;

            var AnyBtn = FindAncestor<Border>(Dep);

            if (AnyBtn != null && AnyBtn.Tag?.ToString() == "NIMTabClose")
            {
                var TabItem = FindAncestor<TabItem>(Dep);

                if (TabItem != null)
                {
                    e.Handled = true;

                    MultiWindowController.CloseMod((TabItem.Tag as FileTabContext).View.Mod);

                    RemoveTab((TabItem.Tag as FileTabContext).Path);
                }
            }

            if (AnyBtn != null && AnyBtn.Tag?.ToString() == "NIMTabAdd")
            {
                e.Handled = true;

                LoadFile();

                return;
            }
        }
        public void AddTab(string Path, bool Select = true)
        {
            UI(() =>
            {
                foreach (TabItem Item in NIMTabs.Items)
                {
                    if (Item.Tag is FileTabContext CTX && CTX.Path == Path)
                    {
                        if (Select)
                            NIMTabs.SelectedItem = Item;

                        return;
                    }
                }

                var View = new TranslateView();
                View.SetFile(this, Path);
                View.Visibility = Visibility.Collapsed;

                var CTXNew = new FileTabContext
                {
                    Path = Path,
                    View = View
                };

                var Tab = new TabItem
                {
                    Header = System.IO.Path.GetFileName(Path),
                    Tag = CTXNew
                };

                NIMTabs.Items.Add(Tab);

                TabViews.Children.Add(View);

                if (Select)
                    NIMTabs.SelectedItem = Tab;

                UpdateTabShowState();

                UpdateScrollButtonsState();
            });
        }
        public void RemoveTab(string Path)
        {
            UI(() =>
            {
                TabItem Target = null;
                FileTabContext CTX = null;

                foreach (TabItem Item in NIMTabs.Items)
                {
                    if (Item.Tag is FileTabContext c && c.Path == Path)
                    {
                        Target = Item;
                        CTX = c;
                        break;
                    }
                }

                if (Target == null)
                    return;

                if (CTX != null && CTX.View != null)
                {
                    CTX.View.Close();
                    CTX.View.Visibility = Visibility.Collapsed;
                }

                if (ActiveTab == CTX.View)
                {
                    ActiveTab = null;
                }

                NIMTabs.Items.Remove(Target);

                UpdateTabShowState();

                UpdateScrollButtonsState();
            });
        }

        #region TabScroll

        private ScrollViewer GetTabScrollViewer()
        {
            return _TabScrollViewer;
        }

        private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var SV = sender as ScrollViewer;
            if (SV == null) return;

            SV.ScrollToHorizontalOffset(SV.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void TabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateScrollButtonsState();
        }

        public void UpdateScrollButtonsState()
        {
            if (_TabScrollViewer == null || _BtnScrollLeft == null || _BtnScrollRight == null)
                return;

            bool NeedsScroll = _TabScrollViewer.ScrollableWidth > 0.5;

            _BtnScrollLeft.Visibility = NeedsScroll ? Visibility.Visible : Visibility.Collapsed;
            _BtnScrollRight.Visibility = NeedsScroll ? Visibility.Visible : Visibility.Collapsed;

            _BtnScrollLeft.IsEnabled = _TabScrollViewer.HorizontalOffset > 0.5;
            _BtnScrollRight.IsEnabled = _TabScrollViewer.HorizontalOffset < _TabScrollViewer.ScrollableWidth - 0.5;

            _BtnScrollLeft.Opacity = _BtnScrollLeft.IsEnabled ? 1.0 : 0.3;
            _BtnScrollRight.Opacity = _BtnScrollRight.IsEnabled ? 1.0 : 0.3;
        }

        private const double TabScrollStep = 120;

        private void ScrollTabsLeft_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var SV = GetTabScrollViewer();
            SV?.ScrollToHorizontalOffset(SV.HorizontalOffset - TabScrollStep);
        }

        private void ScrollTabsRight_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var SV = GetTabScrollViewer();
            SV?.ScrollToHorizontalOffset(SV.HorizontalOffset + TabScrollStep);
        }

        private void ScrollToSelectedTab()
        {
            var SV = GetTabScrollViewer();
            var Selected = NIMTabs.SelectedItem as TabItem;
            if (SV == null || Selected == null) return;

            Selected.BringIntoView();
        }

        #endregion

        #endregion

        #region Nodes

        public bool IsNodeExpanded = false;
        private void ShowNodeMenu(object sender, MouseButtonEventArgs e)
        {
            if (IsNodeExpanded)
            {
                SyncAnimation();
                ShowNodeMenu(false);
                LogView.Visibility = Visibility.Collapsed;
            }
            else
            {
                SyncAnimation();
                ShowNodeMenu(true);
                LogView.Visibility = Visibility.Visible;
            }
        }



        private double CalcLeftMenuHeight()
        {
            double AutoHeight = 0;
            foreach (FrameworkElement GetRow in Nodes.Children)
            {
                AutoHeight += GetRow.ActualHeight + 1;
            }
            return AutoHeight;
        }

        //Control the speed to a fixed 2000 px/s
        private const double ExpandAnimationSpeed = 2000;
        public void SyncAnimation()
        {
            double AutoHeight = CalcLeftMenuHeight();

            var ExpandMenu = (Storyboard)FindResource("ExpandMenu");
            var ExpandAnimation = (DoubleAnimation)ExpandMenu.Children[0];
            ExpandAnimation.To = AutoHeight;
            ExpandAnimation.Duration = TimeSpan.FromSeconds(Math.Abs(0 - AutoHeight) / ExpandAnimationSpeed);

            var CollapseMenu = (Storyboard)FindResource("CollapseMenu");
            var CollapseAnimation = (DoubleAnimation)CollapseMenu.Children[0];
            CollapseAnimation.From = AutoHeight;
            CollapseAnimation.Duration = TimeSpan.FromSeconds(Math.Abs(AutoHeight - 0) / ExpandAnimationSpeed);

            CollapseAnimation.Completed += (_, __) =>
            {
                NodeMenu.Visibility = Visibility.Collapsed;
                NodeMenu.BeginAnimation(HeightProperty, null);
            };
        }
        public bool NodeMenuIsShow = false;
        public void ShowNodeMenu(bool Show)
        {
            NodeMenuIsShow = Show;

            this.Dispatcher.Invoke(new Action(() =>
            {
                if (Show)
                {
                    Mask.Visibility = Visibility.Visible;
                    Storyboard Storyboard = (Storyboard)this.Resources["ExpandMenu"];
                    Storyboard.Begin();

                    IsNodeExpanded = true;
                    NodeMenu.Visibility = Visibility.Visible;
                }
                else
                {
                    Mask.Visibility = Visibility.Collapsed;
                    Storyboard Storyboard = (Storyboard)this.Resources["CollapseMenu"];
                    Storyboard.Begin();

                    IsNodeExpanded = false;
                }
            }));
        }

        #endregion

        private void Mask_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (NodeMenuIsShow)
            {
                SyncAnimation();
                ShowNodeMenu(false);
                LogView.Visibility = Visibility.Collapsed;
            }
        }


        #region LogView

        private Border LastSetLogButton = null;
        private void SelectLogNav(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border)
            {
                Border GetBorderHandle = (Border)sender;

                if (GetBorderHandle.Child is Label)
                {
                    if (LastSetLogButton != null)
                    {
                        LastSetLogButton.Style = (Style)this.FindResource("LogViewButtonUnSelected");
                    }

                    string GetContent = P_Convert.ObjToStr(((Label)GetBorderHandle.Child).Content);

                    if (GetContent == "InputLog")
                    {
                        InputLog.Visibility = Visibility.Visible;
                        OutputLog.Visibility = Visibility.Collapsed;
                        MainLog.Visibility = Visibility.Collapsed;
                    }
                    else
                    if (GetContent == "OutputLog")
                    {
                        InputLog.Visibility = Visibility.Collapsed;
                        OutputLog.Visibility = Visibility.Visible;
                        MainLog.Visibility = Visibility.Collapsed;
                    }
                    else
                    if (GetContent == "Log")
                    {
                        InputLog.Visibility = Visibility.Collapsed;
                        OutputLog.Visibility = Visibility.Collapsed;
                        MainLog.Visibility = Visibility.Visible;
                    }

                    GetBorderHandle.Style = (Style)this.FindResource("LogViewButtonSelected");

                    LastSetLogButton = GetBorderHandle;
                }

            }
        }
        #endregion

        public void SyncPlatformConfig()
        {
            KeyConfigBlocks.Children.Clear();

            List<PlatformConfig> CloudAIs = new List<PlatformConfig>();
            List<PlatformConfig> LocalAIs = new List<PlatformConfig>();
            List<PlatformConfig> TraditionalPlatforms = new List<PlatformConfig>();

            for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                var Key = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                if (NIMApp.EngineSetting.PlatformConfigs[Key].CustomInFo != null)
                {
                    if (NIMApp.EngineSetting.PlatformConfigs[Key].CustomInFo.Type == CustomPlatformType.CloudAI)
                    {
                        CloudAIs.Add(NIMApp.EngineSetting.PlatformConfigs[Key]);
                    }
                    else
                    if (NIMApp.EngineSetting.PlatformConfigs[Key].CustomInFo.Type == CustomPlatformType.LocalAI)
                    {
                        LocalAIs.Add(NIMApp.EngineSetting.PlatformConfigs[Key]);
                    }
                    else
                    if (NIMApp.EngineSetting.PlatformConfigs[Key].CustomInFo.Type == CustomPlatformType.Traditional)
                    {
                        TraditionalPlatforms.Add(NIMApp.EngineSetting.PlatformConfigs[Key]);
                    }
                }
            }

            for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                var Key = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                var GetPlatform = NIMApp.EngineSetting.PlatformConfigs[Key];

                if (GetPlatform.Platform == PlatformType.ChatGpt ||
                   GetPlatform.Platform == PlatformType.Gemini ||
                   GetPlatform.Platform == PlatformType.DeepSeek)
                {
                    if (GetPlatform.CustomInFo == null)
                    {
                        switch (GetPlatform.Platform)
                        {
                            case PlatformType.ChatGpt:
                                {
                                    List<string> Models = new List<string>();
                                    Models.Add("gpt-5-nano");
                                    Models.Add("gpt-5-mini");
                                    Models.Add("gpt-4.1-nano");
                                    Models.Add("gpt-4.1-mini");
                                    Models.Add("gpt-4o-mini");

                                    KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenCloudAIConfig(0, "ChatGpt", "https://platform.openai.com/api-keys", true, GetPlatform.ApiKeys, GetPlatform.Model, CustomPlatformType.CloudAI, Models));
                                }
                                break;
                            case PlatformType.Gemini:
                                {
                                    List<string> Models = new List<string>();
                                    Models.Add("gemini-2.5-flash");
                                    Models.Add("gemini-2.0-flash");

                                    KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenCloudAIConfig(0, "Gemini", "https://aistudio.google.com/apikey", true, GetPlatform.ApiKeys, GetPlatform.Model, CustomPlatformType.CloudAI, Models));
                                }
                                break;
                            case PlatformType.DeepSeek:
                                {
                                    List<string> Models = new List<string>();
                                    Models.Add("deepseek-v4-pro");

                                    KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenCloudAIConfig(0, "DeepSeek", "https://platform.deepseek.com/api_keys", true, GetPlatform.ApiKeys, GetPlatform.Model, CustomPlatformType.CloudAI, Models));
                                }
                                break;
                        }
                    }
                }
            }

            foreach (var CustomPlatform in CloudAIs)
            {
                KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenCloudAIConfig(CustomPlatform.CustomInFo.CustomID, CustomPlatform.CustomInFo.Name, string.Empty, false, CustomPlatform.ApiKeys, CustomPlatform.Model, CustomPlatformType.CloudAI, new List<string>() { CustomPlatform.Model }));
            }

            for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                var Key = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                var GetPlatform = NIMApp.EngineSetting.PlatformConfigs[Key];

                if (GetPlatform.Platform == PlatformType.LMLocalAI)
                {
                    if (GetPlatform.CustomInFo == null)
                    {
                        switch (GetPlatform.Platform)
                        {
                            case PlatformType.LMLocalAI:
                                {
                                    KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenLocalAIConfig(0, "LM Studio", "https://lmstudio.ai/docs/developer", true, GetPlatform.LocalPort, LMStudio.CurrentModel, CustomPlatformType.LocalAI));
                                }
                                break;
                        }
                    }
                }
            }

            foreach (var CustomPlatform in LocalAIs)
            {
                KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenLocalAIConfig(CustomPlatform.CustomInFo.CustomID, CustomPlatform.CustomInFo.Name, string.Empty, false, CustomPlatform.LocalPort, CustomPlatform.Model, CustomPlatformType.LocalAI));
            }

            for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                var Key = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                var GetPlatform = NIMApp.EngineSetting.PlatformConfigs[Key];

                if (GetPlatform.Platform == PlatformType.DeepL)
                {
                    if (GetPlatform.CustomInFo == null)
                    {
                        switch (GetPlatform.Platform)
                        {
                            case PlatformType.DeepL:
                                {
                                    KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenTraditionalConfig(0, "DeepL", "https://www.deepl.com/your-account/keys", true, GetPlatform.ApiKeys, CustomPlatformType.Traditional));
                                }
                                break;
                        }
                    }
                }
            }

            foreach (var CustomPlatform in TraditionalPlatforms)
            {
                KeyConfigBlocks.Children.Add(NIMApp.PlatformConfigStyleWin.GenTraditionalConfig(CustomPlatform.CustomInFo.CustomID, CustomPlatform.CustomInFo.Name, string.Empty, false, CustomPlatform.ApiKeys, CustomPlatformType.Traditional));
            }
        }

        #region Setting
        public void SyncSettingUI(string Name)
        {
            if (Name.Equals("Request And ApiKey Configs"))
            {
                var NIMConfig = NIMApp.EngineSetting;

                SProxyUrl.Text = NIMApp.EngineSetting.ProxyUrl;
                SProxyUserName.Text = NIMApp.EngineSetting.ProxyUserName;
                SProxyPassword.Text = NIMApp.EngineSetting.ProxyPassword;

                SyncPlatformConfig();
            }
            else
            if (Name.Equals("AI Configs"))
            {
                SAIKeyword.Text = NIMApp.EngineSetting.UserCustomAIPrompt;
            }
            else
            if (Name.Equals("Game Configs"))
            {
                SGame.Items.Clear();
                SGame.Items.Add(GameNames.Skyrim.ToString());

                SGame.SelectedValue = NIMApp.SelfSetting.GameType.ToString();

                if (NIMApp.SelfSetting.ShowAssembly)
                {
                    SShowAssembly.IsChecked = true;
                }
                else
                {
                    SShowAssembly.IsChecked = false;
                }

                SCodeGenStyle.Items.Clear();

                SCodeGenStyle.Items.Add("CSharp");
                SCodeGenStyle.Items.Add("Papyrus");

                if (NIMApp.SelfSetting.GenCSharp)
                {
                    SCodeGenStyle.SelectedValue = SCodeGenStyle.Items[0];
                }
                else
                {
                    SCodeGenStyle.SelectedValue = SCodeGenStyle.Items[1];
                }

                EspReader TempEspReader = new EspReader();
                TempEspReader.Create(-5,new NIMEngine.Memory.P_Dict<string, NIMEngine.Memory.P_String>());

                EspFilterStr.Text = TempEspReader.GetFilterByStr(); 

                TempEspReader.Clear();
            }
            else
            if (Name.Equals("UI Configs"))
            {
                if (NIMApp.SelfSetting.TextDisplay == TextLayout.RTL)
                {
                    RTLEnable.IsChecked = true;
                }
                else
                {
                    RTLEnable.IsChecked = false;
                }
            }
            else
            if (Name.Equals("Engine Configs"))
            {
                LoadTranslationPresetControls();

                if (NIMApp.EngineSetting.ContextEnable)
                {
                    SContextEnable.IsChecked = true;
                }
                else
                {
                    SContextEnable.IsChecked = false;
                }

                SThrottlingRatio.Text = NIMApp.EngineSetting.ThrottleRatio.ToString();

                SRotationDelay.Text = NIMApp.EngineSetting.ThrottleDelayMs.ToString();

                SMaxThread.Text = NIMApp.EngineSetting.MaxThreadCount.ToString();

                if (NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase)
                {
                    AutoUpdateStringsFileToDatabase.IsChecked = true;
                }
                else
                {
                    AutoUpdateStringsFileToDatabase.IsChecked = false;
                }

                if (NIMApp.EngineSetting.EnableGlobalSearch)
                {
                    GlobalSearch.IsChecked = true;
                }
                else
                {
                    GlobalSearch.IsChecked = false;
                }

                if (NIMApp.SelfSetting.EnableLanguageDetect)
                {
                    SEnableLanguageDetect.IsChecked = true;
                }
                else
                {
                    SEnableLanguageDetect.IsChecked = false;
                }

                P_Placeholders.Text = NIMApp.SelfSetting.P_Placeholders;

                if (NIMApp.SelfSetting.CanTranslateBook)
                {
                    CanTranslateBook.IsChecked = true;
                }
                else
                {
                    CanTranslateBook.IsChecked = false;
                }

                if (NIMApp.SelfSetting.UseFullPunctuation)
                {
                    UseFullPunctuation.IsChecked = true;
                }
                else
                {
                    UseFullPunctuation.IsChecked = false;
                }

                if (NIMApp.SelfSetting.UseFullPunctuationJa)
                {
                    UseFullPunctuationJa.IsChecked = true;
                }
                else
                {
                    UseFullPunctuationJa.IsChecked = false;
                }
            }
        }
        public void ShowFrame(string Name)
        {
            for (int i = 0; i < this.SettingFrames.Children.Count; i++)
            {
                if (this.SettingFrames.Children[i] is Border)
                {
                    Border GetFrame = (Border)this.SettingFrames.Children[i];
                    string GetTag = P_Convert.ObjToStr(GetFrame.Tag);
                    if (GetTag.Equals(Name))
                    {
                        GetFrame.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        GetFrame.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        public void SelectSettingNav(Border Nav)
        {
            if (LastSetBorder != null)
            {
                SetUnSelectSettingNav(LastSetBorder);
            }

            SetSelectSettingNav(Nav);

            string GetName = GetSettingNavName(Nav);
            ShowFrame(GetName);
            SyncSettingUI(GetName);

            LastSetBorder = Nav;
        }
        private void SelectSettingNav(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border)
            {
                Border GetNav = (Border)sender;

                SelectSettingNav(GetNav);
            }
        }
        public void SelectFristSettingNav()
        {
            int i = 3 + 1;
            if (SettingNavs.Children.Count > i)
            {
                if (SettingNavs.Children[i] is Border)
                    SelectSettingNav((Border)SettingNavs.Children[i]);
            }
        }

        public Border LastSetBorder = null;
        public void SetSelectSettingNav(Border Nav)
        {
            if (Nav.Child is Grid)
            {
                Grid GetMainGrid = (Grid)Nav.Child;
                if (GetMainGrid.Children.Count == 2)
                {
                    if (GetMainGrid.Children[0] is TextBlock NavText)
                    {
                        NavText.Foreground = (Brush)this.FindResource("SettingAccentBrush");
                        NavText.FontWeight = FontWeights.SemiBold;
                    }
                    if (GetMainGrid.Children[1] is Border Indicator)
                    {
                        Indicator.Visibility = Visibility.Visible;
                    }
                }
            }

        }
        public void SetUnSelectSettingNav(Border Nav)
        {
            if (Nav.Child is Grid)
            {
                Grid GetMainGrid = (Grid)Nav.Child;
                if (GetMainGrid.Children.Count == 2)
                {
                    if (GetMainGrid.Children[0] is TextBlock NavText)
                    {
                        NavText.Foreground = (Brush)this.FindResource("SettingMutedBrush");
                        NavText.FontWeight = FontWeights.Normal;
                    }
                    if (GetMainGrid.Children[1] is Border Indicator)
                    {
                        Indicator.Visibility = Visibility.Collapsed;
                    }
                }
            }

        }

        public string GetSettingNavName(Border Nav)
        {
            return P_Convert.ObjToStr(Nav.Tag);
        }

        private void UILanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetValue = P_Convert.ObjToStr(UILanguages.SelectedValue);
            if (GetValue.Length > 0)
            {
                NIMApp.SelfSetting.CurrentUILanguage = (Languages)Enum.Parse(typeof(Languages), GetValue);
                UILanguageHelper.ChangeLanguage(NIMApp.SelfSetting.CurrentUILanguage);
            }
        }
        private void AutoUpdateStringsFileToDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (AutoUpdateStringsFileToDatabase.IsChecked == true)
            {
                NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase = true;
            }
            else
            {
                NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase = false;
            }

            NIMApp.SelfSetting.SaveConfig();
        }

        private void EnableGlobalSearch_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalSearch.IsChecked == true)
            {
                NIMApp.EngineSetting.EnableGlobalSearch = true;
            }
            else
            {
                NIMApp.EngineSetting.EnableGlobalSearch = false;
            }

            Phoenix.SaveConfig();
        }

        private void SEnableLanguageDetect_Click(object sender, RoutedEventArgs e)
        {
            if (SEnableLanguageDetect.IsChecked == true)
            {
                NIMApp.SelfSetting.EnableLanguageDetect = true;
            }
            else
            {
                NIMApp.SelfSetting.EnableLanguageDetect = false;
            }
        }

        private void P_Placeholders_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.SelfSetting.P_Placeholders = P_Placeholders.Text;
            NIMApp.SelfSetting.SaveConfig();
        }
        private void SCodeGenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var GetValue = P_Convert.ObjToStr(SCodeGenStyle.SelectedValue);
            if (GetValue.Length > 0)
                if (GetValue.Equals("CSharp"))
                {
                    NIMApp.SelfSetting.GenCSharp = true;
                }
                else
                {
                    NIMApp.SelfSetting.GenCSharp = false;
                }
        }

        private void SShowAssembly_Click(object sender, RoutedEventArgs e)
        {
            if (SShowAssembly.IsChecked == true)
            {
                NIMApp.SelfSetting.ShowAssembly = true;
            }
            else
            {
                NIMApp.SelfSetting.ShowAssembly = false;
            }

            NIMApp.SelfSetting.SaveConfig();
        }

        private void SProxyUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.ProxyUrl = SProxyUrl.Text;
        }

        private void SProxyUserName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.ProxyUserName = SProxyUserName.Text;
        }

        private void SProxyPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.ProxyPassword = SProxyPassword.Text;
        }

        private void SContextLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;
            if (_isUpdatingTranslationPresetControls ||
                !int.TryParse(SContextLimit.Text, out value))
            {
                return;
            }

            TranslationPresetSettings current = _translationPresetCoordinator.GetCurrentSettings();
            var settings = new TranslationPresetSettings(
                value,
                current.BucketLengthLimit,
                current.PreserveConversationContext,
                current.ForceContextDeduplication,
                current.StrictLinkBucketPurity);
            TranslationPresetSelection selection;
            if (_translationPresetCoordinator.TryApplyCustomSettings(settings, false, out selection))
            {
                ApplyTranslationPresetSelection(selection, false);
            }
        }

        private void SAIKeyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.UserCustomAIPrompt = SAIKeyword.Text.Trim();
        }

        private void SContextEnable_Click(object sender, RoutedEventArgs e)
        {
            if (SContextEnable.IsChecked == true)
            {
                NIMApp.EngineSetting.ContextEnable = true;
            }
            else
            {
                NIMApp.EngineSetting.ContextEnable = false;
            }
        }

        private void SGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetName = P_Convert.ObjToStr(SGame.SelectedValue);
            if (GetName.Trim().Length > 0)
            {
                NIMApp.SelfSetting.GameType = (GameNames)Enum.Parse(typeof(GameNames), GetName);
            }
        }
        public void SaveApiKey(PlatformType Type, string KeysStr)
        {
            for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                int GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == Type)
                {
                    NIMApp.EngineSetting.PlatformConfigs[GetKey].ApiKeys = NIMApp.EngineSetting.KeysStrToArray(KeysStr);
                    break;
                }
            }

            Phoenix.SaveConfig();
        }
        private void SThrottlingRatio_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.ThrottleRatio = P_Convert.ObjToDouble(SThrottlingRatio.Text);
        }
        private void SRotationDelay_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.ThrottleDelayMs = P_Convert.ObjToInt(SRotationDelay.Text);
        }

        private void SMaxThread_TextChanged(object sender, TextChangedEventArgs e)
        {
            NIMApp.EngineSetting.MaxThreadCount = P_Convert.ObjToInt(SMaxThread.Text);

            if (ActiveTab?.Mod?.P_Translator != null)
            {
                if (ActiveTab.Mod.P_Translator.GetBatchCore() != null)
                {
                    ActiveTab.Mod.P_Translator.GetBatchCore().AutoThreadLimit = NIMApp.EngineSetting.MaxThreadCount;
                }
            }
        }

        private void LoadTranslationPresetControls()
        {
            _isUpdatingTranslationPresetControls = true;
            try
            {
                Presets.Items.Clear();
                foreach (TranslationPreset preset in Enum.GetValues(typeof(TranslationPreset)))
                {
                    Presets.Items.Add(preset.ToString());
                }

                ApplyTranslationPresetSelection(_translationPresetCoordinator.Load(), true);
            }
            finally
            {
                _isUpdatingTranslationPresetControls = false;
            }
        }

        private void ApplyTranslationPresetSelection(
            TranslationPresetSelection selection,
            bool applySettingsToControls)
        {
            bool wasUpdating = _isUpdatingTranslationPresetControls;
            _isUpdatingTranslationPresetControls = true;
            try
            {
                if (applySettingsToControls)
                {
                    SContextLimit.Text = selection.Settings.ContextLimit.ToString();
                    BucketLengthLimit.Text = selection.Settings.BucketLengthLimit.ToString();
                    PreserveConversationContext.IsChecked =
                        selection.Settings.PreserveConversationContext;
                    ForceContextDeduplication.IsChecked =
                        selection.Settings.ForceContextDeduplication;
                    StrictLinkBucketPurity.IsChecked = selection.Settings.StrictLinkBucketPurity;
                }

                Presets.SelectedValue = selection.Preset.ToString();
                RadarChart.TranslationQuality = selection.Profile.TranslationQuality;
                RadarChart.TranslationSpeed = selection.Profile.TranslationSpeed;
            }
            finally
            {
                _isUpdatingTranslationPresetControls = wasUpdating;
            }
        }

        private void Presets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingTranslationPresetControls)
            {
                return;
            }

            TranslationPreset preset;
            if (!Enum.TryParse(P_Convert.ObjToStr(Presets.SelectedValue), out preset))
            {
                return;
            }

            TranslationPresetSelection selection;
            if (_translationPresetCoordinator.TrySelect(preset, out selection))
            {
                ApplyTranslationPresetSelection(
                    selection,
                    preset != TranslationPreset.Custom);
            }
        }

        private void BucketLengthLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;
            if (_isUpdatingTranslationPresetControls ||
                !int.TryParse(BucketLengthLimit.Text, out value))
            {
                return;
            }

            TranslationPresetSettings current = _translationPresetCoordinator.GetCurrentSettings();
            var settings = new TranslationPresetSettings(
                current.ContextLimit,
                value,
                current.PreserveConversationContext,
                current.ForceContextDeduplication,
                current.StrictLinkBucketPurity);
            ApplyCustomTranslationPresetSettings(settings, false);
        }

        private void PreserveConversationContext_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingTranslationPresetControls)
            {
                return;
            }

            TranslationPresetSettings current = _translationPresetCoordinator.GetCurrentSettings();
            ApplyCustomTranslationPresetSettings(
                new TranslationPresetSettings(
                    current.ContextLimit,
                    current.BucketLengthLimit,
                    PreserveConversationContext.IsChecked == true,
                    current.ForceContextDeduplication,
                    current.StrictLinkBucketPurity),
                true);
        }

        private void ForceContextDeduplication_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingTranslationPresetControls)
            {
                return;
            }

            TranslationPresetSettings current = _translationPresetCoordinator.GetCurrentSettings();
            ApplyCustomTranslationPresetSettings(
                new TranslationPresetSettings(
                    current.ContextLimit,
                    current.BucketLengthLimit,
                    current.PreserveConversationContext,
                    ForceContextDeduplication.IsChecked == true,
                    current.StrictLinkBucketPurity),
                true);
        }

        private void StrictLinkBucketPurity_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingTranslationPresetControls)
            {
                return;
            }

            TranslationPresetSettings current = _translationPresetCoordinator.GetCurrentSettings();
            ApplyCustomTranslationPresetSettings(
                new TranslationPresetSettings(
                    current.ContextLimit,
                    current.BucketLengthLimit,
                    current.PreserveConversationContext,
                    current.ForceContextDeduplication,
                    StrictLinkBucketPurity.IsChecked == true),
                true);
        }

        private void ApplyCustomTranslationPresetSettings(
            TranslationPresetSettings settings,
            bool persistImmediately)
        {
            TranslationPresetSelection selection;
            if (_translationPresetCoordinator.TryApplyCustomSettings(
                settings,
                persistImmediately,
                out selection))
            {
                ApplyTranslationPresetSelection(selection, false);
            }
        }


        private void RTLEnable_Click(object sender, RoutedEventArgs e)
        {
            if (RTLEnable.IsChecked == true)
            {
                NIMApp.SelfSetting.TextDisplay = TextLayout.RTL;
            }
            else
            {
                NIMApp.SelfSetting.TextDisplay = TextLayout.LTR;
            }
        }

        private void CanTranslateBook_Click(object sender, RoutedEventArgs e)
        {
            if (CanTranslateBook.IsChecked == true)
            {
                NIMApp.SelfSetting.CanTranslateBook = true;
            }
            else
            {
                NIMApp.SelfSetting.CanTranslateBook = false;
            }
        }

        private void UseFullPunctuation_Click(object sender, RoutedEventArgs e)
        {
            if (UseFullPunctuation.IsChecked == true)
            {
                NIMApp.SelfSetting.UseFullPunctuation = true;
            }
            else
            {
                NIMApp.SelfSetting.UseFullPunctuation = false;
            }
        }

        private void UseFullPunctuationJa_Click(object sender, RoutedEventArgs e)
        {
            if (UseFullPunctuationJa.IsChecked == true)
            {
                NIMApp.SelfSetting.UseFullPunctuationJa = true;
            }
            else
            {
                NIMApp.SelfSetting.UseFullPunctuationJa = false;
            }
        }

        private void ReSetFilter(object sender, MouseButtonEventArgs e)
        {
            NIMApp.SelfSetting.CustomFilterStr = string.Empty;
            NIMApp.SelfSetting.SaveConfig();

            EspReader TempEspReader = new EspReader();
            TempEspReader.Create(-6, new NIMEngine.Memory.P_Dict<string, NIMEngine.Memory.P_String>());

            EspFilterStr.Text = TempEspReader.GetFilterByStr();

            TempEspReader.Close();
        }

        private void SetFilter(object sender, MouseButtonEventArgs e)
        {
            try
            {
                EspReader TempEspReader = new EspReader();
                TempEspReader.Create(-6,new NIMEngine.Memory.P_Dict<string, NIMEngine.Memory.P_String>());

                var FilterDict = TempEspReader.ParseFilterString(EspFilterStr.Text);

                var SourceFilterStr = TempEspReader.GetFilterByStr();

                if (SourceFilterStr.ToUpper() != EspFilterStr.Text.ToUpper())
                {
                    if (FilterDict.Count > 0)
                    {
                        NIMApp.SelfSetting.CustomFilterStr = EspFilterStr.Text;
                        NIMApp.SelfSetting.SaveConfig();
                    }
                }

                TempEspReader.Close();
            }
            catch
            {
                MessageBoxExtend.Show(this,"Msg", "The string used to set the filter is incorrect.",PreviewDialogSeverity.Warning);
            }
        }


        #endregion

        #region Drag
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] Files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var FileItem in Files)
                {
                    string GetFilePath = System.IO.Path.GetFullPath(FileItem);
                    if (GetFilePath.Length >= 260)
                        GetFilePath = @"\\?\" + GetFilePath;

                    if (File.Exists(GetFilePath))
                    {
                        Dispatcher.BeginInvoke(new Action(() => LoadFile(GetFilePath)),
                            System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }
        }





        #endregion

        private void ChangeToModern(object sender, MouseButtonEventArgs e)
        {
            if (NIMTabs.Items.Count > 0)
            {
                MessageBoxExtend.Show(this,"Msg","There are files in the workspace. Please clear the workspace before switching layouts.",PreviewDialogSeverity.Warning);
            }
            else
            {
                NIMApp.CurrentLayout = new PreviewShellWindow(_diagnostics);
                NIMApp.CurrentLayout.Show();

                NIMApp.SelfSetting.Layout = NIMLayout.Modern;//Update the configuration file; the Modern layout will be selected on the next startup.

                this.CanExit = false;
                this.Close();
            }
        }
    }
}
