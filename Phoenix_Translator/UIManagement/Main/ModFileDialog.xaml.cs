using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PhoenixTranslator.SkyrimManagement;
using PhoenixTranslator.UIManage;
using PhoenixTranslator.UIManagement;

namespace PhoenixTranslator
{
    /// <summary>
    /// Interaction logic for ModFileDialog.xaml
    /// </summary>
    public partial class ModFileDialog : Window
    {
        public static ModFileDialog Instance = new ModFileDialog();

        public BlockListView ModView = null;

        public ModFileDialog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ModView == null)
            {
                ModView = new BlockListView(CModView, 130);

                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
                ModView.ExColStyles.Add(new ExColStyle(1, GridUnitType.Star));
            }

            var Button = PathBox.Template.FindName("IsSelectBtn", PathBox) as ToggleButton;

            if (Button != null)
            {
                Button.Click += ScanPath_Click;
            }

            Button = PathBox.Template.FindName("IsBrowseBtn", PathBox) as ToggleButton;

            if (Button != null)
            {
                Button.Click += ShowPath_Click;
            }

            Button = PathBox.Template.FindName("IsBackBtn", PathBox) as ToggleButton;

            if (Button != null)
            {
                Button.Click += BackPath_Click;
            }

            Button = SearchBox.Template.FindName("IsSearchBtn", SearchBox) as ToggleButton;
            if (Button != null)
            {
                Button.Click += SearchFile_Click;
            }

            Button = SearchBox.Template.FindName("IsClearBtn", SearchBox) as ToggleButton;
            if (Button != null)
            {
                Button.Click += ClearBtn_Click;
            }

            if (PhoenixApp.SelfSetting.LastSetModFolder != null)
            {
                if (PhoenixApp.SelfSetting.LastSetModFolder.Length > 0)
                {
                    if (Directory.Exists(PhoenixApp.SelfSetting.LastSetModFolder))
                    {
                        PathBox.Text = PhoenixApp.SelfSetting.LastSetModFolder;
                        ScanPath();
                    }
                }
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            ScanPath();
        }

        public Dictionary<SkyrimEntry, List<string>> SearchInFo = null;
        private void SearchFile_Click(object sender, RoutedEventArgs e)
        {
             SearchInFo = StringSearcher.SearchStr(CurrentEntries, SearchBox.Text);
             UPDateMods(SearchInFo.Keys.ToList());
        }


        public ModStringSearcher StringSearcher = new ModStringSearcher();
        public List<SkyrimEntry> CurrentEntries = null;
        private void ScanPath_Click(object sender, RoutedEventArgs e)
        {
            ScanPath();
        }
        public void ScanPath()
        {
            CurrentEntries = StringSearcher.ScanMods(PathBox.Text);

            SearchInFo = null;

            UPDateMods(CurrentEntries);
        }

        private void ShowPath_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog Dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                Dialog.Description = "Please select the root directory of the mod.";
                Dialog.ShowNewFolderButton = true;

                if (Dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PathBox.Text =  Dialog.SelectedPath;
                    ScanPath();
                }
            }
        }

        private void BackPath_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableFilesView.Visibility == Visibility.Visible)
            {
                AvailableFilesView.Visibility = Visibility.Collapsed;
                CModView.Visibility = Visibility.Visible;
                return;
            }

            if (Directory.Exists(PathBox.Text) && PathBox.Text.Length > 0)
            {
                DirectoryInfo Parent = Directory.GetParent(PathBox.Text);

                if (Parent != null)
                {
                    string TempPath = PathBox.Text;
                    try 
                    { 
                        PathBox.Text = Parent.FullName;
                        ScanPath();
                    }
                    catch
                    {
                        PathBox.Text = TempPath;
                    }

                }
            }
        }

        public void UPDateMods(List<SkyrimEntry> Entries)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                ModView.Clear();
            }));

            int ColumnLength = 7;

            int CurrentLength = 7;

            List<SkyrimEntry> EntryBlocks = new List<SkyrimEntry>();

            foreach (var GetEntry in Entries)
            {
                if (CurrentLength > 0)
                {
                    CurrentLength--;
                    EntryBlocks.Add(GetEntry);
                }

                if (CurrentLength == 0)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        ModView.AddRow(UIHelper.CreateEntryLine(this,EntryBlocks).ToArray());
                    }));

                    EntryBlocks.Clear();
                    CurrentLength = ColumnLength;
                }
            }

            if (EntryBlocks.Count > 0)
            {
                for (int i = 0; i < CurrentLength; i++)
                {
                    EntryBlocks.Add(new SkyrimEntry());
                }

                this.Dispatcher.Invoke(new Action(() =>
                {
                    ModView.AddRow(UIHelper.CreateEntryLine(this,EntryBlocks).ToArray());
                }));
            }

        }
        public SkyrimEntry CurrentEntry = null;
        public void ShowAvailableFiles(SkyrimEntry Entry, List<string>Files)
        {
            AvailableFilesView.Visibility = Visibility.Visible;
            CModView.Visibility = Visibility.Collapsed;

            AvailableFileList.Items.Clear();

            if (Entry != null)
            {
                CurrentEntry = Entry;

                if (SearchInFo != null)
                {
                    foreach (var File in SearchInFo[Entry])
                    {
                        AvailableFileList.Items.Add(File.Substring(Entry.Path.Length));
                    }
                }
                else
                {
                    foreach (var File in Files)
                    {
                        AvailableFileList.Items.Add(File.Substring(Entry.Path.Length));
                    }
                }
            }
          
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PhoenixApp.SelfSetting.LastSetModFolder = PathBox.Text;
        }

        private void LoadCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableFileList.SelectedItem != null)
            {
                string SelectedFile = AvailableFileList.SelectedItem.ToString();

                PhoenixApp.WorkWin.Dispatcher.Invoke(new Action(() => {
                    PhoenixApp.WorkWin.LoadFile(CurrentEntry.Path + SelectedFile);
                }));

                this.Hide();
            }
        }

        //private void LoadAll_Click(object sender, RoutedEventArgs e)
        //{
        //    foreach (var GetFile in AvailableFileList.Items)
        //    {
        //        PhoenixApp.WorkWin.Dispatcher.Invoke(new Action(() => {
        //            PhoenixApp.WorkWin.LoadFile(CurrentEntry.Path + GetFile.ToString());
        //        }));
        //    }

        //    this.Close();
        //}

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
        }
    }
}
