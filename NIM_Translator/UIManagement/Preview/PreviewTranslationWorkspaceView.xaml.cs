using System;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Decodes a validated bounded asset snapshot for WPF display without retaining its stream.
    /// </summary>
    public sealed class PreviewAssetImageConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var asset = value as PreviewAssetContext;
            if (asset == null || asset.Content.Length == 0)
            {
                return null;
            }

            using (var stream = new MemoryStream(asset.Content, false))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 512;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Presents the keyboard-first preview translation workspace.
    /// </summary>
    public partial class PreviewTranslationWorkspaceView : UserControl
    {
        /// <summary>
        /// Creates the preview translation workspace view.
        /// </summary>
        public PreviewTranslationWorkspaceView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Moves keyboard focus to the workflow search field after shell navigation.
        /// </summary>
        internal void FocusInitialControl()
        {
            SearchBox.Focus();
        }

        private void CodeResultSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (DataContext as PreviewTranslationWorkspaceViewModel)?.Inspectors;
            PreviewCodeSearchResult result = viewModel?.SelectedCodeResult;
            if (result == null || result.LineNumber <= 0 || result.LineNumber > CodeTextBox.LineCount)
            {
                return;
            }

            int lineIndex = result.LineNumber - 1;
            int start = CodeTextBox.GetCharacterIndexFromLineIndex(lineIndex);
            int length = CodeTextBox.GetLineLength(lineIndex);
            CodeTextBox.Select(start, length);
            CodeTextBox.ScrollToLine(lineIndex);
            CodeTextBox.Focus();
        }
    }
}
