using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Renders one owner-aware NIM modal interaction with explicit semantic severity.
    /// </summary>
    public partial class PreviewDialogWindow : Window
    {
        /// <summary>
        /// Creates a visual dialog for a complete user-safe request.
        /// </summary>
        /// <param name="request">The localized modal interaction.</param>
        internal PreviewDialogWindow(PreviewDialogRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            InitializeComponent();
            Title = request.Title;
            DialogTitle.Text = request.Title;
            DialogMessage.Text = request.Message;
            SeverityText.Text = GetSeverityText(request.Severity);
            SeverityBorder.BorderBrush = GetSeverityBrush(request.Severity);
            CancelButton.Content = PreviewMessageCatalog.Get("Common_Action_Cancel");
            CancelButton.Visibility = request.RequiresConfirmation ? Visibility.Visible : Visibility.Collapsed;
            AcceptButton.Content = PreviewMessageCatalog.Get(
                request.RequiresConfirmation ? "Common_Action_Confirm" : "Common_Action_OK");
            AcceptButton.Style = (Style)FindResource(
                request.Severity == PreviewDialogSeverity.Destructive
                    ? "PreviewButtonDanger"
                    : "PreviewButtonPrimary");
        }

        private static string GetSeverityText(PreviewDialogSeverity severity)
        {
            switch (severity)
            {
                case PreviewDialogSeverity.Warning:
                case PreviewDialogSeverity.Destructive:
                    return PreviewMessageCatalog.Get("Common_Severity_Warning");
                case PreviewDialogSeverity.Error:
                    return PreviewMessageCatalog.Get("Common_Severity_Error");
                default:
                    return PreviewMessageCatalog.Get("Common_Severity_Information");
            }
        }

        private Brush GetSeverityBrush(PreviewDialogSeverity severity)
        {
            switch (severity)
            {
                case PreviewDialogSeverity.Warning:
                case PreviewDialogSeverity.Destructive:
                    return (Brush)FindResource("PreviewBrushWarning");
                case PreviewDialogSeverity.Error:
                    return (Brush)FindResource("PreviewBrushError");
                default:
                    return (Brush)FindResource("PreviewBrushInformation");
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                MessageScrollViewer.ScrollToTop();
                AcceptButton.Focus();
            }));
        }
    }
}
