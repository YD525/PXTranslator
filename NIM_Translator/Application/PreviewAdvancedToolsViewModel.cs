using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>Coordinates staged, guarded Advanced Tools operations independently from WPF.</summary>
    internal sealed class PreviewAdvancedToolsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IPreviewAdvancedToolsStore _store;
        private readonly Func<bool> _confirmDatabaseMutation;
        private readonly PreviewShellViewModel _shell;
        private PreviewAdvancedToolsPage _selectedPage;
        private PreviewPipelineEntry _selectedPipelineEntry;
        private CancellationTokenSource _testCancellation;
        private string _statusText = string.Empty;
        private bool _isTesting;
        private bool _isTelemetryPaused;
        private string _databaseQuery = "SELECT * FROM AdvancedDictionary";
        private bool _isDatabaseMutationMode;

        internal PreviewAdvancedToolsViewModel(
            IPreviewAdvancedToolsStore store,
            PreviewShellViewModel shell,
            Func<bool> confirmDatabaseMutation)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _confirmDatabaseMutation = confirmDatabaseMutation ?? throw new ArgumentNullException(nameof(confirmDatabaseMutation));
            Pages = new[]
            {
                PreviewAdvancedToolsPage.ProviderPipeline,
                PreviewAdvancedToolsPage.CustomProvider,
                PreviewAdvancedToolsPage.TerminologyDatabase,
                PreviewAdvancedToolsPage.Telemetry,
                PreviewAdvancedToolsPage.Presets
            };
            PipelineEntries = new ObservableCollection<PreviewPipelineEntry>();
            TokenUsage = new ObservableCollection<KeyValuePair<string, long>>();
            DatabaseRows = new ObservableCollection<PreviewDatabaseResultRow>();
            CustomProvider = new PreviewCustomProviderDraft();
            CustomProvider.PropertyChanged += CustomProviderPropertyChanged;
            MoveUpCommand = new PreviewShellCommand(p => MoveSelected(-1), p => CanMove(-1));
            MoveDownCommand = new PreviewShellCommand(p => MoveSelected(1), p => CanMove(1));
            ApplyPipelineCommand = new PreviewShellCommand(p => ApplyPipeline(), p => ValidatePipeline());
            CancelPipelineCommand = new PreviewShellCommand(p => ReloadPipeline());
            TestCustomProviderCommand = new PreviewShellCommand(async p => await TestCustomProviderAsync(), p => CanTestCustomProvider);
            CancelCustomProviderTestCommand = new PreviewShellCommand(p => CancelCustomProviderTest(), p => IsTesting);
            SaveCustomProviderCommand = new PreviewShellCommand(p => SaveCustomProvider(), p => IsCustomProviderValid && !IsTesting);
            OpenDatabaseReadOnlyCommand = new PreviewShellCommand(p => SetDatabaseMode(false));
            OpenDatabaseMutationCommand = new PreviewShellCommand(p => SetDatabaseMode(true));
            ExecuteDatabaseQueryCommand = new PreviewShellCommand(p => ExecuteDatabaseQuery(), p => CanExecuteDatabaseQuery);
            PauseTelemetryCommand = new PreviewShellCommand(p => ToggleTelemetryPause());
            ClearTelemetryCommand = new PreviewShellCommand(p => ClearTelemetry());
            RefreshTelemetryCommand = new PreviewShellCommand(p => RefreshTelemetry(), p => !IsTelemetryPaused);
            ReloadPipeline();
            RefreshTelemetry();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public IReadOnlyList<PreviewAdvancedToolsPage> Pages { get; private set; }
        public ObservableCollection<PreviewPipelineEntry> PipelineEntries { get; private set; }
        public ObservableCollection<KeyValuePair<string, long>> TokenUsage { get; private set; }

        /// <summary>Gets the bounded rows returned by the last database statement.</summary>
        public ObservableCollection<PreviewDatabaseResultRow> DatabaseRows { get; private set; }
        public PreviewCustomProviderDraft CustomProvider { get; private set; }
        public IReadOnlyList<string> ProviderGroups { get; } = new[] { "Cloud AI", "Local AI", "Traditional", "Interactive" };

        public PreviewAdvancedToolsPage SelectedPage
        {
            get => _selectedPage;
            set { if (_selectedPage != value) { _selectedPage = value; OnPropertyChanged(); RaisePageProperties(); } }
        }

        public PreviewPipelineEntry SelectedPipelineEntry
        {
            get => _selectedPipelineEntry;
            set { _selectedPipelineEntry = value; OnPropertyChanged(); RaiseCommands(); }
        }

        public bool IsPipelineVisible => SelectedPage == PreviewAdvancedToolsPage.ProviderPipeline;
        public bool IsCustomProviderVisible => SelectedPage == PreviewAdvancedToolsPage.CustomProvider;
        public bool IsDatabaseVisible => SelectedPage == PreviewAdvancedToolsPage.TerminologyDatabase;
        public bool IsTelemetryVisible => SelectedPage == PreviewAdvancedToolsPage.Telemetry;
        public bool IsPresetsVisible => SelectedPage == PreviewAdvancedToolsPage.Presets;
        public bool IsTesting { get => _isTesting; private set { _isTesting = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTestCustomProvider)); RaiseCommands(); } }
        public bool IsTelemetryPaused { get => _isTelemetryPaused; private set { _isTelemetryPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(TelemetryPauseText)); RaiseCommands(); } }
        public string TelemetryPauseText => IsTelemetryPaused ? PreviewMessageCatalog.Get("Advanced_Telemetry_Resume_Action") : PreviewMessageCatalog.Get("Advanced_Telemetry_Pause_Action");
        public string StatusText { get => _statusText; private set { _statusText = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); } }
        public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);
        public bool IsCustomProviderValid => ValidateCustomProvider(CustomProvider) == null;
        public bool CanTestCustomProvider => IsCustomProviderValid && !IsTesting;

        /// <summary>Gets whether explicitly confirmed mutation statements are currently allowed.</summary>
        public bool IsDatabaseMutationMode
        {
            get => _isDatabaseMutationMode;
            private set
            {
                _isDatabaseMutationMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DatabaseModeText));
                OnPropertyChanged(nameof(CanExecuteDatabaseQuery));
            }
        }

        /// <summary>Gets the localized database access mode.</summary>
        public string DatabaseModeText => PreviewMessageCatalog.Get(
            IsDatabaseMutationMode ? "Advanced_Database_Mode_Mutation" : "Advanced_Database_Mode_ReadOnly");

        /// <summary>Gets or sets the staged database statement.</summary>
        public string DatabaseQuery
        {
            get => _databaseQuery;
            set
            {
                _databaseQuery = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExecuteDatabaseQuery));
                RaiseCommands();
            }
        }

        /// <summary>Gets whether the staged statement can run in the active mode.</summary>
        public bool CanExecuteDatabaseQuery => IsDatabaseMutationMode
            ? !string.IsNullOrWhiteSpace(DatabaseQuery)
            : PreviewDatabaseStatementGuard.IsReadOnly(DatabaseQuery);

        public ICommand MoveUpCommand { get; private set; }
        public ICommand MoveDownCommand { get; private set; }
        public ICommand ApplyPipelineCommand { get; private set; }
        public ICommand CancelPipelineCommand { get; private set; }
        public ICommand TestCustomProviderCommand { get; private set; }
        public ICommand CancelCustomProviderTestCommand { get; private set; }
        public ICommand SaveCustomProviderCommand { get; private set; }
        public ICommand OpenDatabaseReadOnlyCommand { get; private set; }
        public ICommand OpenDatabaseMutationCommand { get; private set; }
        public ICommand ExecuteDatabaseQueryCommand { get; private set; }
        public ICommand PauseTelemetryCommand { get; private set; }
        public ICommand ClearTelemetryCommand { get; private set; }
        public ICommand RefreshTelemetryCommand { get; private set; }
        public ICommand OpenDiagnosticsCommand => _shell.OpenShellServicesCommand;
        public ICommand OpenLegacyProviderToolsCommand => _shell.OpenLegacyWorkspaceCommand;

        internal static string ValidateCustomProvider(PreviewCustomProviderDraft draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Length > 80)
            {
                return "Advanced_Custom_Validation_Name";
            }
            Uri endpoint;
            if (!Uri.TryCreate(draft.Endpoint, UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                return "Advanced_Custom_Validation_Endpoint";
            }
            IPAddress address;
            bool isLocal = string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(endpoint.Host, out address) && IPAddress.IsLoopback(address));
            if (endpoint.Scheme == Uri.UriSchemeHttp && !isLocal)
            {
                return "Advanced_Custom_Validation_Endpoint";
            }
            if (string.IsNullOrWhiteSpace(draft.ResponseField) || draft.ResponseField.Length > 200)
            {
                return "Advanced_Custom_Validation_Response";
            }
            return null;
        }

        private bool ValidatePipeline()
        {
            return PipelineEntries.Count > 0 &&
                PipelineEntries.Select(entry => entry.Key).Distinct().Count() == PipelineEntries.Count;
        }

        private void ReloadPipeline()
        {
            PipelineEntries.Clear();
            foreach (PreviewPipelineEntry entry in _store.LoadPipeline())
            {
                PipelineEntries.Add(entry.Clone());
            }
            SelectedPipelineEntry = PipelineEntries.FirstOrDefault();
            StatusText = PreviewMessageCatalog.Get("Advanced_Pipeline_Cancelled_Status");
        }

        private void ApplyPipeline()
        {
            try
            {
                _store.SavePipeline(PipelineEntries.ToList());
                StatusText = PreviewMessageCatalog.Get("Advanced_Pipeline_Applied_Status");
                _shell.ShowNotification(PreviewShellNotificationSeverity.Success, "Advanced_Pipeline_Applied_Status");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Advanced_Operation_Failed_Status");
            }
        }

        private bool CanMove(int offset)
        {
            int index = SelectedPipelineEntry == null ? -1 : PipelineEntries.IndexOf(SelectedPipelineEntry);
            return index >= 0 && index + offset >= 0 && index + offset < PipelineEntries.Count;
        }

        private void MoveSelected(int offset)
        {
            int index = PipelineEntries.IndexOf(SelectedPipelineEntry);
            if (index < 0 || index + offset < 0 || index + offset >= PipelineEntries.Count) return;
            PipelineEntries.Move(index, index + offset);
            RaiseCommands();
        }

        private async Task TestCustomProviderAsync()
        {
            string error = ValidateCustomProvider(CustomProvider);
            if (error != null) { StatusText = PreviewMessageCatalog.Get(error); return; }
            var cancellation = new CancellationTokenSource();
            _testCancellation = cancellation;
            IsTesting = true;
            StatusText = PreviewMessageCatalog.Get("Advanced_Custom_Test_Running");
            try
            {
                PreviewProviderTestResult result = await _store.TestCustomProviderAsync(CustomProvider.Clone(), cancellation.Token);
                StatusText = PreviewMessageCatalog.Get(result.Status == PreviewProviderTestStatus.Succeeded
                    ? "Advanced_Custom_Test_Succeeded" : "Advanced_Custom_Test_Failed");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { StatusText = PreviewMessageCatalog.Get("Advanced_Custom_Test_Cancelled"); }
            catch (Exception) { StatusText = PreviewMessageCatalog.Get("Advanced_Custom_Test_Failed"); }
            finally
            {
                if (ReferenceEquals(_testCancellation, cancellation)) _testCancellation = null;
                cancellation.Dispose();
                IsTesting = false;
            }
        }

        private void CancelCustomProviderTest() { _testCancellation?.Cancel(); }

        private void SaveCustomProvider()
        {
            string error = ValidateCustomProvider(CustomProvider);
            if (error != null) { StatusText = PreviewMessageCatalog.Get(error); return; }
            try
            {
                _store.SaveCustomProvider(CustomProvider.Clone());
                ReloadPipeline();
                SelectedPage = PreviewAdvancedToolsPage.ProviderPipeline;
                StatusText = PreviewMessageCatalog.Get("Advanced_Custom_Saved_Status");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Advanced_Operation_Failed_Status");
            }
        }

        private void SetDatabaseMode(bool mutation)
        {
            if (mutation && !_confirmDatabaseMutation()) return;
            IsDatabaseMutationMode = mutation;
            StatusText = DatabaseModeText;
            RaiseCommands();
        }

        private void ExecuteDatabaseQuery()
        {
            try
            {
                IReadOnlyList<PreviewDatabaseResultRow> rows = _store.ExecuteDatabaseQuery(
                    DatabaseQuery,
                    IsDatabaseMutationMode);
                DatabaseRows.Clear();
                foreach (PreviewDatabaseResultRow row in rows) DatabaseRows.Add(row);
                StatusText = PreviewMessageCatalog.Format("Advanced_Database_Result", rows.Count);
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Advanced_Database_Failed");
            }
        }

        private void ToggleTelemetryPause()
        {
            IsTelemetryPaused = !IsTelemetryPaused;
            if (!IsTelemetryPaused) RefreshTelemetry();
        }

        private void RefreshTelemetry()
        {
            if (IsTelemetryPaused) return;
            TokenUsage.Clear();
            foreach (KeyValuePair<string, long> item in _store.ReadTokenUsage()) TokenUsage.Add(item);
        }

        private void ClearTelemetry()
        {
            _store.ClearTokenUsage();
            RefreshTelemetry();
            StatusText = PreviewMessageCatalog.Get("Advanced_Telemetry_Cleared_Status");
        }

        private void RaisePageProperties()
        {
            OnPropertyChanged(nameof(IsPipelineVisible)); OnPropertyChanged(nameof(IsCustomProviderVisible));
            OnPropertyChanged(nameof(IsDatabaseVisible)); OnPropertyChanged(nameof(IsTelemetryVisible));
            OnPropertyChanged(nameof(IsPresetsVisible));
        }

        private void RaiseCommands()
        {
            foreach (PreviewShellCommand command in new[] { MoveUpCommand, MoveDownCommand, ApplyPipelineCommand,
                TestCustomProviderCommand, CancelCustomProviderTestCommand, SaveCustomProviderCommand,
                ExecuteDatabaseQueryCommand, RefreshTelemetryCommand }.OfType<PreviewShellCommand>()) command.RaiseCanExecuteChanged();
        }

        private void CustomProviderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsCustomProviderValid));
            OnPropertyChanged(nameof(CanTestCustomProvider));
            RaiseCommands();
        }

        private void OnPropertyChanged([CallerMemberName] string name = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        public void Dispose()
        {
            CustomProvider.PropertyChanged -= CustomProviderPropertyChanged;
            CancelCustomProviderTest();
            _testCancellation?.Dispose();
        }
    }
}
