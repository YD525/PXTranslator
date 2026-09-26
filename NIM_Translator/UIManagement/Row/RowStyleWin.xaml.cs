using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit;
using NIM.SkyrimManagement;
using NIM.UIManage;
using PhoenixEngine.Translate;
using PhoenixEngine.Additional;
using PhoenixEngine.Unit;
using PhoenixEngine.Common;
using PhoenixEngine.Memory;
using NIM.ModParser;

namespace NIM.UIManagement
{
    /// <summary>
    /// Interaction logic for RowStyleWin.xaml
    /// </summary>
    public partial class RowStyleWin : Window
    {
        private static readonly SolidColorBrush ModifiedStateBrush;

        static RowStyleWin()
        {
            ModifiedStateBrush = new SolidColorBrush(Color.FromRgb(247, 241, 186));
            ModifiedStateBrush.Freeze();
        }

        public RowStyleWin()
        {
            InitializeComponent();
            this.Hide();
        }

        //Oh no, the way I wrote this is a disaster—I completely forgot the order.
        //Refactoring this area involves a massive amount of work... some of the code here is a real nightmare.
        public static void SetColor(Grid Grid, int R, int G, int B)
        {
            Color FontColor = Color.FromRgb((byte)R, (byte)G, (byte)B);

            Grid MainGrid = Grid;

            Border MainBorder = (Border)MainGrid.Children[0];

            Grid GetChildGrid = (Grid)MainBorder.Child;

            Grid GetTypeGrid = (Grid)GetChildGrid.Children[1];
            StackPanel GetStackPanel = (StackPanel)GetTypeGrid.Children[0];

            TextBox GetType = (TextBox)GetStackPanel.Children[0];
            GetType.Foreground = new SolidColorBrush(FontColor); 

            StackPanel GetKeyPanel = (StackPanel)((Grid)GetChildGrid.Children[0]).Children[0];
            TextBox GetKey = (TextBox)GetKeyPanel.Children[1];
            GetKey.Foreground = new SolidColorBrush(FontColor); 

            Grid GetOriginalGrid = (Grid)GetChildGrid.Children[2];
            TextBox GetOriginal = (TextBox)GetOriginalGrid.Children[0];
            GetOriginal.Foreground = new SolidColorBrush(FontColor); 

            Grid GetTranslatedGrid = (Grid)GetChildGrid.Children[3];
            Border GetTranslatedBorder = (Border)GetTranslatedGrid.Children[0];

            TextEditor GetTranslated = (TextEditor)(GetTranslatedBorder.Child);
            GetTranslated.Foreground = new SolidColorBrush(FontColor); 
        }

        public static string GetType(Grid Grid)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid GetTypeGrid = (Grid)GetDataGrid.Children[1];
            StackPanel GetTypePanel = (StackPanel)GetTypeGrid.Children[0];

            TextBox GetType = GetTypePanel.Children[0] as TextBox;

            return GetType.Text;
        }

        public static string GetKey(Grid Grid)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);
            StackPanel GetStackPanel = (StackPanel)((Grid)GetDataGrid.Children[0]).Children[0];
            TextBox GetKey = (TextBox)GetStackPanel.Children[1];

            return GetKey.Text;
        }

        public static void MarkLeader(Grid Grid, bool Visible = true)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid GetKeyGrid = (Grid)GetDataGrid.Children[1];
            StackPanel GetTypePanel = (StackPanel)GetKeyGrid.Children[0];

            Grid GetLeader = (Grid)(GetTypePanel).Children[1];
            if (Visible)
            {
                GetLeader.Visibility = Visibility.Visible;
            }
            else
            {
                GetLeader.Visibility = Visibility.Hidden;
            }
        }

        public static void SetOriginal(Grid Grid, string Text)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid GetOriginalGrid = (Grid)GetDataGrid.Children[2];

            TextBox GetOriginal = (TextBox)GetOriginalGrid.Children[0];

            GetOriginal.Text = Text;
        }

        public static string GetOriginal(Grid Grid)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid GetOriginalGrid = (Grid)GetDataGrid.Children[2];

            TextBox GetOriginal = (TextBox)GetOriginalGrid.Children[0];

            return GetOriginal.Text;
        }

        public static string GetTranslated(Grid Grid)
        {
            Grid GetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid GetTranslatedGrid = (Grid)GetDataGrid.Children[3];

            Border GetTranslatedBorder = (Border)GetTranslatedGrid.Children[0];

            TextEditor GetTranslated = (TextEditor)(GetTranslatedBorder.Child);

            return GetTranslated.Text;
        }

        public static void SetTranslated(Grid Grid, string Translated)
        {
            Grid SetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid SetTranslatedGrid = (Grid)SetDataGrid.Children[3];

            Border SetTranslatedBorder = (Border)SetTranslatedGrid.Children[0];

            TextEditor SetTranslated = (TextEditor)(SetTranslatedBorder.Child);

            SetTranslated.Text = Translated;
        }

        public ColumnDefinition GetColorCol(Grid Grid)
        {
            Grid SetDataGrid = ((Grid)((Border)Grid.Children[0]).Child);

            Grid SetTranslatedGrid = (Grid)SetDataGrid.Children[3];

            return SetTranslatedGrid.ColumnDefinitions[1];
        }

        public static List<string> RecordModifyStates = new List<string>();

        public Grid CreateLine(ModFile Mod, bool IsModify, double Height, BaseUnit Item)
        {
            var FindDictionary = Mod.OriginalDictionaryReader.CheckDictionary(Item.Key);

            if (FindDictionary != null)
            {
                if (FindDictionary.OriginalText.Trim().Length > 0)
                {
                    if (Item.Original != FindDictionary.OriginalText)
                    {
                        Item.Original = FindDictionary.OriginalText;

                        //11 116 209
                        IsModify = true;

                        if (!RecordModifyStates.Contains(Item.Key))
                        {
                            RecordModifyStates.Add(Item.Key);
                        }
                    }
                }
            }

            bool CanQueryAdvancedDictionary = false;

            if (Item.GetRealOriginal() != Item.Original && Item.GetRealOriginal().Length > 0 && Item.Original.Length > 0)
            {
                CanQueryAdvancedDictionary = true;
            }

            var QueryTranslated = Mod.P_Translator.QueryTransData(Item.Key, Item.Type, Item.Original, CanQueryAdvancedDictionary);

            if (QueryTranslated != null)
            {
                if (QueryTranslated?.TransText.Length > 0)
                {
                    Item.Translated = QueryTranslated.TransText;
                }
            }

            Color FontColor = Colors.White;

            var QueryColor = FontColorFinder.FindColor(Mod.P_Translator.GetFileUniqueKey(), Item.Key);

            if (QueryColor != null)
            {
                FontColor = Color.FromRgb((byte)QueryColor.R, (byte)QueryColor.G, (byte)QueryColor.B);
            }

            Grid MainGrid = UIHelper.CloneElement(LineGrid);

            if (MainGrid == null)
            {
                return new Grid();
            }

            EspReader EspInstance = null;

            if (Mod != null)
            {
                if (Mod.Type == GameFileType.ESP)
                {
                    EspInstance = Mod.EspReader;
                }
            }

            MainGrid.Height = Height;

            Border MainBorder = (Border)MainGrid.Children[0];
            MainBorder.Tag = Item.Key;

            Grid GetChildGrid = (Grid)MainBorder.Child;

            GetChildGrid.ColumnDefinitions[0].Width = Mod.Win.TransViewHeader.ColumnDefinitions[0].Width;
            GetChildGrid.ColumnDefinitions[1].Width = Mod.Win.TransViewHeader.ColumnDefinitions[1].Width;
            GetChildGrid.ColumnDefinitions[2].Width = Mod.Win.TransViewHeader.ColumnDefinitions[2].Width;
            GetChildGrid.ColumnDefinitions[3].Width = Mod.Win.TransViewHeader.ColumnDefinitions[3].Width;

            StackPanel GetStackPanel = (StackPanel)((Grid)GetChildGrid.Children[0]).Children[0];

            Ellipse State = (Ellipse)GetStackPanel.Children[0];

            if (IsModify || RecordModifyStates.Contains(Item.Key))
            {
                State.Fill = ModifiedStateBrush;
            }

            TextBox GetKey = (TextBox)GetStackPanel.Children[1];

            GetKey.Text = Item.Key;

            if (FontColor == Colors.White)
            {
                FontColor = (Color)Application.Current.Resources["DefFontColor"];
            }
            GetKey.Foreground = new SolidColorBrush(FontColor);

            Grid GetTypeGrid = (Grid)GetChildGrid.Children[1];
            StackPanel GetTypePanel = (StackPanel)GetTypeGrid.Children[0];
            TextBox GetType = GetTypePanel.Children[0] as TextBox;

            if (EspInstance != null)
            {
                if (EspInstance.Records.ContainsKey(Item.Key))
                {
                    GetType.Text = EspInstance.Records[Item.Key].ParentSig + " " + EspInstance.Records[Item.Key].ChildSig;
                }
            }
            else
            {
                GetType.Text = Item.Type;
            }

            if (FontColor == Colors.White)
            {
                FontColor = (Color)Application.Current.Resources["DefFontColor"];
            }

            GetType.Foreground = new SolidColorBrush(FontColor);

            GetKey.Foreground = new SolidColorBrush(FontColor);

            GetKey.PreviewMouseWheel += OnePreviewMouseWheel;


            if (Mod.P_Translator != null)
            {
                var BatchCore = Mod.P_Translator.GetBatchCore();
                if (BatchCore != null)
                {
                    if (BatchCore.Container != null)
                    {
                        if (BatchCore.Container.Heads.ContainsKey(Item.Key))
                        {
                            Grid GetLeader = (Grid)(GetTypePanel).Children[1];
                            GetLeader.Visibility = Visibility.Visible;
                        }
                    }
                }

            }

            Grid GetOriginalGrid = (Grid)GetChildGrid.Children[2];
            TextBox GetOriginal = (TextBox)GetOriginalGrid.Children[0];
            GetOriginal.Text = Item.Original;

            if (FontColor == Colors.White)
            {
                FontColor = (Color)Application.Current.Resources["DefFontColor"];
            }
            GetOriginal.Foreground = new SolidColorBrush(FontColor);

            GetOriginal.PreviewMouseWheel += OnePreviewMouseWheel;

            Grid GetTranslatedGrid = (Grid)GetChildGrid.Children[3];
            Border GetTranslatedBorder = (Border)GetTranslatedGrid.Children[0];

            Grid GetColorGrid = (Grid)GetTranslatedGrid.Children[1];

            ((Border)GetColorGrid.Children[0]).Tag = Mod;
            ((Border)GetColorGrid.Children[1]).Tag = Mod;
            ((Border)GetColorGrid.Children[2]).Tag = Mod;

            ((Border)GetColorGrid.Children[0]).PreviewMouseDown += ChangeColor;
            ((Border)GetColorGrid.Children[1]).PreviewMouseDown += ChangeColor;
            ((Border)GetColorGrid.Children[2]).PreviewMouseDown += ChangeColor;

            TextEditor GetTranslated = (TextEditor)(GetTranslatedBorder.Child);

            GetTranslated.TextArea.LeftMargins.Clear();

            GetTranslated.Text = Item.Translated;

            if (FontColor == Colors.White)
            {
                FontColor = (Color)Application.Current.Resources["DefFontColor"];
            }

            GetTranslated.Foreground = new SolidColorBrush(FontColor);

            GetTranslated.PreviewMouseWheel += TextEditorPreviewMouseWheel;

            GetTranslated.MouseLeave += GetTranslated_MouseLeave;
            GetTranslated.LostFocus += GetTranslated_LostFocus;

            GetTranslated.Tag = Item.Key;

            GetTranslated.TextArea.Caret.CaretBrush = Brushes.Orange;

            if (NIMApp.SelfSetting.ViewMode == "Normal")
            {
                MainGrid.Cursor = Cursors.Hand;
                GetKey.Cursor = Cursors.Hand;
                GetOriginal.Cursor = Cursors.Hand;
                GetTranslated.Cursor = Cursors.Hand;
                GetTranslatedBorder.Background = null;
                GetTranslatedBorder.Cursor = null;

                GetTranslated.IsReadOnly = true;
                GetTranslated.VerticalContentAlignment = VerticalAlignment.Center;

                GetTranslated.TextArea.Caret.Hide();
                GetTranslated.IsHitTestVisible = false;
                GetTranslated.TextArea.Caret.CaretBrush = Brushes.Transparent;
                GetTranslated.VerticalAlignment = VerticalAlignment.Center;

                ApplyLTROrRtl(GetTranslated);
            }
            else
            {
                MainGrid.Cursor = Cursors.Hand;
                GetKey.Cursor = Cursors.Hand;
                GetOriginal.Cursor = Cursors.Hand;
                GetTranslated.Cursor = Cursors.IBeam;
                GetTranslated.IsReadOnly = false;
                GetTranslated.VerticalContentAlignment = VerticalAlignment.Center;

                ApplyLTROrRtl(GetTranslated);
            }

            if (Item.Score < 0)
            {
                GetKey.Foreground = Brushes.Red;
                GetOriginal.Foreground = Brushes.Red;
                GetTranslated.Foreground = Brushes.Red;
                GetType.Foreground = Brushes.Red;
                GetTranslatedBorder.Visibility = Visibility.Collapsed;
                GetTranslated.IsReadOnly = true;
            }

            return MainGrid;
        }

        private void GetTranslated_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveText((TextEditor)sender);
        }

        void ApplyLTROrRtl(TextEditor Box)
        {
            Box.HorizontalAlignment = HorizontalAlignment.Stretch;
            Box.VerticalContentAlignment = VerticalAlignment.Center;

            if (NIMApp.SelfSetting.TextDisplay == UIManage.TextLayout.RTL)
            {
                Box.FlowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                Box.FlowDirection = FlowDirection.LeftToRight;
            }
        }

        private void GetTranslated_MouseLeave(object sender, MouseEventArgs e)
        {
            SaveText((TextEditor)sender);
        }

        public void SaveText(TextEditor RTB)
        {
            // Skip if In Normal View Mode Or Working Window / TransViewList Is Null
            if (NIMApp.SelfSetting.ViewMode == "Normal" ||
                NIMApp.WorkWin == null ||
                NIMApp.WorkWin.ActiveTab == null) return;
            try
            {
                string OriginalText = RTB.Text;

                // Get Key And Target Grid
                string Key = P_Convert.ObjToStr(RTB.Tag);
                var Target = NIMApp.WorkWin.ActiveTab.TransListView.KeyToFakeGrid(Key);

                // Update Translation Data And History Cache
                if (Target != null)
                {
                    NIMApp.WorkWin.ActiveTab.Mod.P_Translator.AutoSetLink(Key, Target.Source,new P_String(OriginalText,1));

                    bool IsCloud = false;
                    Target.SyncData(NIMApp.WorkWin.ActiveTab.Mod, ref IsCloud);

                    TranslateView.CurrentHistory?.RefreshData();
                    NIMApp.WorkWin.ActiveTab.ReSetHistoryPointer();
                }

                // Apply LTR Or RTL Layout
                ApplyLTROrRtl(RTB);
            }
            finally
            {

            }
        }



        public void OnePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var TextBox = sender as TextBox;
            var Parent = VisualTreeHelper.GetParent(TextBox);

            while (Parent != null && !(Parent is ScrollViewer))
            {
                Parent = VisualTreeHelper.GetParent(Parent);
            }

            if (Parent is ScrollViewer ScrollViewer)
            {
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        public void TextEditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var TextBox = sender as TextEditor;
            var Parent = VisualTreeHelper.GetParent(TextBox);

            while (Parent != null && !(Parent is ScrollViewer))
            {
                Parent = VisualTreeHelper.GetParent(Parent);
            }

            if (Parent is ScrollViewer ScrollViewer)
            {
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        public void ChangeColor(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border)
            {
                Border ButtonHandle = (Border)sender;
                ModFile GetMod = ButtonHandle.Tag as ModFile;
                Color GetColor = ((SolidColorBrush)ButtonHandle.Background).Color;

                if (GetMod.ListView != null)
                {
                    GetMod.ListView.ChangeFontColor(GetMod.P_Translator.GetFileUniqueKey(), GetColor.R, GetColor.G, GetColor.B);
                }
            }
        }
    }
}