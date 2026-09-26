using System.Windows.Controls;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Presents revision history and explicit project-update decisions.
    /// </summary>
    public partial class PreviewHistoryUpdateView : UserControl
    {
        /// <summary>
        /// Creates the combined history and project-update view.
        /// </summary>
        public PreviewHistoryUpdateView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Moves keyboard focus to the first control for the visible history or update workflow.
        /// </summary>
        internal void FocusInitialControl()
        {
            if (SearchBox.IsVisible)
            {
                SearchBox.Focus();
                return;
            }

            HistoryTimeline.Focus();
        }
    }
}
