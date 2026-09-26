using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Presents project opening, safe recent-project metadata, and the active project boundary.
    /// </summary>
    public partial class PreviewProjectHubView : UserControl
    {
        /// <summary>
        /// Creates the preview Project Hub.
        /// </summary>
        public PreviewProjectHubView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Moves keyboard focus to the primary project-opening action after shell navigation.
        /// </summary>
        internal void FocusInitialControl()
        {
            OpenProjectButton.Focus();
        }

        private void ProjectHub_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetDroppedFiles(e).Length == 1 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void ProjectHub_Drop(object sender, DragEventArgs e)
        {
            var viewModel = DataContext as PreviewProjectHubViewModel;
            if (viewModel != null)
            {
                await viewModel.OpenDroppedProjectsAsync(GetDroppedFiles(e));
            }

            e.Handled = true;
        }

        private static string[] GetDroppedFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return new string[0];
            }

            return ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(2)
                .ToArray();
        }
    }
}
