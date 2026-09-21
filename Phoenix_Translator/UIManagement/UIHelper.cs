using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using PhoenixTranslator.SkyrimManagement;
using PhoenixTranslator.UIManagement;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Xml;
using System.Windows.Markup;
using static PhoenixTranslator.UIManagement.NodeStyleWin;
using System.Runtime.CompilerServices;
using PhoenixEngine.Translate;
using PhoenixEngine;
using PhoenixEngine.Platform;
using PhoenixEngine.Unit;
using ModFileParser;
using PhoenixTranslator.UIManagement.Preview;
using System.Web.WebSockets;

namespace PhoenixTranslator.UIManage
{
    public class ScanAnimator
    {
        private readonly TranslateTransform _ScanTransform;
        private readonly FrameworkElement _ProcessBar;
        private DoubleAnimation _Animation;

        private readonly double _Speed = 120;
        private double _PendingTo;

        public ScanAnimator(TranslateTransform scanTransform, FrameworkElement processBar, double speed = 120)
        {
            _ScanTransform = scanTransform;
            _ProcessBar = processBar;
            _Speed = speed;
        }

        public void UpdateAnimationTarget()
        {
            _PendingTo = _ProcessBar.ActualWidth;
        }

        public void Start()
        {
            if (_Animation != null) return;
            StartNewCycle();
        }

        public void Stop()
        {
            _ScanTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _Animation = null;
        }

        private void StartNewCycle()
        {
            double from = -30;
            double to = _PendingTo > 0 ? _PendingTo : _ProcessBar.ActualWidth;
            if (to <= 0) return;

            double distance = to - from;
            double durationSeconds = distance / _Speed;

            _Animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                AutoReverse = false,
                RepeatBehavior = new RepeatBehavior(1),
                FillBehavior = FillBehavior.Stop
            };

            Timeline.SetDesiredFrameRate(_Animation, 15);

            _Animation.Completed += (s, e) =>
            {
                _ScanTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _Animation = null;

                StartNewCycle();
            };

            _ScanTransform.BeginAnimation(TranslateTransform.XProperty, _Animation);
        }
    }

    public class UIHelper
    {
        #region ModFileDialog
        private static SolidColorBrush EntryPicBG = new SolidColorBrush(Color.FromRgb(72,72,72));
        private static SolidColorBrush EntryTextBG = new SolidColorBrush(Color.FromRgb(48,48,48));
        private static SolidColorBrush EntryTextColor = new SolidColorBrush(Colors.White);
        public static List<Grid> CreateEntryLine(List<SkyrimEntry> Entries)
        {
            List<Grid> Grids = new List<Grid>();
            foreach (var GetEntry in Entries)
            {
                if (GetEntry.Type == SkyrimEntryType.Null)
                {
                    Grids.Add(new Grid());
                }
                else
                {
                    Grid MainGrid = new Grid();

                    RowDefinition Row1st = new RowDefinition();
                    RowDefinition Row2nd = new RowDefinition();

                    Row1st.Height = new GridLength(0.7, GridUnitType.Star);
                    Row2nd.Height = new GridLength(0.3, GridUnitType.Star);

                    MainGrid.RowDefinitions.Add(Row1st);
                    MainGrid.RowDefinitions.Add(Row2nd);

                    MainGrid.Margin = new Thickness(2.5);

                    if (GetEntry.Type == SkyrimEntryType.Folder)
                    {
                        Grid PicGrid = new Grid();
                        Border PicBorder = new Border();
                        PicBorder.CornerRadius = new CornerRadius(9,9,0,0);
                        PicBorder.Background = EntryPicBG;

                        PreviewIcon Icon = new PreviewIcon();

                        Icon.Icon = PreviewIconName.Projects;
                        Icon.FontSize = 25;
                        Icon.Foreground = EntryTextColor;

                        PicBorder.Child = Icon;
                        PicGrid.Children.Add(PicBorder);

                        Grid.SetRow(PicGrid,0);
                        MainGrid.Children.Add(PicGrid);

                        Border TextBorder = new Border();
                        TextBorder.CornerRadius = new CornerRadius(0,0,9,9);
                        TextBorder.Background = EntryTextBG;

                        TextBox Tittle = new TextBox();
                        Tittle.FontSize = 8;
                        Tittle.Text = GetEntry.Name;
                        Tittle.Foreground = EntryTextColor;
                        Tittle.VerticalAlignment = VerticalAlignment.Center;
                        Tittle.HorizontalAlignment = HorizontalAlignment.Center;
                        Tittle.FontWeight = FontWeights.Bold;
                        Tittle.IsReadOnly = true;
                        Tittle.BorderBrush = null;
                        Tittle.BorderThickness = new Thickness(0);
                        Tittle.Background = null;
                        Tittle.TextWrapping = TextWrapping.Wrap;
                        Tittle.AcceptsReturn = true;
                        Tittle.TabIndex = -1;
                        Tittle.HorizontalContentAlignment = HorizontalAlignment.Center;

                        TextBorder.Child = Tittle;

                        Grid.SetRow(TextBorder, 1);
                        MainGrid.Children.Add(TextBorder);

                        Grids.Add(MainGrid);
                    }
                    else
                    if (GetEntry.Type == SkyrimEntryType.Mod)
                    {
                        Grid PicGrid = new Grid();
                        Border PicBorder = new Border();
                        PicBorder.CornerRadius = new CornerRadius(9, 9, 0, 0);
                        PicBorder.Background = EntryPicBG;

                        PreviewIcon Icon = new PreviewIcon();

                        Icon.Icon = PreviewIconName.Document;
                        Icon.FontSize = 25;
                        Icon.Foreground = EntryTextColor;

                        PicBorder.Child = Icon;
                        PicGrid.Children.Add(PicBorder);

                        Grid.SetRow(PicGrid, 0);
                        MainGrid.Children.Add(PicGrid);

                        Border TextBorder = new Border();
                        TextBorder.CornerRadius = new CornerRadius(0, 0, 9, 9);
                        TextBorder.Background = EntryTextBG;

                        TextBox Tittle = new TextBox();
                        Tittle.FontSize = 8;
                        Tittle.Text = GetEntry.Mod.ModName;
                        Tittle.Tag = GetEntry.Mod.ModID;
                        Tittle.Foreground = EntryTextColor;
                        Tittle.VerticalAlignment = VerticalAlignment.Center;
                        Tittle.HorizontalAlignment = HorizontalAlignment.Center;
                        Tittle.FontWeight = FontWeights.Bold;
                        Tittle.IsReadOnly = true;
                        Tittle.BorderBrush = null;
                        Tittle.BorderThickness = new Thickness(0);
                        Tittle.Background = null;
                        Tittle.TextWrapping = TextWrapping.Wrap;
                        Tittle.AcceptsReturn = true;
                        Tittle.TabIndex = -1;
                        Tittle.HorizontalContentAlignment = HorizontalAlignment.Center;

                        TextBorder.Child = Tittle;

                        Grid.SetRow(TextBorder, 1);
                        MainGrid.Children.Add(TextBorder);

                        Grids.Add(MainGrid);
                    }
                }
              
            }
            return Grids;
        }

        #endregion
        public static void ShowButton(Border NormalButton, bool Enable)
        {
            if (Enable)
            {
                NormalButton.Cursor = Cursors.Hand;
                NormalButton.IsEnabled = true;
                NormalButton.Opacity = 1;
            }
            else
            {
                NormalButton.Cursor = null;
                NormalButton.Opacity = 0.5;
                NormalButton.IsEnabled = false;
            }
        }

        public static Grid SelectLine = null;

        public static double DefLineHeight = 42;
        public static double DefFontSize = 15;

        public static readonly Typeface _TypeFace =
    new Typeface(SystemFonts.MessageFontFamily,
                 FontStyles.Normal,
                 FontWeights.Normal,
                 FontStretches.Normal);

        public static double MeasureTextWidth(string Text, double FontSize)
        {
            if (string.IsNullOrEmpty(Text))
                return 0;


            if (Text.Length < 16)
                return Text.Length * FontSize * 0.6;

            var Font = new FormattedText(
                Text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _TypeFace,
                FontSize,
                Brushes.Black,
                null,
                1);

            return Font.WidthIncludingTrailingWhitespace;
        }

        public static FakeGrid CreateFakeLine(string Type, string Key, string SourceText, string TransText, double Score)
        {
            double AutoHeight = DefLineHeight;

            if (!string.IsNullOrEmpty(SourceText))
            {
                var MaxWidth = (PhoenixApp.WorkWin.ActualWidth / 3) - 135;

                int RoughCharLimit = (int)(MaxWidth / (SystemFonts.MessageFontSize * 0.6));

                if (SourceText.Length > RoughCharLimit)
                {
                    AutoHeight = 82;
                }
                else
                {
                    double Width = MeasureTextWidth(SourceText, SystemFonts.MessageFontSize);
                    if (Width > MaxWidth)
                        AutoHeight = 82;
                }
            }

            return new FakeGrid(AutoHeight, Type, Key, SourceText, TransText, Score);
        }

        public static Grid CreateLine(ModFile File, FakeGrid Item)
        {
            bool IsModify = false;
            if (Item.RealSource.Length > 0)
            {
                if (Item.RealSource != Item.Source)
                {
                    IsModify = true;
                }
            }
            return CreateLine(File,IsModify, Item.Height, Item.Type, Item.Key, Item.Source, Item.Translated,"",Item.Score);
        }

        public static Grid CreateLine(ModFile Mod,bool IsModify,double Height, string Type, string Key, string SourceText, string TransText, string Emotion, double Score)
        {
            Grid MainGrid = PhoenixApp.RowStyleWin.CreateLine(Mod, IsModify,Height, new BaseUnit(Mod.P_Translator.GetFileUniqueKey(), Key, Type, SourceText, TransText, Emotion, Score));
            return MainGrid;
        }

        public static void SyncFromStringsFile(ModFile Mod, YDListView View)
        {
            if (Mod.EspReader == null)
            {
                return;
            }

            var CanVasHandle = View.GetMainCanvas();
            CanVasHandle.Dispatcher.Invoke(new Action(() =>
            {
                CanVasHandle.IsEnabled = false;
            }));

            for (int i = 0; i < View.RealLines.Count; i++)
            {
                var Line = View.RealLines[i];

                if (Mod.EspReader.Records.ContainsKey(Line.Key))
                {
                    var GetRealRecord = Mod.EspReader.Records[Line.Key];
                    if (Mod.FromStringsFile.Strings.ContainsKey(GetRealRecord.StringID))
                    {
                        View.RealLines[i].Source = Mod.FromStringsFile.Strings[GetRealRecord.StringID].Value;
                        View.RealLines[i].RealSource = string.Empty;
                        View.RealLines[i].SyncUI(View);
                    }
                }
            }

            CanVasHandle.Dispatcher.Invoke(new Action(() =>
            {
                CanVasHandle.IsEnabled = true;
            }));
        }

        public static void TransViewSyncEspRecord(EspReader Instance,string ParentSig, YDListView View)
        {
            var CanVasHandle = View.GetMainCanvas();
            CanVasHandle.Dispatcher.Invoke(new Action(() =>
            {
                CanVasHandle.IsEnabled = false;
            }));

            var AllRecords = Instance.SelectSig(ParentSig);

            const int BatchSize = 10000;
            int Total = AllRecords.Count;

            for (int i = 0; i < Total; i += BatchSize)
            {
                var Batch = AllRecords.Skip(i).Take(BatchSize).ToList();

                View.Parent.Dispatcher.Invoke(new Action(() =>
                {
                    foreach (var Record in Batch)
                    {
                        View.AddRowR(LineRenderer.CreateLine(
                            Record.Value.ParentSig,
                            Record.Value.FormID,
                            Record.Value.UniqueKey,
                            Record.Value.String,
                            "",
                            999));
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            CanVasHandle.Dispatcher.Invoke(new Action(() =>
            {
                CanVasHandle.IsEnabled = true;
            }));
        }

        static class NodeLightController
        {
            private static readonly ConditionalWeakTable<Grid, CancellationTokenSource> _lightMap
                = new ConditionalWeakTable<Grid, CancellationTokenSource>();

            public static void Blink(Grid grid, ContentControl light, int ms)
            {
                if (_lightMap.TryGetValue(grid, out _))
                    return;

                var cts = new CancellationTokenSource();
                _lightMap.Add(grid, cts);

                light.Dispatcher.Invoke(() =>
                {
                    light.Style = (Style)Application.Current.FindResource("IndicatorOnStyle");
                });

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(ms, cts.Token);

                        light.Dispatcher.Invoke(() =>
                        {
                            light.Style = (Style)Application.Current.FindResource("IndicatorOffStyle");
                        });
                    }
                    catch (TaskCanceledException) { }
                    finally
                    {
                        _lightMap.Remove(grid);
                    }
                });
            }
        }

        public static void NodeCallCallback(int CustomID, PlatformType Sign)
        {
            try
            {
                if (!PhoenixApp.WorkWin.IsNodeExpanded)
                {
                    return;
                }

                PhoenixApp.WorkWin.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        for (int i = 0; i < PhoenixApp.WorkWin.Nodes.Children.Count; i++)
                        {
                            if (PhoenixApp.WorkWin.Nodes.Children[i] is Grid)
                            {
                                Grid SetGrid = PhoenixApp.WorkWin.Nodes.Children[i] as Grid;
                                if (SetGrid.Children[0] is Grid)
                                {
                                    HeaderInFo GetHeader = SetGrid.Tag as HeaderInFo;
                                    if (GetHeader.CustomID <= 0 && GetHeader.MainType == PlatformType.Null)
                                    {
                                        if (Sign == PlatformType.PhoenixEngine)
                                        {
                                            ContentControl GetLight = PhoenixApp.NodeStyleWin.GetNodeLight(SetGrid);
                                            NodeLightController.Blink(SetGrid, GetLight, 1000);
                                        }
                                    }
                                    else
                                    if (GetHeader.CustomID <= 0 && GetHeader.MainType == Sign)
                                    {
                                        ContentControl GetLight = PhoenixApp.NodeStyleWin.GetNodeLight(SetGrid);
                                        NodeLightController.Blink(SetGrid, GetLight, 1000);
                                    }
                                    else
                                    if (GetHeader.CustomID > 0 && GetHeader.CustomID == CustomID)
                                    {
                                        ContentControl GetLight = PhoenixApp.NodeStyleWin.GetNodeLight(SetGrid);
                                        NodeLightController.Blink(SetGrid, GetLight, 1000);
                                    }
                                }

                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        public static void SyncAvalonEditTextLayout(TranslateView View)
        {
            if (PhoenixApp.SelfSetting.TextDisplay == TextLayout.LTR)
            {
                View.ToStr.FlowDirection = FlowDirection.LeftToRight;
            }
            else
            {
                View.ToStr.FlowDirection = FlowDirection.RightToLeft;
            }
        }
        public enum StyleType
        {
            BlueStyle = 0, RetroStyle = 1
        }
        
        public static void SetGlobalStyle(StyleType Style)
        {
            switch (Style)
            {
                case StyleType.BlueStyle:
                    {
                        LoadResourceDictionary("/Themes/BlueStyle.xaml");
                    }
                    break;
                case StyleType.RetroStyle:
                    {
                        LoadResourceDictionary("/Themes/RetroStyle.xaml");
                    }
                    break;
            }
        }

        public static void LoadResourceDictionary(string resourceName)
        {
            var Dict = new ResourceDictionary();
            Dict.Source = new Uri(resourceName, UriKind.Relative);
            try
            {
                Application.Current.Resources.MergedDictionaries.Clear();
            }
            catch { }
            try
            {
                Application.Current.Resources.MergedDictionaries.Add(Dict);
            }
            catch { }
            Application.Current.MainWindow?.InvalidateVisual();
            Application.Current.MainWindow?.UpdateLayout();
        }

        public static T CloneElement<T>(T source) where T : UIElement
        {
            try
            {
                if (source == null) return null;

                string xaml = XamlWriter.Save(source);
                StringReader stringReader = new StringReader(xaml);
                System.Xml.XmlReader xmlReader = System.Xml.XmlReader.Create(stringReader);

                return (T)XamlReader.Load(xmlReader);
            }
            catch { return null; }
        }

        public static void SyncNodes(StackPanel Nodes)
        {
            List<PlatformConfig> CustomPlatforms = new List<PlatformConfig>();

            List<PlatformConfig> LocalAIPlatforms = new List<PlatformConfig>();
            List<PlatformConfig> CloudAIPlatforms = new List<PlatformConfig>();
            List<PlatformConfig> TraditionalPlatforms = new List<PlatformConfig>();

            List<PlatformConfig> InteractivePlatforms = new List<PlatformConfig>();//This is a special node.

            for (int i = 0; i < PhoenixApp.EngineSetting.PlatformConfigs.Count; i++)
            {
                var GetKey = PhoenixApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                if (PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.CustomPlatform)
                {
                    CustomPlatforms.Add(PhoenixApp.EngineSetting.PlatformConfigs[GetKey]);
                }
                else
                if (PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.LMLocalAI)
                {
                    LocalAIPlatforms.Add(PhoenixApp.EngineSetting.PlatformConfigs[GetKey]);
                }
                else
                if (PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.ChatGpt
                    || PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.Gemini
                    || PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.DeepSeek)
                {
                    CloudAIPlatforms.Add(PhoenixApp.EngineSetting.PlatformConfigs[GetKey]);
                }
                else
                {
                    if (PhoenixApp.EngineSetting.PlatformConfigs[GetKey].Platform != PlatformType.HumanTranslation)
                    {
                        TraditionalPlatforms.Add(PhoenixApp.EngineSetting.PlatformConfigs[GetKey]);
                    }
                    else
                    {
                        InteractivePlatforms.Add(PhoenixApp.EngineSetting.PlatformConfigs[GetKey]);
                    }
                }
            }

            Nodes.Children.Clear();

            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenMainNodeTree("Engine Nodes"));
            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,"PreTranslate Node", PlatformType.Null, CustomPlatformType.Null, 0, PhoenixApp.EngineSetting.PreTranslateEnable));

            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNodeTree("Cloud AI Nodes"));
            foreach (var Get in CloudAIPlatforms)
            {
                Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.Platform.ToString(), Get.Platform, CustomPlatformType.CloudAI, 0, Get.Enable));
            }

            foreach (var Get in CustomPlatforms)
            {
                if (Get.CustomInFo != null)
                {
                    if (Get.CustomInFo.Type == CustomPlatformType.CloudAI)
                    {
                        Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.CustomInFo.Name, Get.Platform, Get.CustomInFo.Type, Get.CustomInFo.CustomID, Get.Enable));
                    }
                }
            }
            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenEmptyNode(CustomPlatformType.CloudAI));


            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNodeTree("Local AI Nodes"));
            foreach (var Get in LocalAIPlatforms)
            {
                string AutoName = Get.Platform.ToString();
                if (AutoName == "LMLocalAI")
                {
                    AutoName = "LM Studio";
                }
                Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,AutoName, Get.Platform, CustomPlatformType.LocalAI, 0, Get.Enable));
            }

            foreach (var Get in CustomPlatforms)
            {
                if (Get.CustomInFo != null)
                {
                    if (Get.CustomInFo.Type == CustomPlatformType.LocalAI)
                    {
                        Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.CustomInFo.Name, Get.Platform, Get.CustomInFo.Type, Get.CustomInFo.CustomID, Get.Enable));
                    }
                }
            }
            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenEmptyNode(CustomPlatformType.LocalAI));

            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNodeTree("Traditional Nodes"));
            foreach (var Get in TraditionalPlatforms)
            {
                Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.Platform.ToString(), Get.Platform, CustomPlatformType.Traditional, 0, Get.Enable));
            }

            foreach (var Get in CustomPlatforms)
            {
                if (Get.CustomInFo != null)
                {
                    if (Get.CustomInFo.Type == CustomPlatformType.Traditional)
                    {
                        Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.CustomInFo.Name, Get.Platform, Get.CustomInFo.Type, Get.CustomInFo.CustomID, Get.Enable));
                    }
                }
            }

            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenEmptyNode(CustomPlatformType.Traditional));

            Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNodeTree("Interactive Nodes"));

            foreach (var Get in InteractivePlatforms)
            {
                Nodes.Children.Add(PhoenixApp.NodeStyleWin.GenNode(Nodes,Get.Platform.ToString(), Get.Platform, CustomPlatformType.Interactive, 0, Get.Enable));
            }

            PhoenixApp.NodeStyleWin.SyncCount(Nodes);
        }

    }

    public enum TextLayout
    {
        LTR = 0, RTL = 1
    }
}
