using System.Windows.Controls;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Presents the keyboard-focused review and quality-assurance workflow.
    /// </summary>
    public partial class PreviewReviewQualityView : UserControl
    {
        /// <summary>
        /// Creates the review and quality-assurance view.
        /// </summary>
        public PreviewReviewQualityView()
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
    }
}
