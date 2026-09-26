using System;
using System.Windows;
using System.Windows.Controls;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Hosts About, diagnostics, credits, and license routes inside the preview shell.
    /// </summary>
    public partial class PreviewShellServicesView : UserControl
    {
        /// <summary>Creates the shell services view.</summary>
        public PreviewShellServicesView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Occurs when the user requests returning to the active workflow.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>Moves keyboard focus to the first shell-services control.</summary>
        public void FocusInitialControl()
        {
            ServicesTabs.Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
