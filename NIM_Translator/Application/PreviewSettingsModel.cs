using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies one intent-oriented Settings Center category.
    /// </summary>
    internal enum PreviewSettingsCategory
    {
        General,
        Providers,
        Translation,
        FilesAndFormats,
        HistoryAndData,
        AppearanceAndAccessibility,
        Advanced
    }

    /// <summary>
    /// Describes one searchable Settings Center category.
    /// </summary>
    internal sealed class PreviewSettingsCategoryOption
    {
        internal PreviewSettingsCategoryOption(
            PreviewSettingsCategory value,
            string label,
            string description,
            string searchTerms)
        {
            Value = value;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Description = description ?? string.Empty;
            SearchTerms = searchTerms ?? string.Empty;
        }

        /// <summary>Gets the category identity.</summary>
        public PreviewSettingsCategory Value { get; private set; }

        /// <summary>Gets the localized category label.</summary>
        public string Label { get; private set; }

        /// <summary>Gets the localized category description.</summary>
        public string Description { get; private set; }

        /// <summary>Gets non-visible search terms including legacy terminology.</summary>
        public string SearchTerms { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Describes one configurable translation provider without exposing credentials.
    /// </summary>
    internal sealed class PreviewProviderOption
    {
        internal PreviewProviderOption(
            int key,
            string name,
            bool isLocal,
            bool hasStoredCredential,
            bool supportsConnectivityTest,
            IReadOnlyList<string> models)
        {
            Key = key;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsLocal = isLocal;
            HasStoredCredential = hasStoredCredential;
            SupportsConnectivityTest = supportsConnectivityTest;
            Models = models ?? new List<string>();
        }

        /// <summary>Gets the engine configuration key.</summary>
        public int Key { get; private set; }

        /// <summary>Gets the safe provider display name.</summary>
        public string Name { get; private set; }

        /// <summary>Gets whether the provider uses a local endpoint.</summary>
        public bool IsLocal { get; private set; }

        /// <summary>Gets whether this provider has a stored credential without exposing its value.</summary>
        public bool HasStoredCredential { get; private set; }

        /// <summary>Gets whether the central Settings Center can safely test this provider.</summary>
        public bool SupportsConnectivityTest { get; private set; }

        /// <summary>Gets the provider's configured model candidates.</summary>
        public IReadOnlyList<string> Models { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Identifies a sanitized provider connectivity outcome without carrying response content.
    /// </summary>
    internal enum PreviewProviderTestStatus
    {
        Succeeded,
        AuthenticationFailed,
        Unreachable,
        TimedOut,
        Rejected,
        Unsupported
    }

    /// <summary>
    /// Carries a sanitized provider connectivity outcome across the settings boundary.
    /// </summary>
    internal sealed class PreviewProviderTestResult
    {
        internal PreviewProviderTestResult(PreviewProviderTestStatus status)
        {
            Status = status;
        }

        /// <summary>Gets the sanitized outcome classification.</summary>
        public PreviewProviderTestStatus Status { get; private set; }
    }

    /// <summary>
    /// Holds one editable settings snapshot independently from WPF controls and persistence.
    /// </summary>
    internal sealed class PreviewSettingsSnapshot : INotifyPropertyChanged
    {
        private int _providerKey;
        private string _providerModel = string.Empty;
        private bool _providerEnabled;
        private bool _hasStoredCredential;
        private string _localPortText = "1234";
        private string _sourceLanguage = "English";
        private string _targetLanguage = "English";
        private bool _enableLanguageDetection = true;
        private bool _enableContext = true;
        private string _contextLimitText = "200";
        private string _additionalPrompt = string.Empty;
        private string _placeholderPattern = "<(.*?)>,";
        private string _gamePath = string.Empty;
        private bool _showAssembly;
        private bool _generateCSharp = true;
        private bool _autoUpdateDatabase;
        private bool _enableGlobalSearch;
        private string _uiLanguage = "English";
        private string _density = "Compact";
        private bool _rightToLeft;
        private string _proxyUrl = string.Empty;
        private string _proxyUserName = string.Empty;
        private bool _hasStoredProxyPassword;
        private string _maxThreadCountText = "2";
        private string _throttleRatioText = "0.7";
        private string _throttleDelayText = "200";

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets or sets the selected engine provider key.</summary>
        public int ProviderKey { get => _providerKey; set => Set(ref _providerKey, value); }

        /// <summary>Gets or sets the selected provider model.</summary>
        public string ProviderModel { get => _providerModel; set => Set(ref _providerModel, value ?? string.Empty); }

        /// <summary>Gets or sets whether the selected provider is enabled.</summary>
        public bool ProviderEnabled { get => _providerEnabled; set => Set(ref _providerEnabled, value); }

        /// <summary>Gets or sets whether a credential is already stored without exposing its value.</summary>
        public bool HasStoredCredential { get => _hasStoredCredential; set => Set(ref _hasStoredCredential, value); }

        /// <summary>Gets or sets the local-provider port as staged text.</summary>
        public string LocalPortText { get => _localPortText; set => Set(ref _localPortText, value ?? string.Empty); }

        /// <summary>Gets or sets the default source-language name.</summary>
        public string SourceLanguage { get => _sourceLanguage; set => Set(ref _sourceLanguage, value ?? string.Empty); }

        /// <summary>Gets or sets the default target-language name.</summary>
        public string TargetLanguage { get => _targetLanguage; set => Set(ref _targetLanguage, value ?? string.Empty); }

        /// <summary>Gets or sets whether automatic source-language detection is enabled.</summary>
        public bool EnableLanguageDetection { get => _enableLanguageDetection; set => Set(ref _enableLanguageDetection, value); }

        /// <summary>Gets or sets whether contextual translation is enabled.</summary>
        public bool EnableContext { get => _enableContext; set => Set(ref _enableContext, value); }

        /// <summary>Gets or sets the context character limit as staged text.</summary>
        public string ContextLimitText { get => _contextLimitText; set => Set(ref _contextLimitText, value ?? string.Empty); }

        /// <summary>Gets or sets the optional additional provider prompt.</summary>
        public string AdditionalPrompt { get => _additionalPrompt; set => Set(ref _additionalPrompt, value ?? string.Empty); }

        /// <summary>Gets or sets the protected-placeholder regular expression.</summary>
        public string PlaceholderPattern { get => _placeholderPattern; set => Set(ref _placeholderPattern, value ?? string.Empty); }

        /// <summary>Gets or sets the configured game path.</summary>
        public string GamePath { get => _gamePath; set => Set(ref _gamePath, value ?? string.Empty); }

        /// <summary>Gets or sets whether PEX assembly is visible.</summary>
        public bool ShowAssembly { get => _showAssembly; set => Set(ref _showAssembly, value); }

        /// <summary>Gets or sets whether PEX output uses C# instead of Papyrus.</summary>
        public bool GenerateCSharp { get => _generateCSharp; set => Set(ref _generateCSharp, value); }

        /// <summary>Gets or sets whether imported strings update the database automatically.</summary>
        public bool AutoUpdateDatabase { get => _autoUpdateDatabase; set => Set(ref _autoUpdateDatabase, value); }

        /// <summary>Gets or sets whether translation memory searches across projects.</summary>
        public bool EnableGlobalSearch { get => _enableGlobalSearch; set => Set(ref _enableGlobalSearch, value); }

        /// <summary>Gets or sets the application interface language name.</summary>
        public string UiLanguage { get => _uiLanguage; set => Set(ref _uiLanguage, value ?? string.Empty); }

        /// <summary>Gets or sets the compact or comfortable density name.</summary>
        public string Density { get => _density; set => Set(ref _density, value ?? string.Empty); }

        /// <summary>Gets or sets whether target editors use right-to-left layout.</summary>
        public bool RightToLeft { get => _rightToLeft; set => Set(ref _rightToLeft, value); }

        /// <summary>Gets or sets the optional HTTP or HTTPS proxy address.</summary>
        public string ProxyUrl { get => _proxyUrl; set => Set(ref _proxyUrl, value ?? string.Empty); }

        /// <summary>Gets or sets the proxy user name.</summary>
        public string ProxyUserName { get => _proxyUserName; set => Set(ref _proxyUserName, value ?? string.Empty); }

        /// <summary>Gets or sets whether a proxy password is stored without exposing its value.</summary>
        public bool HasStoredProxyPassword
        {
            get => _hasStoredProxyPassword;
            set => Set(ref _hasStoredProxyPassword, value);
        }

        /// <summary>Gets or sets the worker limit as staged text.</summary>
        public string MaxThreadCountText
        {
            get => _maxThreadCountText;
            set => Set(ref _maxThreadCountText, value ?? string.Empty);
        }

        /// <summary>Gets or sets the throttling ratio as staged text.</summary>
        public string ThrottleRatioText
        {
            get => _throttleRatioText;
            set => Set(ref _throttleRatioText, value ?? string.Empty);
        }

        /// <summary>Gets or sets the throttling delay in milliseconds as staged text.</summary>
        public string ThrottleDelayText
        {
            get => _throttleDelayText;
            set => Set(ref _throttleDelayText, value ?? string.Empty);
        }

        /// <summary>Creates a detached copy for staged editing or comparison.</summary>
        /// <returns>A snapshot with the same non-secret values.</returns>
        internal PreviewSettingsSnapshot Clone()
        {
            return new PreviewSettingsSnapshot
            {
                ProviderKey = ProviderKey,
                ProviderModel = ProviderModel,
                ProviderEnabled = ProviderEnabled,
                HasStoredCredential = HasStoredCredential,
                LocalPortText = LocalPortText,
                SourceLanguage = SourceLanguage,
                TargetLanguage = TargetLanguage,
                EnableLanguageDetection = EnableLanguageDetection,
                EnableContext = EnableContext,
                ContextLimitText = ContextLimitText,
                AdditionalPrompt = AdditionalPrompt,
                PlaceholderPattern = PlaceholderPattern,
                GamePath = GamePath,
                ShowAssembly = ShowAssembly,
                GenerateCSharp = GenerateCSharp,
                AutoUpdateDatabase = AutoUpdateDatabase,
                EnableGlobalSearch = EnableGlobalSearch,
                UiLanguage = UiLanguage,
                Density = Density,
                RightToLeft = RightToLeft,
                ProxyUrl = ProxyUrl,
                ProxyUserName = ProxyUserName,
                HasStoredProxyPassword = HasStoredProxyPassword,
                MaxThreadCountText = MaxThreadCountText,
                ThrottleRatioText = ThrottleRatioText,
                ThrottleDelayText = ThrottleDelayText
            };
        }

        /// <summary>Compares every staged non-secret value with another snapshot.</summary>
        /// <param name="other">The snapshot to compare.</param>
        /// <returns><see langword="true"/> when every value is equal.</returns>
        internal bool HasSameValues(PreviewSettingsSnapshot other)
        {
            return other != null &&
                ProviderKey == other.ProviderKey &&
                ProviderModel == other.ProviderModel &&
                ProviderEnabled == other.ProviderEnabled &&
                HasStoredCredential == other.HasStoredCredential &&
                LocalPortText == other.LocalPortText &&
                SourceLanguage == other.SourceLanguage &&
                TargetLanguage == other.TargetLanguage &&
                EnableLanguageDetection == other.EnableLanguageDetection &&
                EnableContext == other.EnableContext &&
                ContextLimitText == other.ContextLimitText &&
                AdditionalPrompt == other.AdditionalPrompt &&
                PlaceholderPattern == other.PlaceholderPattern &&
                GamePath == other.GamePath &&
                ShowAssembly == other.ShowAssembly &&
                GenerateCSharp == other.GenerateCSharp &&
                AutoUpdateDatabase == other.AutoUpdateDatabase &&
                EnableGlobalSearch == other.EnableGlobalSearch &&
                UiLanguage == other.UiLanguage &&
                Density == other.Density &&
                RightToLeft == other.RightToLeft &&
                ProxyUrl == other.ProxyUrl &&
                ProxyUserName == other.ProxyUserName &&
                HasStoredProxyPassword == other.HasStoredProxyPassword &&
                MaxThreadCountText == other.MaxThreadCountText &&
                ThrottleRatioText == other.ThrottleRatioText &&
                ThrottleDelayText == other.ThrottleDelayText;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Defines the persistence boundary used by the staged Settings Center.
    /// </summary>
    internal interface IPreviewSettingsStore
    {
        /// <summary>Gets safe provider choices without credential values.</summary>
        IReadOnlyList<PreviewProviderOption> GetProviders();

        /// <summary>Gets supported language names without UI dependencies.</summary>
        IReadOnlyList<string> GetLanguages();

        /// <summary>Loads a detached non-secret settings snapshot.</summary>
        PreviewSettingsSnapshot Load();

        /// <summary>Persists one validated snapshot and optional write-only credential replacements.</summary>
        /// <param name="settings">The validated staged settings.</param>
        /// <param name="providerCredential">The optional new provider credential.</param>
        /// <param name="proxyPassword">The optional new proxy password.</param>
        void Save(PreviewSettingsSnapshot settings, string providerCredential, string proxyPassword);

        /// <summary>
        /// Tests staged provider connectivity without persisting settings or sending translation content.
        /// </summary>
        /// <param name="settings">The staged non-secret provider and proxy settings.</param>
        /// <param name="providerCredential">The optional write-only staged provider credential.</param>
        /// <param name="proxyPassword">The optional write-only staged proxy password.</param>
        /// <param name="cancellationToken">Cancels the bounded connectivity request.</param>
        /// <returns>A sanitized result that excludes response content, endpoints, and credentials.</returns>
        Task<PreviewProviderTestResult> TestProviderAsync(
            PreviewSettingsSnapshot settings,
            string providerCredential,
            string proxyPassword,
            CancellationToken cancellationToken);
    }
}
