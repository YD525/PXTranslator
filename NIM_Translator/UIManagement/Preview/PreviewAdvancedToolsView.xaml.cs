using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NIM.UIManagement.Preview
{
    /// <summary>Hosts the categorized Advanced Tools workspace.</summary>
    public partial class PreviewAdvancedToolsView : UserControl
    {
        /// <summary>Creates the Advanced Tools view.</summary>
        public PreviewAdvancedToolsView()
        {
            InitializeComponent();
        }

        /// <summary>Moves keyboard focus to the selected Advanced Tools page.</summary>
        public void FocusInitialControl()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                if (PageTabs.ItemContainerGenerator.ContainerFromIndex(PageTabs.SelectedIndex) is ListBoxItem item)
                {
                    Keyboard.Focus(item);
                }
                else
                {
                    Keyboard.Focus(PageTabs);
                }
            }));
        }
    }
}
