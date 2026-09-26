using System.Windows;
using System.Windows.Controls;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Presents the unified searchable Settings Center.
    /// </summary>
    public partial class PreviewSettingsView : UserControl
    {
        /// <summary>
        /// Creates the Settings Center view.
        /// </summary>
        public PreviewSettingsView()
        {
            InitializeComponent();
            DataContextChanged += PreviewSettingsViewDataContextChanged;
        }

        /// <summary>
        /// Moves keyboard focus to the global settings search field after shell navigation.
        /// </summary>
        internal void FocusInitialControl()
        {
            SearchBox.Focus();
        }

        private void PreviewSettingsViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var previous = e.OldValue as PreviewSettingsViewModel;
            if (previous != null)
            {
                previous.SecretsCleared -= ViewModelSecretsCleared;
                previous.ProviderCredentialCleared -= ViewModelProviderCredentialCleared;
            }

            var current = e.NewValue as PreviewSettingsViewModel;
            if (current != null)
            {
                current.SecretsCleared += ViewModelSecretsCleared;
                current.ProviderCredentialCleared += ViewModelProviderCredentialCleared;
            }

            ClearSecrets();
        }

        private void ViewModelSecretsCleared(object sender, System.EventArgs e)
        {
            ClearSecrets();
        }

        private void ViewModelProviderCredentialCleared(object sender, System.EventArgs e)
        {
            ProviderCredential.Clear();
        }

        private void ClearSecrets()
        {
            ProviderCredential.Clear();
            ProxyPassword.Clear();
        }

        private void ProviderCredential_PasswordChanged(object sender, RoutedEventArgs e)
        {
            (DataContext as PreviewSettingsViewModel)?.StageProviderCredential(ProviderCredential.Password);
        }

        private void ProxyPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            (DataContext as PreviewSettingsViewModel)?.StageProxyPassword(ProxyPassword.Password);
        }
    }
}
