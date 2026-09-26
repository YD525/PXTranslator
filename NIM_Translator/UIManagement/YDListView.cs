using System.Windows.Controls;
using System.Windows;
using NIM.UIManage;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using NIM.UIManagement;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Linq;
using ICSharpCode.AvalonEdit;
using NIMEngine.Translate;
using NIMEngine.Additional;
using NIMEngine.Common;
using NIM.SkyrimManagement;

public class FakeGrid
{
    public double Height { get; set; } = 0;
    public string Type { get; set; } = "";
    public string Key { get; set; } = "";
    public string Source { get; set; } = "";

    public string RealSource { get; set; } = "";
    public string Translated { get; set; } = "";

    public double Score { get; set; } = 0;

    public FakeGrid(double Height, string Type, string Key, string Source, string Translated, double Score)
    {
        this.Height = Height;
        this.Type = Type;
        this.Key = Key;
        this.Source = Source;
        this.Translated = Translated;
        this.Score = Score;
    }

    public string GetRealSource()
    {
        if (this.RealSource.Length == 0)
        {
            return this.Source;
        }

        return this.RealSource;
    }
    public string GetSource()
    {
        return this.Source;
    }

    public void SyncUI(YDListView ListViewHandle)
    {
        for (int i = 0; i < ListViewHandle.VisibleRows.Count; i++)
        {
            bool CanExit = false;

            if (RowStyleWin.GetKey(ListViewHandle.VisibleRows[i].View).Equals(this.Key))
            {
                ListViewHandle.Parent.Dispatcher.Invoke(new Action(() =>
                {
                    RowStyleWin.SetOriginal(ListViewHandle.VisibleRows[i].View,this.Source);
                    RowStyleWin.SetTranslated(ListViewHandle.VisibleRows[i].View, this.Translated);

                    CanExit = true;
                }));
            }

            if (CanExit)
            {
                break;
            }
        }
    }

    public void SyncData(ModFile Mod,ref bool IsCloud)
    {
        bool FromDictionary = false;

        var FindDictionary = Mod.OriginalDictionaryReader.CheckDictionary(this.Key);

        if (FindDictionary != null)
        {
            if (!string.IsNullOrEmpty(FindDictionary.OriginalText))
            {
                if (this.Source != FindDictionary.OriginalText)
                {
                    this.RealSource = this.Source;
                    this.Source = FindDictionary.OriginalText;
                }

                FromDictionary = true;
            }
        }

        if (!FromDictionary)
        {
            if (!string.IsNullOrEmpty(this.RealSource))
            {
                if (this.Source != this.RealSource)
                {
                    this.Source = this.RealSource;
                }
            }
        }

        IsCloud = false;
        var QueryResult = Mod.P_Translator.QueryTransData(this.Key,this.Type,this.Source, false);

        if (QueryResult != null)
        {
            this.Translated = QueryResult.TransText;
            IsCloud = QueryResult.FromCloud;
        }
    }

    public void SetFontColor(YDListView ListViewHandle, int R, int G, int B)
    {
        for (int i = 0; i < ListViewHandle.VisibleRows.Count; i++)
        {
            if (RowStyleWin.GetKey(ListViewHandle.VisibleRows[i].View).Equals(this.Key))
            {
                RowStyleWin.SetColor(ListViewHandle.VisibleRows[i].View, R, G, B);
                break;
            }
        }
    }
}

public class VisibleGrid
{
    public FakeGrid Data;
    public Grid View;

    public VisibleGrid(FakeGrid Data, Grid View)
    {
        this.Data = Data;
        this.View = View;
    }
}
public class YDListView
{
    public Grid Parent = null;
    private ScrollViewer Scroll = null;
    public Canvas MainCanvas;

    public Thread UpdateTrd = null;

    public List<FakeGrid> RealLines = new List<FakeGrid>();
    public List<VisibleGrid> VisibleRows = new List<VisibleGrid>();

    public int Rows { get { return GetRows(); } }
    public int BufferRows = 3;
    public bool CanSet = true;

    public int SelectLineID = 0;
    public Border LastSelectBorder = null;
    public ColumnDefinition LastSetColorCol = null;

    public bool IsSearchBox = false;

    public Thread SelectLineThread = null;
    private CancellationTokenSource CancelSelectLineThread = null;
    private CancellationToken? CancelToken = null;


    public delegate void LineSelected(string Key);
    public LineSelected LineSelectedEvent = null;
    public void SetSelectLine(Grid MainGrid, bool UPDate)
    {
        if (LastSelectBorder != null)
        {
            LastSelectBorder.BorderBrush = new SolidColorBrush((Color)Application.Current.Resources["LineANormal"]);
        }
        if (LastSetColorCol != null)
        {
            LastSetColorCol.Width = new GridLength(0, GridUnitType.Pixel);
        }

        SelectLineID = P_Convert.ObjToInt(MainGrid.Tag);

        Border MainBorder = (Border)MainGrid.Children[0];

        Grid GetChildGrid = (Grid)MainBorder.Child;

        Grid GetTranslatedGrid = (Grid)GetChildGrid.Children[3];
        TextEditor GetTranslated = (TextEditor)(((Border)GetTranslatedGrid.Children[0]).Child);

        GetTranslatedGrid.ColumnDefinitions[1].Width = new GridLength(15,GridUnitType.Pixel);
        
        if (UPDate)
        {
            GetTranslated.Focus();
            string GetKey = RowStyleWin.GetKey(MainGrid);

            LineSelectedEvent?.Invoke(GetKey);

            //if (DeFine.GlobalLocalSetting.ShowCode && DeFine.WorkingWin.CurrentTransType == 3)
            //{
            //    string AutoValue = KeyToFakeGrid(GetKey).RealSource;

            //    if (DeFine.WorkingWin.PexLinks.ContainsKey(GetKey))
            //    {
            //        if (AutoValue == "")
            //        {
            //            AutoValue = RowStyleWin.GetOriginal(MainGrid);
            //        }
            //    }

            //    RowStyleWin.SelectLineFromIDE(DeFine.WorkingWin.PexLinks[GetKey], AutoValue);
            //}
        }

        MainBorder.BorderBrush = new SolidColorBrush((Color)Application.Current.Resources["LineASelected"]);
        LastSelectBorder = MainBorder;
        LastSetColorCol = GetTranslatedGrid.ColumnDefinitions[1];
    }

    private bool IsGridInViewport(Grid Grid)
    {
        double GridTop = Canvas.GetTop(Grid);
        double GridBottom = GridTop + Grid.ActualHeight;

        double ViewTop = Scroll.VerticalOffset;
        double ViewBottom = ViewTop + Scroll.ViewportHeight;

        return GridTop >= ViewTop && GridBottom <= ViewBottom;
    }

    public void UP()
    {
        if (RealLines.Count == 0)
            return;

        SelectLineID--;
        if (SelectLineID < 0)
            SelectLineID = RealLines.Count - 1;

        var TargetLogicItem = RealLines[SelectLineID];

        Grid MatchGrid = null;
        foreach (var G in VisibleRows)
        {
            if (RowStyleWin.GetKey(G.View).Equals(TargetLogicItem.Key))
            {
                MatchGrid = G.View;
                break;
            }
        }

        if (MatchGrid != null && IsGridInViewport(MatchGrid))
        {
            SetSelectLine(MatchGrid, true);
        }
        else
        {
            double Offset = 0;
            for (int i = 0; i < SelectLineID; i++)
            {
                Offset += RealLines[i].Height;
            }

            Scroll.ScrollToVerticalOffset(Offset);

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                UpdateVisibleRows(true);

                foreach (var G in VisibleRows)
                {
                    if (RowStyleWin.GetKey(G.View).Equals(TargetLogicItem.Key))
                    {
                        SetSelectLine(G.View, true);
                        break;
                    }
                }
            }));
        }
    }

    public void Down()
    {
        if (RealLines.Count == 0)
            return;

        SelectLineID++;
        if (SelectLineID >= RealLines.Count)
        {
            SelectLineID = 0;
        }

        var TargetLogicItem = RealLines[SelectLineID];
        Grid MatchGrid = null;
        foreach (var G in VisibleRows)
        {
            if (RowStyleWin.GetKey(G.View).Equals(TargetLogicItem.Key))
            {
                MatchGrid = G.View;
                break;
            }
        }

        if (MatchGrid != null && IsGridInViewport(MatchGrid))
        {
            SetSelectLine(MatchGrid, true);
        }
        else
        {
            double Offset = 0;
            for (int i = 0; i < SelectLineID; i++)
            {
                Offset += RealLines[i].Height;
            }

            Scroll.ScrollToVerticalOffset(Offset);

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                UpdateVisibleRows(true);

                foreach (var G in VisibleRows)
                {
                    if (RowStyleWin.GetKey(G.View).Equals(TargetLogicItem.Key))
                    {
                        SetSelectLine(G.View, true);
                        break;
                    }
                }
            }));
        }
    }

    private int GetRows()
    {
        return this.RealLines.Count;
    }

    public ModFile FileRef = null;
    public YDListView(ModFile File,Grid Parent)
    {
        this.FileRef = File;
        Style ScrollBarStyle = new Style(typeof(ScrollBar))
        {
            BasedOn = (Style)Application.Current.FindResource("for_scrollbar")
        };
        Parent.Resources.Add(typeof(ScrollBar), ScrollBarStyle);

        this.MainCanvas = new Canvas();
        this.MainCanvas.Background = null;
        this.MainCanvas.VerticalAlignment = VerticalAlignment.Top;
        this.MainCanvas.SnapsToDevicePixels = true;
        this.MainCanvas.UseLayoutRounding = true;
        this.MainCanvas.IsItemsHost = false;
        ScrollViewer OneScroll = new ScrollViewer();
        OneScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        OneScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        OneScroll.Content = MainCanvas;
        OneScroll.ScrollChanged += OnScrollChanged; 

        this.Scroll = OneScroll;

        Parent.Children.Add(OneScroll);
        this.Parent = Parent;

        UpdateTrd = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(100);

                if (CanSet)
                {
                    if (CanUpDate)
                    {
                        try
                        {
                            this.MainCanvas.Dispatcher.Invoke(new Action(() =>
                            {
                                UpdateVisibleRows();
                            }));
                        }
                        catch { }

                        CanUpDate = false;
                    }
                }
            }

        });
        UpdateTrd.Start();
    }

    public Canvas GetMainCanvas()
    {
        return this.MainCanvas;
    }

    public void HotReload()
    {
        CanSet = false;

        MainCanvas.Children.Clear();
        VisibleRows.Clear();

        MainCanvas.Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                UpdateVisibleRows(true);
                CanSet = true;
            })
        );
    }

    public void Clear()
    {
        this.SelectLineID = 0;
        this.CanSet = false;
        this.VisibleRows.Clear();
        this.RealLines.Clear();
        this.MainCanvas.Children.Clear();
        this.MainCanvas.Height = 0;
        this.Scroll.ScrollToTop();
        GC.SuppressFinalize(this);
        GC.Collect();
        this.CanSet = true;
    }

    private bool CanUpDate = false;
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        CanUpDate = true;
    }


    public void UpdateVisibleRows(bool ForceUPDate = false)
    {
        if (ForceUPDate)
        {
            CanUpDate = true;
        }

        if (!CanUpDate)
            return;

        CanUpDate = false;

        double ScrollTop = this.Scroll.VerticalOffset;
        double ScrollHeight = this.Scroll.ExtentHeight;
        double ViewHeight = this.Scroll.ViewportHeight;

        double AccumulatedHeight = 0;
        int FirstVisibleRow = 0;

        for (int I = 0; I < RealLines.Count; I++)
        {
            if (AccumulatedHeight >= ScrollTop)
            {
                FirstVisibleRow = I;
                break;
            }
            AccumulatedHeight += RealLines[I].Height;
        }

        double VisibleHeight = 0;
        int LastVisibleRow = FirstVisibleRow;

        for (int I = FirstVisibleRow; I < RealLines.Count; I++)
        {
            VisibleHeight += RealLines[I].Height;
            if (VisibleHeight >= ViewHeight)
            {
                LastVisibleRow = I;
                break;
            }
        }

        LastVisibleRow += BufferRows;
        if (LastVisibleRow >= GetRows())
            LastVisibleRow = GetRows() - 1;

        int FirstVisibleRowWithBuffer = Math.Max(0, FirstVisibleRow - BufferRows);

        bool IsNearBottom = ScrollTop + ViewHeight >= ScrollHeight - 150;

        HashSet<int> NewVisibleIndices = new HashSet<int>();
        if (IsNearBottom)
        {
            for (int I = FirstVisibleRowWithBuffer; I < RealLines.Count; I++)
                NewVisibleIndices.Add(I);
        }
        else
        {
            for (int I = FirstVisibleRowWithBuffer; I <= LastVisibleRow; I++)
                NewVisibleIndices.Add(I);
        }

        for (int I = MainCanvas.Children.Count - 1; I >= 0; I--)
        {
            UIElement Child = MainCanvas.Children[I];
            if (Child is FrameworkElement Fe && Fe.Tag is int RowIndex && !NewVisibleIndices.Contains(RowIndex))
            {
                MainCanvas.Children.RemoveAt(I);
            }
        }

        double CurrentTop = 0;
        for (int I = 0; I < RealLines.Count; I++)
        {
            var Row = RealLines[I];

            if (NewVisibleIndices.Contains(I))
            {
                bool AlreadyExists = false;
                foreach (UIElement Child in MainCanvas.Children)
                {
                    if (Child is FrameworkElement Fe && Fe.Tag is int TagIndex && TagIndex == I)
                    {
                        AlreadyExists = true;
                        break;
                    }
                }

                if (!AlreadyExists)
                {
                    Grid Grid = UIHelper.CreateLine(FileRef, Row);
                    Grid.Tag = I;
                    try 
                    { 
                        Grid.Width = this.Parent.ActualWidth - 15;
                    }
                    catch { }
                    Grid.PreviewMouseLeftButtonDown += MainGrid_PreviewMouseLeftButtonDown;
                    Canvas.SetTop(Grid, CurrentTop);
                    Canvas.SetLeft(Grid, 0);
                    MainCanvas.Children.Add(Grid);
                    if (I.Equals(SelectLineID))
                    {
                        SetSelectLine(Grid, false);
                    }
                }
            }

            CurrentTop += Row.Height;
        }

        VisibleRows.Clear();
        foreach (UIElement Child in MainCanvas.Children)
        {
            if (Child is Grid G)
                VisibleRows.Add(new VisibleGrid(this.RealLines[(int)G.Tag],G));
        }
    }


    public void QuickRefresh(ModFile Mod)
    {
        this.Parent.Dispatcher.BeginInvoke(new Action(() => {
            for (int i = 0; i < this.VisibleRows.Count; i++)
            {
                bool RefCache = false;
                this.VisibleRows[i].Data.SyncData(Mod,ref RefCache);

                Grid GetGrid = this.VisibleRows[i].View;

                RowStyleWin.SetOriginal(GetGrid, this.VisibleRows[i].Data.Source);
                RowStyleWin.SetTranslated(GetGrid, this.VisibleRows[i].Data.Translated);
            }
        }));
    }

    public object AddLocker = new object();
    public int AddRowR(FakeGrid Item)
    {
        lock (AddLocker)
        {
            if (Item == null) return 0;

            this.RealLines.Add(Item);

            MainCanvas.Height += Item.Height;

            return this.RealLines.Count;
        }
    }

    public object DeleteLocker = new object();
    public void DeleteRow(int Offset)
    {
        lock (DeleteLocker)
        {
            if (Offset < 0 || Offset >= RealLines.Count)
                return;

            // Record The Row To Be Deleted
            var deletedRow = RealLines[Offset];
            string key = deletedRow.Key;
            double removedHeight = deletedRow.Height;

            // Remove Data Row
            RealLines.RemoveAt(Offset);

            // Find The Corresponding UI Element In Canvas
            FrameworkElement deletedElement = null;
            for (int i = 0; i < MainCanvas.Children.Count; i++)
            {
                if (MainCanvas.Children[i] is FrameworkElement fe && fe.Tag is int tag && tag == Offset)
                {
                    deletedElement = fe;
                    break;
                }
            }

            double deletedTop = 0;
            if (deletedElement != null)
            {
                deletedTop = Canvas.GetTop(deletedElement);

                // Unsubscribe Events And Clear Child Elements
                try { deletedElement.PreviewMouseDown -= MainGrid_PreviewMouseLeftButtonDown; } catch { }
                if (deletedElement is Panel p) p.Children.Clear();

                // Remove The Element From Canvas
                MainCanvas.Children.Remove(deletedElement);
            }

            // Adjust Remaining Elements' Tag And Position
            foreach (UIElement child in MainCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is int tag)
                {
                    if (tag > Offset)
                    {
                        // Update Tag Index
                        fe.Tag = tag - 1;

                        // Move Element Up To Fill The Gap
                        double top = Canvas.GetTop(fe);
                        Canvas.SetTop(fe, top - removedHeight);
                    }
                }
            }

            // Update VisibleRows Indexes And Remove The Deleted One
            for (int i = VisibleRows.Count - 1; i >= 0; i--)
            {
                var v = VisibleRows[i];
                if (v.Data == deletedRow)
                {
                    VisibleRows.RemoveAt(i);
                }
                else
                {
                    int index = RealLines.IndexOf(v.Data);
                    if (index >= 0)
                        v.View.Tag = index;
                }
            }

            // Recalculate Canvas Height
            MainCanvas.Height = RealLines.Sum(r => r.Height);

            // Refresh Visible Area Without Clearing The Whole Canvas
            UpdateVisibleRows(true);
        }
    }

    private void MainGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SetSelectLine((Grid)sender, true);
    }

    public void ChangeFontColor(int FileUniqueKey, int R, int G, int B)
    {
        var GetLine = this.RealLines[SelectLineID];
        FontColorFinder.SetColor(FileUniqueKey, GetLine.Key, R, G, B);
        GetLine.SetFontColor(this, R, G, B);
    }

    public string GetSelectedKey()
    {
        if (this.RealLines.Count > this.SelectLineID)
        {
            return this.RealLines[this.SelectLineID].Key;
        }

        return string.Empty;
    }

    public FakeGrid GetSelectedGrid()
    {
        if (this.RealLines.Count > this.SelectLineID)
        {
            return this.RealLines[this.SelectLineID];
        }

        return null;
    }

    public void ScrollTo(int SelectID)
    {
        double Offset = 0;
        for (int i = 0; i < SelectID; i++)
        {
            if (RealLines.Count > i)
            {
                Offset += RealLines[i].Height;
            }
            else
            {
                break;
            }
        }

        Scroll.ScrollToVerticalOffset(Offset);

        UpdateVisibleRows(true);
    }

    public bool IsKeyInViewport(string Key)
    {
        foreach (var Get in this.VisibleRows)
        {
            if (RowStyleWin.GetKey(Get.View).Equals(Key))
            {
                if (IsGridInViewport(Get.View))
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }

    public void SetSelectLineByKey(string Key, bool UPDate)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            foreach (var Get in this.VisibleRows)
            {
                if (RowStyleWin.GetKey(Get.View).Equals(Key))
                {
                    SetSelectLine(Get.View, UPDate);
                    break;
                }
            }
        }));
    }

    public FakeGrid KeyToFakeGrid(string Key)
    {
        for (int i = 0; i < this.RealLines.Count; i++)
        {
            if (this.RealLines[i].Key.Equals(Key))
            {
                return this.RealLines[i];
            }
        }

        return null;
    }

    public int KeyToSelectID(string Key)
    {
        int TempSelectLineID = 0;

        for (int i = 0; i < this.RealLines.Count; i++)
        {
            if (this.RealLines[i].Key.Equals(Key))
            {
                TempSelectLineID = i;
                break;
            }
        }

        return TempSelectLineID;
    }

    public List<FakeGrid> QuickSearch(string Keyword)
    {
        if (string.IsNullOrEmpty(Keyword))
            return new List<FakeGrid>();

        return RealLines
            .Where(Line =>
                (!string.IsNullOrEmpty(Line.Source) &&
                 Line.Source.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                ||
                (!string.IsNullOrEmpty(Line.Translated) &&
                 Line.Translated.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                ||
                (!string.IsNullOrEmpty(Line.Key) &&
                Line.Key.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            )
            .ToList();
    }

    public bool Goto(string Key)
    {
        if (string.IsNullOrEmpty(Key))
            return false;

        int TargetIndex = -1;
        for (int i = 0; i < RealLines.Count; i++)
        {
            if (RealLines[i].Key.Equals(Key))
            {
                TargetIndex = i;
                break;
            }
        }

        if (TargetIndex == -1)
            return false;

        double Offset = 0;
        for (int i = 0; i < TargetIndex; i++)
        {
            Offset += RealLines[i].Height;
        }

        Scroll.ScrollToVerticalOffset(Offset);

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            UpdateVisibleRows(true);

            foreach (var vg in VisibleRows)
            {
                if (RowStyleWin.GetKey(vg.View).Equals(Key))
                {
                    SetSelectLine(vg.View, true);
                    break;
                }
            }

            SelectLineID = TargetIndex;
        }));

        return true;
    }
}

