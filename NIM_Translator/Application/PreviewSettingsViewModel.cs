using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Owns the searchable staged Settings Center, validation, and explicit persistence actions.
    /// </summary>
    internal sealed class PreviewSettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PreviewShellViewModel _shell;
        private readonly IPreviewSettingsStore _store;
        private readonly Func<bool> _confirmReset;
        private readonly Func<bool> _confirmDiscard;
        private readonly Action _openLegacyWorkspace;
        private readonly PreviewWorkflowRolloutViewModel _rollout;
        private PreviewSettingsSnapshot _baseline;
        private PreviewSettingsSnapshot _settings;
        private PreviewSettingsCategoryOption _selectedCategory;
        private PreviewProviderOption _selectedProvider;
        private string _searchText;
        private string _pendingProviderCredential;
        private string _pendingProxyPassword;
        private string _providerTestStatusText;
        private CancellationTokenSource _providerTestCancellation;
        private bool _restoringNavigation;

        /// <summary>
        /// Creates a staged Settings Center coordinator over the legacy persistence boundary.
        /// </summary>
        /// <param name="shell">The shell used for safe operation and notification status.</param>
        /// <param name="store">The settings persistence and provider-test boundary.</param>
        /// <param name="confirmReset">Confirms replacing staged values with defaults.</param>
        /// <param name="confirmDiscard">Confirms discarding unsaved values.</param>
        /// <param name="openLegacyWorkspace">Opens the explicit legacy fallback.</param>
        /// <param name="rollout">Optionally stages independent preview-workflow routing.</param>
        internal PreviewSettingsViewModel(
            PreviewShellViewModel shell,
            IPreviewSettingsStore store,
            Func<bool> confirmReset,
            Func<bool> confirmDiscard,
            Action openLegacyWorkspace,
            PreviewWorkflowRolloutViewModel rollout = null)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _confirmReset = confirmReset ?? throw new ArgumentNullException(nameof(confirmReset));
            _confirmDiscard = confirmDiscard ?? throw new ArgumentNullException(nameof(confirmDiscard));
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));
            _rollout = rollout;
            _searchText = string.Empty;
            _pendingProviderCredential = string.Empty;
            _pendingProxyPassword = string.Empty;
            _providerTestStatusText = string.Empty;

            Categories = CreateCategories();
            VisibleCategories = Categories;
            _selectedCategory = Categories[0];
            ProviderOptions = _store.GetProviders();
            LanguageOptions = _store.GetLanguages();
            DensityOptions = new[] { "Compact", "Comfortable" };
            ValidationMessages = new List<string>();

            SelectCategoryCommand = new PreviewShellCommand(SelectCategory);
            ApplyCommand = new PreviewShellCommand(parameter => Apply(), parameter => CanApply);
            CancelCommand = new PreviewShellCommand(parameter => Cancel(), parameter => IsModified);
            ResetCommand = new PreviewShellCommand(parameter => Reset(), parameter => !IsBusy && !IsTestingProvider);
            TestProviderCommand = new PreviewShellCommand(
                async parameter => await TestProviderAsync(),
                parameter => CanTestProvider);
            CancelProviderTestCommand = new PreviewShellCommand(
                parameter => CancelProviderTest(),
                parameter => IsTestingProvider);
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());
            OpenAdvancedToolsCommand = new PreviewShellCommand(
                parameter => _shell.CurrentDestination = PreviewShellDestination.AdvancedTools);

            _shell.PropertyChanged += ShellPropertyChanged;
            if (_rollout != null)
            {
                _rollout.PropertyChanged += RolloutPropertyChanged;
            }
            Reload();
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when write-only credential controls must clear their in-memory values.
        /// </summary>
        internal event EventHandler SecretsCleared;

        /// <summary>
        /// Occurs when the provider credential control must clear after provider selection changes.
        /// </summary>
        internal event EventHandler ProviderCredentialCleared;

        /// <summary>Gets the complete stable category list.</summary>
        public IReadOnlyList<PreviewSettingsCategoryOption> Categories { get; private set; }

        /// <summary>Gets categories matching the current global search.</summary>
        public IReadOnlyList<PreviewSettingsCategoryOption> VisibleCategories { get; private set; }

        /// <summary>Gets safe provider choices.</summary>
        public IReadOnlyList<PreviewProviderOption> ProviderOptions { get; private set; }

        /// <summary>Gets model candidates for the selected provider.</summary>
        public IReadOnlyList<string> ModelOptions => SelectedProvider?.Models ?? new List<string>();

        /// <summary>Gets supported language names.</summary>
        public IReadOnlyList<string> LanguageOptions { get; private set; }

        /// <summary>Gets supported density preference names.</summary>
        public IReadOnlyList<string> DensityOptions { get; private set; }

        /// <summary>Gets actionable validation messages for staged values.</summary>
        public IReadOnlyList<string> ValidationMessages { get; private set; }

        /// <summary>Gets the staged non-secret settings.</summary>
        public PreviewSettingsSnapshot Settings => _settings;

        /// <summary>Gets independently staged preview-workflow rollout choices.</summary>
        public IReadOnlyList<PreviewWorkflowOption> RolloutOptions => _rollout?.Options ??
            new List<PreviewWorkflowOption>();

        /// <summary>Gets the command that selects an intent category.</summary>
        public ICommand SelectCategoryCommand { get; private set; }

        /// <summary>Gets the command that validates and persists all staged changes.</summary>
        public ICommand ApplyCommand { get; private set; }

        /// <summary>Gets the command that discards all staged changes.</summary>
        public ICommand CancelCommand { get; private set; }

        /// <summary>Gets the command that stages safe defaults after confirmation.</summary>
        public ICommand ResetCommand { get; private set; }

        /// <summary>Gets the command that tests staged provider connectivity without saving.</summary>
        public ICommand TestProviderCommand { get; private set; }

        /// <summary>Gets the command that cancels the active provider connectivity test.</summary>
        public ICommand CancelProviderTestCommand { get; private set; }

        /// <summary>Gets the command that opens the complete legacy settings fallback.</summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>Gets the command that routes data and provider maintenance to Advanced Tools.</summary>
        public ICommand OpenAdvancedToolsCommand { get; private set; }

        /// <summary>Gets or sets the global settings search.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _searchText = normalized;
                OnPropertyChanged();
                RefreshSearch();
            }
        }

        /// <summary>Gets or sets the category displayed in the editor.</summary>
        public PreviewSettingsCategoryOption SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (value == null || ReferenceEquals(_selectedCategory, value))
                {
                    return;
                }

                _selectedCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentDescription));
                RaiseCategoryVisibility();
            }
        }

        /// <summary>Gets or sets the selected provider without exposing its credential.</summary>
        public PreviewProviderOption SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (ReferenceEquals(_selectedProvider, value) || value == null)
                {
                    return;
                }

                CancelProviderTest();
                ClearPendingProviderCredential();
                _selectedProvider = value;
                _settings.ProviderKey = value.Key;
                _settings.HasStoredCredential = value.HasStoredCredential;
                if (value.Models.Count > 0 && !value.Models.Contains(_settings.ProviderModel))
                {
                    _settings.ProviderModel = value.Models[0];
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(ModelOptions));
                OnPropertyChanged(nameof(IsLocalProvider));
                OnPropertyChanged(nameof(IsProviderUnavailable));
                OnPropertyChanged(nameof(ProviderDependencyText));
                ClearProviderTestStatus();
                Validate();
            }
        }

        /// <summary>Gets the localized selected category title.</summary>
        public string CurrentTitle => SelectedCategory?.Label ?? string.Empty;

        /// <summary>Gets the localized selected category description.</summary>
        public string CurrentDescription => SelectedCategory?.Description ?? string.Empty;

        /// <summary>Gets whether staged values differ from the loaded baseline.</summary>
        public bool IsModified => _settings != null && (!_settings.HasSameValues(_baseline) ||
            !string.IsNullOrEmpty(_pendingProviderCredential) || !string.IsNullOrEmpty(_pendingProxyPassword) ||
            _rollout?.IsModified == true);

        /// <summary>Gets whether one or more staged values are invalid.</summary>
        public bool HasValidationErrors => ValidationMessages.Count > 0;

        /// <summary>Gets whether Apply is currently allowed.</summary>
        public bool CanApply => IsModified && !HasValidationErrors && !IsBusy && !IsTestingProvider;

        /// <summary>Gets whether settings persistence is active.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>Gets whether an explicit provider connectivity test is active.</summary>
        public bool IsTestingProvider { get; private set; }

        /// <summary>Gets whether staged provider connectivity can currently be tested.</summary>
        public bool CanTestProvider => HasProviders && !IsBusy && !IsTestingProvider;

        /// <summary>Gets the localized, sanitized result of the latest provider test.</summary>
        public string ProviderTestStatusText => _providerTestStatusText;

        /// <summary>Gets whether the selected provider uses a local endpoint.</summary>
        public bool IsLocalProvider => SelectedProvider?.IsLocal == true;

        /// <summary>Gets whether no provider configuration is available.</summary>
        public bool IsProviderUnavailable => ProviderOptions.Count == 0;

        /// <summary>Gets whether one or more provider configurations are available.</summary>
        public bool HasProviders => ProviderOptions.Count > 0;

        /// <summary>Gets a localized explanation when provider settings are unavailable.</summary>
        public string ProviderDependencyText => IsProviderUnavailable
            ? PreviewMessageCatalog.Get("Settings_Dependency_NoProviders")
            : string.Empty;

        /// <summary>Gets whether General is selected.</summary>
        public bool IsGeneralVisible => IsCategory(PreviewSettingsCategory.General);

        /// <summary>Gets whether Providers is selected.</summary>
        public bool IsProvidersVisible => IsCategory(PreviewSettingsCategory.Providers);

        /// <summary>Gets whether Translation is selected.</summary>
        public bool IsTranslationVisible => IsCategory(PreviewSettingsCategory.Translation);

        /// <summary>Gets whether Files and formats is selected.</summary>
        public bool IsFilesVisible => IsCategory(PreviewSettingsCategory.FilesAndFormats);

        /// <summary>Gets whether History and data is selected.</summary>
        public bool IsHistoryDataVisible => IsCategory(PreviewSettingsCategory.HistoryAndData);

        /// <summary>Gets whether Appearance and accessibility is selected.</summary>
        public bool IsAppearanceVisible => IsCategory(PreviewSettingsCategory.AppearanceAndAccessibility);

        /// <summary>Gets whether Advanced is selected.</summary>
        public bool IsAdvancedVisible => IsCategory(PreviewSettingsCategory.Advanced);

        /// <summary>Gets the localized persistent staged-state label.</summary>
        public string StateText => PreviewMessageCatalog.Get(
            HasValidationErrors ? "Settings_State_Invalid" : IsModified ? "Settings_State_Modified" : "Settings_State_Clean");

        /// <summary>Stages a write-only provider credential without exposing it to WPF binding.</summary>
        /// <param name="credential">The PasswordBox value.</param>
        internal void StageProviderCredential(string credential)
        {
            _pendingProviderCredential = credential ?? string.Empty;
            ClearProviderTestStatus();
            Validate();
        }

        /// <summary>Stages a write-only proxy password without exposing it to WPF binding.</summary>
        /// <param name="password">The PasswordBox value.</param>
        internal void StageProxyPassword(string password)
        {
            _pendingProxyPassword = password ?? string.Empty;
            ClearProviderTestStatus();
            Validate();
        }

        /// <summary>Confirms and discards staged changes before the application closes.</summary>
        /// <returns><see langword="true"/> when closing may continue.</returns>
        internal bool TryDiscardForClose()
        {
            if (!IsModified)
            {
                return true;
            }

            if (!_confirmDiscard())
            {
                return false;
            }

            Reload();
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CancelProviderTest();
            _shell.PropertyChanged -= ShellPropertyChanged;
            if (_rollout != null)
            {
                _rollout.PropertyChanged -= RolloutPropertyChanged;
            }
            DetachSettings();
            ClearPendingSecrets();
        }

        private void Reload()
        {
            DetachSettings();
            ProviderOptions = _store.GetProviders();
            _settings = _store.Load();
            _baseline = _settings.Clone();
            _settings.PropertyChanged += SettingsPropertyChanged;
            _rollout?.Cancel();
            _selectedProvider = ProviderOptions.FirstOrDefault(option => option.Key == _settings.ProviderKey)
                ?? ProviderOptions.FirstOrDefault();
            ClearPendingSecrets();
            Validate();
            OnPropertyChanged(nameof(ProviderOptions));
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SelectedProvider));
            OnPropertyChanged(nameof(ModelOptions));
            OnPropertyChanged(nameof(IsLocalProvider));
            OnPropertyChanged(nameof(IsProviderUnavailable));
            OnPropertyChanged(nameof(HasProviders));
            OnPropertyChanged(nameof(CanTestProvider));
        }

        private void Apply()
        {
            Validate();
            if (!CanApply)
            {
                return;
            }

            IsBusy = true;
            OnPropertyChanged(nameof(IsBusy));
            RaiseCommandAvailability();
            try
            {
                _store.Save(_settings.Clone(), _pendingProviderCredential, _pendingProxyPassword);
                _rollout?.Apply();
                Reload();
                _shell.ShowNotification(PreviewShellNotificationSeverity.Success, "Settings_Apply_Succeeded");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidOperationException || exception is ArgumentException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Error, "Settings_Apply_Failed");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommandAvailability();
            }
        }

        private void Cancel()
        {
            Reload();
            _shell.ShowNotification(PreviewShellNotificationSeverity.Information, "Settings_Cancel_Completed");
        }

        private void Reset()
        {
            if (!_confirmReset())
            {
                return;
            }

            PreviewSettingsSnapshot defaults = new PreviewSettingsSnapshot
            {
                ProviderKey = _settings.ProviderKey,
                ProviderModel = _settings.ProviderModel,
                ProviderEnabled = _settings.ProviderEnabled,
                HasStoredCredential = _settings.HasStoredCredential,
                LocalPortText = "1234",
                SourceLanguage = "English",
                TargetLanguage = "English",
                EnableLanguageDetection = true,
                EnableContext = true,
                ContextLimitText = "200",
                PlaceholderPattern = "<(.*?)>,",
                UiLanguage = "English",
                Density = "Compact",
                GenerateCSharp = true,
                HasStoredProxyPassword = _settings.HasStoredProxyPassword,
                MaxThreadCountText = "2",
                ThrottleRatioText = "0.7",
                ThrottleDelayText = "200"
            };
            ReplaceSettings(defaults);
            ClearPendingSecrets();
            Validate();
        }

        private void ReplaceSettings(PreviewSettingsSnapshot replacement)
        {
            DetachSettings();
            _settings = replacement;
            _settings.PropertyChanged += SettingsPropertyChanged;
            OnPropertyChanged(nameof(Settings));
        }

        private void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ClearProviderTestStatus();
            Validate();
        }

        private void RolloutPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(RolloutOptions));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(CanApply));
            RaiseCommandAvailability();
        }

        private async Task TestProviderAsync()
        {
            string validationMessage = GetProviderTestValidationMessage();
            if (!string.IsNullOrEmpty(validationMessage))
            {
                SetProviderTestStatus(PreviewMessageCatalog.Format(
                    "Settings_Providers_Test_Failed",
                    validationMessage));
                _shell.ShowNotification(
                    PreviewShellNotificationSeverity.Error,
                    "Settings_Providers_Test_Failed",
                    validationMessage);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _providerTestCancellation = cancellation;
            IsTestingProvider = true;
            SetProviderTestStatus(PreviewMessageCatalog.Get("Settings_Providers_Test_Running"));
            OnPropertyChanged(nameof(IsTestingProvider));
            OnPropertyChanged(nameof(CanTestProvider));
            _shell.SetOperation("Settings_Providers_Test_Running", 0);
            RaiseCommandAvailability();
            try
            {
                PreviewProviderTestResult result = await _store.TestProviderAsync(
                    _settings.Clone(),
                    _pendingProviderCredential,
                    _pendingProxyPassword,
                    cancellation.Token);
                if (result.Status == PreviewProviderTestStatus.Succeeded)
                {
                    SetProviderTestStatus(PreviewMessageCatalog.Get("Settings_Providers_Test_Succeeded"));
                    _shell.ShowNotification(
                        PreviewShellNotificationSeverity.Success,
                        "Settings_Providers_Test_Succeeded");
                }
                else
                {
                    string reason = PreviewMessageCatalog.Get(GetProviderTestReasonId(result.Status));
                    SetProviderTestStatus(PreviewMessageCatalog.Format("Settings_Providers_Test_Failed", reason));
                    _shell.ShowNotification(
                        result.Status == PreviewProviderTestStatus.Unsupported
                            ? PreviewShellNotificationSeverity.Information
                            : PreviewShellNotificationSeverity.Error,
                        "Settings_Providers_Test_Failed",
                        reason);
                }
            }
            catch (OperationCanceledException)
            {
                SetProviderTestStatus(PreviewMessageCatalog.Get("Settings_Providers_Test_Cancelled"));
            }
            catch (Exception exception) when (exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                string reason = PreviewMessageCatalog.Get("Settings_Providers_Test_Reason_Unreachable");
                SetProviderTestStatus(PreviewMessageCatalog.Format("Settings_Providers_Test_Failed", reason));
                _shell.ShowNotification(
                    PreviewShellNotificationSeverity.Error,
                    "Settings_Providers_Test_Failed",
                    reason);
            }
            finally
            {
                if (ReferenceEquals(_providerTestCancellation, cancellation))
                {
                    _providerTestCancellation = null;
                }

                cancellation.Dispose();
                IsTestingProvider = false;
                OnPropertyChanged(nameof(IsTestingProvider));
                OnPropertyChanged(nameof(CanTestProvider));
                _shell.CompleteOperation();
                RaiseCommandAvailability();
            }
        }

        private void CancelProviderTest()
        {
            _providerTestCancellation?.Cancel();
        }

        private string GetProviderTestValidationMessage()
        {
            int number;
            if (SelectedProvider == null)
            {
                return PreviewMessageCatalog.Get("Settings_Dependency_NoProviders");
            }

            if (SelectedProvider.IsLocal && !TryInt(_settings.LocalPortText, 1, 65535, out number))
            {
                return PreviewMessageCatalog.Format("Settings_Validation_PortRange", 1, 65535);
            }

            if (SelectedProvider.SupportsConnectivityTest && !SelectedProvider.IsLocal &&
                !_settings.HasStoredCredential && string.IsNullOrWhiteSpace(_pendingProviderCredential))
            {
                return PreviewMessageCatalog.Get("Settings_Validation_CredentialRequired");
            }

            if (_pendingProviderCredential.Length > 4096 || _pendingProxyPassword.Length > 4096)
            {
                return PreviewMessageCatalog.Get("Settings_Validation_CredentialLength");
            }

            Uri uri;
            if (!string.IsNullOrWhiteSpace(_settings.ProxyUrl) &&
                (!Uri.TryCreate(_settings.ProxyUrl, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return PreviewMessageCatalog.Get("Settings_Validation_InvalidEndpoint");
            }

            return string.Empty;
        }

        private static string GetProviderTestReasonId(PreviewProviderTestStatus status)
        {
            switch (status)
            {
                case PreviewProviderTestStatus.AuthenticationFailed:
                    return "Settings_Providers_Test_Reason_Authentication";
                case PreviewProviderTestStatus.TimedOut:
                    return "Settings_Providers_Test_Reason_TimedOut";
                case PreviewProviderTestStatus.Rejected:
                    return "Settings_Providers_Test_Reason_Rejected";
                case PreviewProviderTestStatus.Unsupported:
                    return "Settings_Providers_Test_Reason_Unsupported";
                default:
                    return "Settings_Providers_Test_Reason_Unreachable";
            }
        }

        private void SetProviderTestStatus(string message)
        {
            _providerTestStatusText = message ?? string.Empty;
            OnPropertyChanged(nameof(ProviderTestStatusText));
        }

        private void ClearProviderTestStatus()
        {
            if (!IsTestingProvider && !string.IsNullOrEmpty(_providerTestStatusText))
            {
                SetProviderTestStatus(string.Empty);
            }
        }

        private void Validate()
        {
            var messages = new List<string>();
            int number;
            double ratio;
            if (!TryInt(_settings?.ContextLimitText, 1, 100000, out number))
            {
                messages.Add(PreviewMessageCatalog.Format("Settings_Validation_IntegerRange", 1, 100000));
            }

            if (!TryInt(_settings?.MaxThreadCountText, 1, 256, out number))
            {
                messages.Add(PreviewMessageCatalog.Format("Settings_Validation_ThreadRange", 1, 256));
            }

            if (!TryInt(_settings?.ThrottleDelayText, 0, 600000, out number))
            {
                messages.Add(PreviewMessageCatalog.Format("Settings_Validation_DelayRange", 0, 600000));
            }

            if (!double.TryParse(_settings?.ThrottleRatioText, NumberStyles.Float, CultureInfo.InvariantCulture, out ratio) ||
                ratio < 0 || ratio > 1)
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_ThrottleRatio"));
            }

            if (IsLocalProvider && !TryInt(_settings?.LocalPortText, 1, 65535, out number))
            {
                messages.Add(PreviewMessageCatalog.Format("Settings_Validation_PortRange", 1, 65535));
            }

            if (_settings?.ProviderEnabled == true && SelectedProvider?.SupportsConnectivityTest == true &&
                !SelectedProvider.IsLocal &&
                !_settings.HasStoredCredential && string.IsNullOrWhiteSpace(_pendingProviderCredential))
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_CredentialRequired"));
            }

            if (SelectedProvider?.Models.Count > 0 && string.IsNullOrWhiteSpace(_settings?.ProviderModel))
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_ModelRequired"));
            }

            if (_pendingProviderCredential.Length > 4096 || _pendingProxyPassword.Length > 4096)
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_CredentialLength"));
            }

            Uri uri;
            if (!string.IsNullOrWhiteSpace(_settings?.ProxyUrl) &&
                (!Uri.TryCreate(_settings.ProxyUrl, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_InvalidEndpoint"));
            }

            if (!string.IsNullOrWhiteSpace(_settings?.GamePath) && !Directory.Exists(_settings.GamePath))
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_GamePath"));
            }

            try
            {
                new Regex(_settings?.PlaceholderPattern ?? string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException)
            {
                messages.Add(PreviewMessageCatalog.Get("Settings_Validation_PlaceholderPattern"));
            }

            ValidationMessages = messages;
            OnPropertyChanged(nameof(ValidationMessages));
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(StateText));
            RaiseCommandAvailability();
        }

        private void RefreshSearch()
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                VisibleCategories = Categories;
            }
            else
            {
                VisibleCategories = Categories.Where(category =>
                    Contains(category.Label, _searchText) || Contains(category.Description, _searchText) ||
                    Contains(category.SearchTerms, _searchText)).ToList();
            }

            OnPropertyChanged(nameof(VisibleCategories));
            if (VisibleCategories.Count > 0 && !VisibleCategories.Contains(SelectedCategory))
            {
                SelectedCategory = VisibleCategories[0];
            }
        }

        private void SelectCategory(object parameter)
        {
            PreviewSettingsCategoryOption option = parameter as PreviewSettingsCategoryOption;
            if (option != null)
            {
                SelectedCategory = option;
            }
        }

        private void ShellPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PreviewShellViewModel.CurrentDestination) || _restoringNavigation ||
                _shell.CurrentDestination == PreviewShellDestination.Settings || !IsModified)
            {
                return;
            }

            if (_confirmDiscard())
            {
                Reload();
                return;
            }

            _restoringNavigation = true;
            try
            {
                _shell.CurrentDestination = PreviewShellDestination.Settings;
            }
            finally
            {
                _restoringNavigation = false;
            }
        }

        private void RaiseCategoryVisibility()
        {
            OnPropertyChanged(nameof(IsGeneralVisible));
            OnPropertyChanged(nameof(IsProvidersVisible));
            OnPropertyChanged(nameof(IsTranslationVisible));
            OnPropertyChanged(nameof(IsFilesVisible));
            OnPropertyChanged(nameof(IsHistoryDataVisible));
            OnPropertyChanged(nameof(IsAppearanceVisible));
            OnPropertyChanged(nameof(IsAdvancedVisible));
        }

        private void RaiseCommandAvailability()
        {
            ((PreviewShellCommand)ApplyCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)CancelCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)ResetCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)TestProviderCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)CancelProviderTestCommand).RaiseCanExecuteChanged();
        }

        private void DetachSettings()
        {
            if (_settings != null)
            {
                _settings.PropertyChanged -= SettingsPropertyChanged;
            }
        }

        private void ClearPendingSecrets()
        {
            _pendingProviderCredential = string.Empty;
            _pendingProxyPassword = string.Empty;
            SecretsCleared?.Invoke(this, EventArgs.Empty);
        }

        private void ClearPendingProviderCredential()
        {
            if (string.IsNullOrEmpty(_pendingProviderCredential))
            {
                return;
            }

            _pendingProviderCredential = string.Empty;
            ProviderCredentialCleared?.Invoke(this, EventArgs.Empty);
        }

        private bool IsCategory(PreviewSettingsCategory category)
        {
            return SelectedCategory?.Value == category;
        }

        private static bool TryInt(string text, int minimum, int maximum, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                value >= minimum && value <= maximum;
        }

        private static bool Contains(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static IReadOnlyList<PreviewSettingsCategoryOption> CreateCategories()
        {
            return new[]
            {
                CreateCategory(PreviewSettingsCategory.General, "General", "language defaults startup game"),
                CreateCategory(PreviewSettingsCategory.Providers, "Providers", "api key credential model node local endpoint lm studio cloud"),
                CreateCategory(PreviewSettingsCategory.Translation, "Translation", "ai prompt preset context language detect placeholder punctuation preprocessing"),
                CreateCategory(PreviewSettingsCategory.FilesAndFormats, "FilesAndFormats", "game path esp esm pex assembly papyrus csharp import export"),
                CreateCategory(PreviewSettingsCategory.HistoryAndData, "HistoryAndData", "history cache database dictionary terminology memory retention global search"),
                CreateCategory(PreviewSettingsCategory.AppearanceAndAccessibility, "AppearanceAndAccessibility", "ui theme density compact comfortable rtl accessibility"),
                CreateCategory(PreviewSettingsCategory.Advanced, "Advanced", "proxy pipeline custom provider concurrency threads throttle diagnostics"),
            };
        }

        private static PreviewSettingsCategoryOption CreateCategory(
            PreviewSettingsCategory value,
            string id,
            string searchTerms)
        {
            return new PreviewSettingsCategoryOption(
                value,
                PreviewMessageCatalog.Get("Settings_Category_" + id + "_Title"),
                PreviewMessageCatalog.Get("Settings_Category_" + id + "_Description"),
                searchTerms);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
