using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NIM.ModParser;
using NIM.SkyrimManagement;
using NIM.UIManagement;

namespace NIM
{
    public class TrackingItem
    {
        public ModFile ModRef;
        public string Key = "";
        public TrackingItem(ModFile ModRef, string Key)
        {
            this.ModRef = ModRef;
            this.Key = Key;
        }
    }

    public partial class RecordTracking : Window
    {
        private static readonly SolidColorBrush CardBackgroundBrush;
        private static readonly SolidColorBrush NameTextBrush;
        private static readonly SolidColorBrush GrayTextBrush;
        private static readonly SolidColorBrush HighlightTextBrush;

        private bool _NpcUserCollapsed = false;
        private bool _RelatedTextUserCollapsed = false;
        private bool _DialogueUserCollapsed = false;

        static RecordTracking()
        {
            CardBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            CardBackgroundBrush.Freeze();

            NameTextBrush = new SolidColorBrush(Color.FromRgb(0xFA, 0xE3, 0x06));
            NameTextBrush.Freeze();

            GrayTextBrush = new SolidColorBrush(Color.FromRgb(0xBF, 0xBF, 0xBF));
            GrayTextBrush.Freeze();

            HighlightTextBrush = new SolidColorBrush(Color.FromRgb(247, 241, 186));
            HighlightTextBrush.Freeze();
        }

        private Window _Owner;

        private bool _NpcExpanded = true;
        private bool _RelatedTextExpanded = true;
        private bool _DialogueExpanded = true;

        public ModFile ModRef = null;

        public RecordTracking(ModFile Mod, Window Owner)
        {
            InitializeComponent();

            _Owner = Owner;
            this.ModRef = Mod;

            this.Owner = _Owner;
            this.Loaded += RecordTracking_Loaded;
            this.Closed += RecordTracking_Closed;

            _Owner.LocationChanged += OwnerMainWindow_LocationChanged;
            _Owner.SizeChanged += OwnerMainWindow_SizeChanged;
            _Owner.StateChanged += OwnerMainWindow_StateChanged;
            _Owner.Closed += OwnerMainWindow_Closed;
        }

        private void RecordTracking_Loaded(object Sender, RoutedEventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_LocationChanged(object Sender, EventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_SizeChanged(object Sender, SizeChangedEventArgs E)
        {
            UpdateFollowPosition();
        }

        public void UpdateAllSectionHeights()
        {
            NpcRow.BeginAnimation(RowDefinition.HeightProperty, null);
            NpcChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            bool hasNpc = NpcListPanel.Children.Count > 0;
            bool NpcOpen = hasNpc && !_NpcUserCollapsed;
            NpcRow.Height = new GridLength(NpcOpen ? 1 : 0, GridUnitType.Star);
            NpcChevronRotate.Angle = NpcOpen ? 0 : 180;
            _NpcExpanded = NpcOpen;

            RelatedTextRow.BeginAnimation(RowDefinition.HeightProperty, null);
            RelatedTextChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            bool hasRelated = RelatedTextListPanel.Children.Count > 0;
            bool RelatedOpen = hasRelated && !_RelatedTextUserCollapsed;
            RelatedTextRow.Height = new GridLength(RelatedOpen ? 1 : 0, GridUnitType.Star);
            RelatedTextChevronRotate.Angle = RelatedOpen ? 0 : 180;
            _RelatedTextExpanded = RelatedOpen;

            DialogueRow.BeginAnimation(RowDefinition.HeightProperty, null);
            DialogueChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            bool hasDialogue = DialogueListPanel.Children.Count > 0;
            bool DialogueOpen = hasDialogue && !_DialogueUserCollapsed;
            DialogueRow.Height = new GridLength(DialogueOpen ? 1 : 0, GridUnitType.Star);
            DialogueChevronRotate.Angle = DialogueOpen ? 0 : 180;
            _DialogueExpanded = DialogueOpen;
        }

        private void OwnerMainWindow_StateChanged(object Sender, EventArgs E)
        {
            if (_Owner.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                UpdateFollowPosition();
            }
        }

        private void OwnerMainWindow_Closed(object Sender, EventArgs E)
        {
            MultiWindowController.TrackingWin = null;
            this.Close();
        }

        private void RecordTracking_Closed(object Sender, EventArgs E)
        {
            _Owner.LocationChanged -= OwnerMainWindow_LocationChanged;
            _Owner.SizeChanged -= OwnerMainWindow_SizeChanged;
            _Owner.StateChanged -= OwnerMainWindow_StateChanged;
            _Owner.Closed -= OwnerMainWindow_Closed;
        }

        private void UpdateFollowPosition()
        {
            double Gap = 3;

            this.Left = _Owner.Left + _Owner.ActualWidth + Gap;
            this.Top = _Owner.Top;
            this.Height = _Owner.ActualHeight;
        }

        //Section collapse / expand

        private void NpcHeader_PreviewMouseDown(object Sender, MouseButtonEventArgs E)
        {
            ToggleSection(NpcContentHost, NpcRow, NpcChevronRotate, ref _NpcExpanded, ref _NpcUserCollapsed);
        }

        private void RelatedTextHeader_PreviewMouseDown(object Sender, MouseButtonEventArgs E)
        {
            ToggleSection(RelatedTextContentHost, RelatedTextRow, RelatedTextChevronRotate, ref _RelatedTextExpanded, ref _RelatedTextUserCollapsed);
        }

        private void DialogueHeader_PreviewMouseDown(object Sender, MouseButtonEventArgs E)
        {
            ToggleSection(DialogueContentHost, DialogueRow, DialogueChevronRotate, ref _DialogueExpanded, ref _DialogueUserCollapsed);
        }

        private void ToggleSection(Border ContentHost, RowDefinition Row, RotateTransform ChevronRotate, ref bool IsExpanded, ref bool UserCollapsed)
        {
            if (IsExpanded)
            {
                CollapseSection(Row, ChevronRotate);
            }
            else
            {
                ExpandSection(Row, ChevronRotate);
            }

            IsExpanded = !IsExpanded;
            UserCollapsed = !IsExpanded;
        }

        private void CollapseSection(RowDefinition Row, RotateTransform ChevronRotate)
        {
            Row.BeginAnimation(RowDefinition.HeightProperty, null);
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);

            GridLengthAnimation HeightAnimation = new GridLengthAnimation();
            HeightAnimation.From = Row.Height;
            HeightAnimation.To = new GridLength(0, GridUnitType.Star);
            HeightAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(220));
            HeightAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            HeightAnimation.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation RotateAnimation = new DoubleAnimation();
            RotateAnimation.To = 180;
            RotateAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(220));
            RotateAnimation.FillBehavior = FillBehavior.HoldEnd;

            Row.BeginAnimation(RowDefinition.HeightProperty, HeightAnimation);
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, RotateAnimation);
        }

        private void ExpandSection(RowDefinition Row, RotateTransform ChevronRotate)
        {
            Row.BeginAnimation(RowDefinition.HeightProperty, null);
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);

            GridLengthAnimation HeightAnimation = new GridLengthAnimation();
            HeightAnimation.From = Row.Height;
            HeightAnimation.To = new GridLength(1, GridUnitType.Star);
            HeightAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(220));
            HeightAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            HeightAnimation.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation RotateAnimation = new DoubleAnimation();
            RotateAnimation.To = 0;
            RotateAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(220));
            RotateAnimation.FillBehavior = FillBehavior.HoldEnd;

            Row.BeginAnimation(RowDefinition.HeightProperty, HeightAnimation);
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, RotateAnimation);
        }

        public void LoadNpcRecord(RecordItem Record, string NpcName, string Gender)
        {
            NpcListPanel.Children.Clear();

            bool HasData = Record != null;

            if (HasData)
            {
                NpcListPanel.Children.Add(BuildNpcCard(Record, NpcName, Gender));
            }
        }

        public void LoadRelatedTextRecords(string CurrentKey, List<RecordItem> Records, CancellationToken Token)
        {
            RelatedTextListPanel.Children.Clear();

            int Count = Records != null ? Records.Count : 0;
            bool HasData = Count > 0;

            Border CurrentCard = null;

            if (HasData)
            {
                for (int i = 0; i < Records.Count; i++)
                {
                    var Card = BuildRelatedTextCard(CurrentKey, Records[i]);
                    RelatedTextListPanel.Children.Add(Card);

                    if (Records[i].UniqueKey == CurrentKey)
                        CurrentCard = Card;
                }
            }

            if (CurrentCard != null)
                ScrollToCurrentCardDeferred(RelatedTextScrollViewer, CurrentCard, Token);
        }

        public void LoadBookRecords(string CurrentKey, ModFile ModRef, List<RecordItem> Records, CancellationToken Token)
        {
            AutoLabName.Content = "Related Book Entries";

            DialogueListPanel.Children.Clear();

            int Count = Records != null ? Records.Count : 0;
            bool HasData = Count > 0;

            Border CurrentCard = null;

            if (HasData)
            {
                foreach (var GetRecord in Records)
                {
                    var Card = BuildRelatedTextCard(CurrentKey, GetRecord);
                    DialogueListPanel.Children.Add(Card);

                    if (GetRecord.UniqueKey == CurrentKey)
                        CurrentCard = Card;
                }
            }

            if (CurrentCard != null)
                ScrollToCurrentCardDeferred(DialogueScrollViewer, CurrentCard, Token);
        }

        public void LoadDialogueRecords(string CurrentKey, ModFile ModRef, List<ManagedDialNode> Records, CancellationToken Token)
        {
            AutoLabName.Content = "Related Dialogue Scenes";

            DialogueListPanel.Children.Clear();

            int Count = Records != null ? Records.Count : 0;
            bool HasData = Count > 0;

            Border CurrentCard = null;

            if (HasData)
            {
                for (int i = 0; i < Records.Count; i++)
                {
                    var GetLine = BuildDialogueCard(CurrentKey, ModRef, Records[i]);
                    if (GetLine != null)
                    {
                        DialogueListPanel.Children.Add(GetLine);

                        if (((TrackingItem)GetLine.Tag).Key == CurrentKey)
                            CurrentCard = GetLine;
                    }
                }
            }

            if (CurrentCard != null)
                ScrollToCurrentCardDeferred(DialogueScrollViewer, CurrentCard, Token);
        }

        //Card builders

        private Border BuildNpcCard(RecordItem Item, string NpcName, string Gender)
        {
            string Source = "";

            var GetFakeGrid = ModRef?.ListView?.KeyToFakeGrid(Item.UniqueKey);

            if (GetFakeGrid == null)
            {
                GetFakeGrid = new FakeGrid(0, Item.ParentSig, Item.UniqueKey, Item.String, "", 0);
                Source = Item.String;
            }
            else
            {
                bool IsCloud = false;
                GetFakeGrid.SyncData(ModRef, ref IsCloud);

                Source = GetFakeGrid.GetSource();
            }

            Border CardBorder = new Border();
            CardBorder.Background = CardBackgroundBrush;  
            CardBorder.CornerRadius = new CornerRadius(6);
            CardBorder.Margin = new Thickness(0, 0, 0, 6);
            CardBorder.Padding = new Thickness(8, 6, 8, 6);

            StackPanel ContentPanel = new StackPanel();
            ContentPanel.Orientation = Orientation.Vertical;

            TextBlock TextLine = new TextBlock();
            TextLine.Text = Source;
            TextLine.Foreground = Brushes.White;
            TextLine.FontSize = 13;
            TextLine.TextWrapping = TextWrapping.Wrap;
            ContentPanel.Children.Add(TextLine);

            StackPanel InfoLine = new StackPanel();
            InfoLine.Orientation = Orientation.Horizontal;
            InfoLine.Margin = new Thickness(0, 4, 0, 0);

            TextBlock NameText = new TextBlock();
            NameText.Text = NpcName;
            NameText.Foreground = NameTextBrush;  
            NameText.FontSize = 12;
            NameText.FontWeight = FontWeights.DemiBold;
            InfoLine.Children.Add(NameText);

            TextBlock GenderText = new TextBlock();
            GenderText.Text = "  (" + Gender + ")";
            GenderText.Foreground = GrayTextBrush; 
            GenderText.FontSize = 12;
            InfoLine.Children.Add(GenderText);

            ContentPanel.Children.Add(InfoLine);
            CardBorder.Child = ContentPanel;

            return CardBorder;
        }

        private Border BuildRelatedTextCard(string CurrentKey, RecordItem Item)
        {
            string Source = "";

            var GetFakeGrid = ModRef?.ListView?.KeyToFakeGrid(Item.UniqueKey);

            if (GetFakeGrid == null)
            {
                GetFakeGrid = new FakeGrid(0, Item.ParentSig, Item.UniqueKey, Item.String, "", 0);
                Source = Item.String;
            }
            else
            {
                bool IsCloud = false;
                GetFakeGrid.SyncData(ModRef, ref IsCloud);

                Source = GetFakeGrid.GetSource();
            }

            Border CardBorder = new Border();
            CardBorder.Background = CardBackgroundBrush;  
            CardBorder.CornerRadius = new CornerRadius(6);
            CardBorder.Margin = new Thickness(0, 0, 0, 6);
            CardBorder.Padding = new Thickness(8, 6, 8, 6);
            CardBorder.Tag = new TrackingItem(ModRef, Item.UniqueKey);
            CardBorder.PreviewMouseDown += AnyCard_PreviewMouseDown;

            StackPanel ContentPanel = new StackPanel();
            ContentPanel.Orientation = Orientation.Vertical;

            TextBox TextLine = new TextBox();
            TextLine.Background = null;
            TextLine.BorderBrush = null;
            TextLine.BorderThickness = new Thickness(0);
            TextLine.IsReadOnly = true;
            TextLine.Foreground = Brushes.White;
            TextLine.FontSize = 13;
            TextLine.TextWrapping = TextWrapping.Wrap;
            TextLine.Cursor = Cursors.Hand;

            if (CurrentKey == Item.UniqueKey)
            {
                TextLine.Foreground = HighlightTextBrush; 
            }

            if (GetFakeGrid.Translated.Length == 0)
            {
                TextLine.Text = Source;
            }
            else
            {
                TextLine.Text = Source + " -> " + GetFakeGrid.Translated;
            }

            ContentPanel.Children.Add(TextLine);

            StackPanel InfoLine = new StackPanel();
            InfoLine.Orientation = Orientation.Horizontal;
            InfoLine.Margin = new Thickness(0, 4, 0, 0);

            TextBlock InFoText = new TextBlock();
            InFoText.Text = "Matched";
            InFoText.Foreground = HighlightTextBrush;  
            InFoText.FontSize = 12;
            InFoText.FontWeight = FontWeights.DemiBold;
            InfoLine.Children.Add(InFoText);

            TextBlock ResponseIdText = new TextBlock();
            ResponseIdText.Text = "  #" + Item.ParentSig + " " + Item.ChildSig;
            ResponseIdText.Foreground = GrayTextBrush;  
            ResponseIdText.FontSize = 12;
            InfoLine.Children.Add(ResponseIdText);

            ContentPanel.Children.Add(InfoLine);
            CardBorder.Child = ContentPanel;

            return CardBorder;
        }

        private Border BuildDialogueCard(string CurrentKey, ModFile ModRef, ManagedDialNode Item)
        {
            if (Item.RecordOffset == -1) return null;
            var GetRecord = ModRef.EspReader.GetRecordItemByOffsets(false, Item.RecordOffset, Item.SubOffset);
            if (GetRecord != null)
            {
                string Source = "";

                var GetFakeGrid = ModRef?.ListView?.KeyToFakeGrid(GetRecord.UniqueKey);

                if (GetFakeGrid == null)
                {
                    GetFakeGrid = new FakeGrid(0, GetRecord.ParentSig, GetRecord.UniqueKey, GetRecord.String, "", 0);
                    Source = GetRecord.String;
                }
                else
                {
                    bool IsCloud = false;
                    GetFakeGrid.SyncData(ModRef, ref IsCloud);

                    Source = GetFakeGrid.GetSource();
                }

                Border CardBorder = new Border();
                CardBorder.Background = CardBackgroundBrush;  
                CardBorder.CornerRadius = new CornerRadius(6);
                CardBorder.Margin = new Thickness(0, 0, 0, 6);
                CardBorder.Padding = new Thickness(8, 6, 8, 6);
                CardBorder.Tag = new TrackingItem(ModRef, GetRecord.UniqueKey);
                CardBorder.PreviewMouseDown += AnyCard_PreviewMouseDown;
                CardBorder.Cursor = Cursors.Hand;

                StackPanel ContentPanel = new StackPanel();
                ContentPanel.Orientation = Orientation.Vertical;

                TextBox TextLine = new TextBox();
                TextLine.Background = null;
                TextLine.BorderBrush = null;
                TextLine.BorderThickness = new Thickness(0);
                TextLine.IsReadOnly = true;
                TextLine.Foreground = Brushes.White;
                TextLine.FontSize = 13;
                TextLine.TextWrapping = TextWrapping.Wrap;
                TextLine.Cursor = Cursors.Hand;

                if (CurrentKey == GetRecord.UniqueKey)
                {
                    TextLine.Foreground = HighlightTextBrush;  
                }

                if (GetFakeGrid.Translated.Length == 0)
                {
                    TextLine.Text = Source;
                }
                else
                {
                    TextLine.Text = Source + " -> " + GetFakeGrid.Translated;
                }

                ContentPanel.Children.Add(TextLine);

                StackPanel InfoLine = new StackPanel();
                InfoLine.Orientation = Orientation.Horizontal;
                InfoLine.Margin = new Thickness(0, 4, 0, 0);

                TextBlock EmotionText = new TextBlock();
                if (Item.EmotionType != 999)
                {
                    EmotionText.Text = EmotionTypeHelper.FromRaw(Item.EmotionType).ToString();
                }
                else
                {
                    if (Item.SubOffset == 0)
                    {
                        EmotionText.Text = "Tittle";
                    }
                }

                EmotionText.Foreground = HighlightTextBrush;  
                EmotionText.FontSize = 12;
                EmotionText.FontWeight = FontWeights.DemiBold;
                InfoLine.Children.Add(EmotionText);

                TextBlock ResponseIdText = new TextBlock();
                ResponseIdText.Text = "  #" + GetRecord.ParentSig + " " + GetRecord.ChildSig;
                ResponseIdText.Foreground = GrayTextBrush;  
                ResponseIdText.FontSize = 12;
                InfoLine.Children.Add(ResponseIdText);

                ContentPanel.Children.Add(InfoLine);
                CardBorder.Child = ContentPanel;

                return CardBorder;
            }

            return null;
        }

        private void AnyCard_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border)
            {
                TrackingItem GetTrack = (TrackingItem)((sender as Border).Tag);
                if (!GetTrack.ModRef.ListView.Goto(GetTrack.Key))
                {
                    GetTrack.ModRef?.Win.SelectSig("ALL", new Action(() =>
                    {
                        GetTrack.ModRef.ListView.Goto(GetTrack.Key);
                    }));
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MultiWindowController.TrackingWin = null;
        }

        private void ScrollIntoViewCentered(ScrollViewer Sv, FrameworkElement Element)
        {
            try
            {
                if (Sv == null || Element == null) return;
                if (!Element.IsDescendantOf(Sv)) return;
                if (Element.ActualHeight == 0 || Sv.ViewportHeight == 0) return;

                GeneralTransform Transform = Element.TransformToAncestor(Sv);
                Point Position = Transform.Transform(new Point(0, 0));

                double CurrentOffset = Sv.VerticalOffset;
                double ElementTop = CurrentOffset + Position.Y;

                double TargetOffset = ElementTop - (Sv.ViewportHeight - Element.ActualHeight) / 2;

                if (TargetOffset < 0) TargetOffset = 0;
                if (TargetOffset > Sv.ScrollableHeight) TargetOffset = Sv.ScrollableHeight;

                Sv.ScrollToVerticalOffset(TargetOffset);
            }
            catch
            {
            }
        }

        private void ScrollToCurrentCardDeferred(ScrollViewer Sv, Border Card, CancellationToken Token)
        {
            if (Sv == null || Card == null) return;
            if (Token.IsCancellationRequested) return;
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

            void TryScroll()
            {
                if (Token.IsCancellationRequested) return;
                try
                {
                    Sv.UpdateLayout();
                    ScrollIntoViewCentered(Sv, Card);
                }
                catch
                {
                }
            }

            DispatcherOperation Op;
            try
            {
                Op = Dispatcher.BeginInvoke(new Action(TryScroll), DispatcherPriority.Loaded);
            }
            catch
            {
                return;
            }

            CancellationTokenRegistration Registration = default;
            Registration = Token.Register(() =>
            {
                try { Op.Abort(); } catch { }
                Registration.Dispose();
            });

            Task.Delay(240, Token).ContinueWith(T =>
            {
                if (T.IsCanceled) return;
                try
                {
                    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                    Dispatcher.Invoke(TryScroll);
                }
                catch
                {
                }
            }, TaskScheduler.Default);
        }

        public void MatchTransItem(string Original, uint StringKey, CancellationToken CancellationToken)
        {
            //MatchView.Dispatcher.Invoke(new Action(() =>
            //{
            //    MatchView.Children.Clear();
            //}));

            //List<string> UniqueResult = new List<string>();
            //List<string> UniqueKeys = new List<string>();

            //var MatchCloudItems = LocalDBCache.MatchLocalItem((int)TranslatorInterface.Instance.To, Original);

            //foreach (var GetMatch in MatchCloudItems)
            //{
            //    if (!UniqueResult.Contains(GetMatch.Result))
            //    {
            //        UniqueResult.Add(GetMatch.Result);
            //        if (!UniqueKeys.Contains(GetMatch.Key))
            //        {
            //            UniqueKeys.Add(GetMatch.Key);
            //            MatchView.Dispatcher.Invoke(new Action(() =>
            //            {
            //                MatchView.Children.Add(UIHelper.CreatMatchLine(
            //                  UniqueKeyHelper.RowidToOriginalKey(GetMatch.FileUniqueKey),//Get Original File Name
            //                  GetMatch.Key,
            //                  GetMatch.Result
            //                  ));
            //            }));
            //        }
            //    }
            //}

            //foreach (var GetMatch in CloudDBCache.MatchCloudItem((int)TranslatorInterface.Instance.To, Original))
            //{
            //    if (!UniqueResult.Contains(GetMatch.Result))
            //    {
            //        UniqueResult.Add(GetMatch.Result);
            //        if (!UniqueKeys.Contains(GetMatch.Key))
            //        {
            //            UniqueKeys.Add(GetMatch.Key);
            //            MatchView.Dispatcher.Invoke(new Action(() =>
            //            {
            //                MatchView.Children.Add(UIHelper.CreatMatchLine(
            //                  UniqueKeyHelper.RowidToOriginalKey(GetMatch.FileUniqueKey),//Get Original File Name
            //                  GetMatch.Key,
            //                  GetMatch.Result
            //                  ));
            //            }));
            //        }
            //    }
            //}


            //Find DL IL Strings
            //if (StringKey != 0)
            //    if (DeFine.WorkingWin.CurrentTransType == 2)
            //    {
            //        if (EspInstance.ToStringsFile != null)
            //        {
            //            if (EspInstance.ToStringsFile.Strings.ContainsKey(StringKey) == true)
            //            {
            //                string AutoFileName = "Strings";
            //                var FindItem = EspInstance.ToStringsFile.Strings[StringKey];

            //                if (FindItem.Type == StringsFileType.DL)
            //                {
            //                    AutoFileName += ".dlstrings";
            //                }
            //                else
            //                if (FindItem.Type == StringsFileType.IL)
            //                {
            //                    AutoFileName += ".ilstrings";
            //                }
            //                else
            //                {
            //                    AutoFileName += ".strings";
            //                }
            //                MatchView.Dispatcher.Invoke(new Action(() =>
            //                {
            //                    MatchView.Children.Add(UIHelper.CreatMatchLine(
            //                    FindItem.Type.ToString(),
            //                    FindItem.ID.ToString(),
            //                    FindItem.Value
            //                    ));
            //                }));
            //            }
            //        }
            //    }
        }
    }
}

