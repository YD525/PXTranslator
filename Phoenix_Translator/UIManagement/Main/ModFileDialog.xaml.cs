using System;
using System.Collections.Generic;
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

            var Button = PathBox.Template.FindName("IsSelectPathBtn", PathBox) as ToggleButton;

            if (Button != null)
            {
                Button.Click += SetPath_Click;
            }
        }

        public ModStringSearcher StringSearcher = new ModStringSearcher();

        public List<SkyrimEntry> CurrentEntries = null;
        private void SetPath_Click(object sender, RoutedEventArgs e)
        {
            CurrentEntries = StringSearcher.ScanMods(PathBox.Text);

            UPDateMods(CurrentEntries);
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
                        ModView.AddRow(UIHelper.CreateEntryLine(EntryBlocks).ToArray());
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
                    ModView.AddRow(UIHelper.CreateEntryLine(EntryBlocks).ToArray());
                }));
            }

        }

        private void Path_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void SearchStr_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
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

        private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
