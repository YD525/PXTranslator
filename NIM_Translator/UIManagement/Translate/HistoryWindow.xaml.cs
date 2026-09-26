using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PhoenixEngine.Engine.ADO;
using NIM.UIManagement;
using PhoenixEngine.ADO;
using PhoenixEngine.Memory;
using PhoenixEngine.Translate;
using NIM.ModParser;

namespace NIM
{
    public partial class HistoryWindow : Window
    {
        public int FileUniqueKey;
        private List<HistoryRecord> _AllRecords = new List<HistoryRecord>();
        private List<HistoryRecord> _FilteredRecords = new List<HistoryRecord>();
        public HistoryWindow(int FileUniqueKey)
        {
            InitializeComponent();

            this.FileUniqueKey = FileUniqueKey;

            this.Owner = NIMApp.WorkWin;

            if (!this.Resources.Contains("CurrentStatusConverter"))
            {
                this.Resources.Add("CurrentStatusConverter", new CurrentStatusConverter());
            }

            this.PreviewKeyDown += Window_PreviewKeyDown;
        }

        public void UpdateFollowPosition()
        {
            double Gap = 3;

            this.Left = NIMApp.WorkWin.Left;
            this.Top = (NIMApp.WorkWin.Top - this.ActualHeight) - Gap;
            this.Width = NIMApp.WorkWin.Width;
        }

        private void OwnerMainWindow_LocationChanged(object Sender, EventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_SizeChanged(object Sender, SizeChangedEventArgs E)
        {
            UpdateFollowPosition();
        }

        private void Window_Loaded(object Sender, RoutedEventArgs E)
        {
            LoadHistoryData();

            NIMApp.WorkWin.LocationChanged += OwnerMainWindow_LocationChanged;
            NIMApp.WorkWin.SizeChanged += OwnerMainWindow_SizeChanged;
        }

        private void Window_Closing(object Sender, System.ComponentModel.CancelEventArgs E)
        {
            NIMApp.WorkWin.LocationChanged -= OwnerMainWindow_LocationChanged;
            NIMApp.WorkWin.SizeChanged -= OwnerMainWindow_SizeChanged;

            if (TranslateView.CurrentHistory == this)
            {
                TranslateView.CurrentHistory = null;
            } 
        }

        private void Window_PreviewKeyDown(object Sender, KeyEventArgs E)
        {
            if (E.Key == Key.Escape)
                this.Close();
            else if (E.Key == Key.F5)
                RefreshData();
            else if (E.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                E.Handled = true;
            }
        }

        public string GetOriginal(string Key)
        {
            var Tab = NIMApp.WorkWin?.ActiveTab;

            if (Tab?.Mod.Type == SkyrimManagement.GameFileType.ESP)
            {
                if (Tab.Mod.GetRecords<RecordItem>().TryGetValue(Key, out var Record) == true)
                {
                    return Record.String;
                }
            }
            else
            if (Tab?.Mod.Type == SkyrimManagement.GameFileType.PEX)
            {
                if (Tab.Mod.GetRecords<PexStrings>().TryGetValue(Key, out var Record) == true)
                {
                    return Record.Source;
                }
            }
            else
            if (Tab?.Mod.Type == SkyrimManagement.GameFileType.MCM)
            {
                if (Tab.Mod.GetRecords<MCMStrings>().TryGetValue(Key, out var Record) == true)
                {
                    return Record.Source;
                }
            }
            else
            if (Tab?.Mod.Type == SkyrimManagement.GameFileType.XML)
            {
                if (Tab.Mod.GetRecords<XmlStrings>().TryGetValue(Key, out var Record) == true)
                {
                    return Record.Source;
                }
            }

            return string.Empty;
        }

        private void LoadHistoryData()
        {
            try
            {
                StatusText.Text = "Loading history records...";

                var RawItems = HistoryDBCache.GetHistoryItems(
                    FileUniqueKey,
                    (int)NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To
                );

                var List = RawItems
                    .OrderBy(x => x.Rowid)
                    .Select(Item => new HistoryRecord
                    {
                        Rowid = Item.Rowid,
                        Key = Item.Key,
                        Original = GetOriginal(Item.Key),
                        To = Item.To,
                        CurrentText = Item.CurrentText,
                        IsCurrent = Item.IsCurrent,
                        Time = Item.Time,
                        RangeID = Item.RangeID
                    })
                    .ToList();

                _AllRecords = List;

                ApplyFilter();

                StatusText.Text =
                    $"Loaded {_FilteredRecords.Count} records (total: {_AllRecords.Count})";

                if (_FilteredRecords.Any(R => R.IsCurrent == 1))
                {
                    var Current = _FilteredRecords.First(R => R.IsCurrent == 1);
                    ScrollToItem(Current);
                }
            }
            catch (Exception Ex)
            {
                StatusText.Text = $"Error: {Ex.Message}";

                MessageBox.Show(
                    $"Failed to load history:\n{Ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public void RefreshData()
        {
            LoadHistoryData();
        }

        private void ApplyFilter()
        {
            string Keyword = SearchBox.Text?.Trim() ?? string.Empty;

            var Query = _AllRecords.AsEnumerable();

            if (!string.IsNullOrEmpty(Keyword))
            {
                Query = Query.Where(R =>
                    (R.PreviousText?.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (R.CurrentText?.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                );
            }

            _FilteredRecords = Query.ToList();

            HistoryGrid.ItemsSource = null;
            HistoryGrid.ItemsSource = _FilteredRecords;

            if (string.IsNullOrEmpty(Keyword))
                StatusText.Text = $"Loaded {_FilteredRecords.Count} records (total: {_AllRecords.Count})";
            else
                StatusText.Text = $"Found {_FilteredRecords.Count} records matching '{Keyword}' (total: {_AllRecords.Count})";
        }

        private void ScrollToItem(HistoryRecord Item)
        {
            try
            {
                int Index = _FilteredRecords.IndexOf(Item);
                if (Index >= 0)
                {
                    var Row = HistoryGrid.ItemContainerGenerator.ContainerFromIndex(Index) as DataGridRow;
                    if (Row != null)
                        Row.BringIntoView();
                    else
                        HistoryGrid.ScrollIntoView(Item);
                }
            }
            catch { }
        }

        private void SearchBox_TextChanged(object Sender, TextChangedEventArgs E)
        {
            ApplyFilter();
        }

        private void SearchClick(object Sender, MouseButtonEventArgs E)
        {
            ApplyFilter();
        }
        private void RefreshClick(object Sender, MouseButtonEventArgs E)
        {
            RefreshData();
        }

        private void ClearAllClick(object Sender, MouseButtonEventArgs E)
        {
            if (_AllRecords.Count == 0)
            {
                StatusText.Text = "No records to clear.";
                return;
            }

            if (MessageBoxExtend.Show(
                     this,
                     "Confirm Clear All",
                     $"Delete all {_AllRecords.Count} history records?\nThis cannot be undone.",
                     ApplicationLayer.PreviewDialogSeverity.Warning,
                     true
                 ))
            {
                try
                {
                    StatusText.Text = "Clearing all records...";
                    if (HistoryDBCache.ClearHistory(FileUniqueKey))
                    {
                        _AllRecords.Clear();
                        _FilteredRecords.Clear();
                        HistoryGrid.ItemsSource = null;
                        HistoryGrid.ItemsSource = _FilteredRecords;
                        StatusText.Text = "All history records cleared.";
                    }
                    else
                        StatusText.Text = "Failed to clear history.";
                }
                catch (Exception Ex)
                {
                    StatusText.Text = $"Error: {Ex.Message}";
                    MessageBox.Show($"Clear failed:\n{Ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void CloseClick(object Sender, MouseButtonEventArgs E) => this.Close();

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
    
        private DataGridRow _ContextMenuRow;
        private void HistoryGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var Element = e.OriginalSource as DependencyObject;

            var Row = ItemsControl.ContainerFromElement(
                HistoryGrid,
                Element
            ) as DataGridRow;

            if (Row == null)
                return;

            _ContextMenuRow = Row;

            foreach (var Item in HistoryGrid.Items)
            {
                if (HistoryGrid.ItemContainerGenerator.ContainerFromItem(Item) is DataGridRow OldRow)
                {
                    if (Equals(OldRow.Tag, "ContextMenu"))
                        OldRow.Tag = null;
                }
            }

            Row.Tag = "ContextMenu";
        }



        private void GoToMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_ContextMenuRow?.Item is HistoryRecord Record)
            {
                int ID = Record.Rowid;
                var HistoryItem = HistoryDBCache.IDToHistoryItem(this.FileUniqueKey,ID);

                if (HistoryItem != null)
                {
                    NIMApp.WorkWin.ActiveTab.TransListView.Goto(HistoryItem.Key);
                }

                RefreshData();
            }
        }

        private void RestoreMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_ContextMenuRow?.Item is HistoryRecord Record)
            {
                int ID = Record.Rowid;
                var HistoryItem = HistoryDBCache.IDToHistoryItem(this.FileUniqueKey, ID);

                if (HistoryItem != null)
                {
                    NIMApp.WorkWin.ActiveTab.TransListView.Goto(HistoryItem.Key);

                    HistoryDBCache.SelectID(this.FileUniqueKey,ID);

                    var Row = NIMApp.WorkWin.ActiveTab.TransListView.KeyToFakeGrid(HistoryItem.Key);
                    bool IsCloud = false;
                    Row.SyncData(NIMApp.WorkWin.ActiveTab.Mod, ref IsCloud);

                    if (IsCloud)
                    {
                        CloudDBCache.DeleteCache(HistoryItem.FileUniqueKey, HistoryItem.Key, NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To);
                    }
                    else
                    {
                        LocalDBCache.DeleteCache(HistoryItem.FileUniqueKey, HistoryItem.Key, NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To);
                    }

                    string NewText = HistoryItem.CurrentText;
                    NIMApp.WorkWin.ActiveTab.Mod.P_Translator.AutoSetLink(HistoryItem.Key, Row.Source, new P_String(HistoryItem.CurrentText, 0, HistoryItem.RangeID));

                    Row.Translated = NewText;

                    for (int i = 0; i < NIMApp.WorkWin.ActiveTab.TransListView.Rows; i++)
                    {
                        NIMApp.WorkWin.ActiveTab.TransListView.RealLines[i].SyncUI(NIMApp.WorkWin.ActiveTab.TransListView);
                    }

                    RefreshData();
                }
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_ContextMenuRow?.Item is HistoryRecord Record)
            {
                int ID = Record.Rowid;

                var Result = MessageBox.Show(
                    $"Are you sure you want to delete this history record?\n\nRowID: {ID}",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.None
                );

                if (Result != MessageBoxResult.Yes)
                    return;

                HistoryDBCache.DeleteHistory(this.FileUniqueKey, ID);

                RefreshData();
            }
        }
        private void SetAsCurrentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_ContextMenuRow?.Item is HistoryRecord Record)
            {
                int ID = Record.Rowid;

                HistoryDBCache.SelectID(this.FileUniqueKey, ID);
                RefreshData();
            }
        }
    }

    public class HistoryRecord
    {
        public int Rowid { get; set; }
        public string Key { get; set; }

        public string Original { get; set; }
        public int To { get; set; }
        public string PreviousText { get; set; }
        public string CurrentText { get; set; }
        public int IsCurrent { get; set; }
        public DateTime Time { get; set; }
        public string RangeID { get; set; }
    }

    public class CurrentStatusConverter : IValueConverter
    {
        public object Convert(object Value, Type TargetType, object Parameter, System.Globalization.CultureInfo Culture)
        {
            return (Value is int V && V == 1) ? "Current" : "History";
        }

        public object ConvertBack(object Value, Type TargetType, object Parameter, System.Globalization.CultureInfo Culture)
        {
            throw new NotImplementedException();
        }
    }
}