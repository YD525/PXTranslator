using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies the normalized entry scope used by a workspace tool.
    /// </summary>
    internal enum PreviewWorkspaceToolScope
    {
        /// <summary>Uses only the selected entry.</summary>
        Current,
        /// <summary>Uses entries visible through the current workspace filters.</summary>
        Visible,
        /// <summary>Uses every normalized project entry.</summary>
        All
    }

    /// <summary>
    /// Describes one selectable workspace-tool scope.
    /// </summary>
    internal sealed class PreviewWorkspaceToolScopeOption
    {
        /// <summary>Creates a localized selectable scope.</summary>
        /// <param name="value">The scope behavior.</param>
        /// <param name="label">The localized visible label.</param>
        internal PreviewWorkspaceToolScopeOption(PreviewWorkspaceToolScope value, string label)
        {
            Value = value;
            Label = label;
        }

        /// <summary>Gets the scope behavior.</summary>
        public PreviewWorkspaceToolScope Value { get; private set; }

        /// <summary>Gets the localized visible label.</summary>
        public string Label { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Represents an editable project terminology pair.
    /// </summary>
    internal sealed class PreviewTerminologyEntry : INotifyPropertyChanged
    {
        private string _source;
        private string _target;

        /// <summary>Creates an editable terminology pair.</summary>
        /// <param name="source">The source term.</param>
        /// <param name="target">The preferred target term.</param>
        internal PreviewTerminologyEntry(string source, string target)
        {
            _source = source ?? string.Empty;
            _target = target ?? string.Empty;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets or sets the source terminology text.</summary>
        public string Source
        {
            get => _source;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_source, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _source = normalized;
                OnPropertyChanged();
            }
        }

        /// <summary>Gets or sets the preferred target terminology text.</summary>
        public string Target
        {
            get => _target;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_target, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _target = normalized;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Owns preview, undo, terminology, table, export, and interactive-provider tools for the workspace.
    /// </summary>
    internal sealed class PreviewWorkspaceToolsViewModel : INotifyPropertyChanged
    {
        private readonly Func<IReadOnlyList<PreviewTranslationEntry>> _getAllEntries;
        private readonly Func<IReadOnlyList<PreviewTranslationEntry>> _getVisibleEntries;
        private readonly Func<PreviewTranslationEntry> _getSelectedEntry;
        private readonly Action _notifyEntriesChanged;
        private readonly Func<string> _chooseImportPath;
        private readonly Func<string> _chooseTableExportPath;
        private readonly Func<string> _chooseProjectExportPath;
        private readonly Func<string, CancellationToken, Task> _exportProject;
        private readonly Func<string, CancellationToken, string> _convertToTraditional;
        private readonly Action<string> _copyText;
        private readonly Action<bool> _busyChanged;
        private readonly PreviewTranslationTableService _tableService;
        private readonly PreviewRamCacheService _ramCacheService;
        private readonly Func<string> _chooseRamCacheImportPath;
        private readonly Func<string> _chooseRamCacheExportPath;
        private readonly Func<bool, bool, CancellationToken, Task> _clearTranslationCaches;
        private readonly Func<bool, bool, bool> _confirmCacheClear;
        private readonly PreviewInteractiveExchangeService _interactiveService;
        private Dictionary<PreviewTranslationEntry, string> _undoTargets;
        private Dictionary<PreviewTranslationEntry, string> _pendingConversion;
        private string _findText;
        private string _replacementText;
        private string _terminologySearch;
        private string _interactiveRequest;
        private string _interactiveResponse;
        private string _interactiveRequestId;
        private string _statusText;
        private int _previewCount;
        private bool _isBusy;
        private PreviewWorkspaceToolScopeOption _selectedScope;
        private PreviewTerminologyEntry _selectedTerm;
        private CancellationTokenSource _cancellation;
        private bool _clearProviderCache = true;
        private bool _clearUserCache = true;

        /// <summary>Creates tools over the shared normalized workspace state and injected UI boundaries.</summary>
        /// <param name="getAllEntries">Gets the complete project entry set.</param>
        /// <param name="getVisibleEntries">Gets the current filtered entry set.</param>
        /// <param name="getSelectedEntry">Gets the selected entry.</param>
        /// <param name="notifyEntriesChanged">Notifies workspace consumers after staged mutations.</param>
        /// <param name="chooseImportPath">Selects a translation table to import.</param>
        /// <param name="chooseTableExportPath">Selects a new translation-table destination.</param>
        /// <param name="chooseProjectExportPath">Selects a new translated-project destination.</param>
        /// <param name="exportProject">Writes the current staged project through its format boundary.</param>
        /// <param name="convertToTraditional">Converts one value through the writing-variant provider.</param>
        /// <param name="copyText">Copies a manual provider request.</param>
        /// <param name="busyChanged">Notifies the parent workspace when long-running tool work changes state.</param>
        /// <param name="chooseRamCacheImportPath">Selects a bounded legacy-compatible RamCache file.</param>
        /// <param name="chooseRamCacheExportPath">Selects a new legacy-compatible RamCache destination.</param>
        /// <param name="clearTranslationCaches">Clears selected active-project cache stores.</param>
        /// <param name="confirmCacheClear">Confirms the selected destructive cache scope.</param>
        internal PreviewWorkspaceToolsViewModel(
            Func<IReadOnlyList<PreviewTranslationEntry>> getAllEntries,
            Func<IReadOnlyList<PreviewTranslationEntry>> getVisibleEntries,
            Func<PreviewTranslationEntry> getSelectedEntry,
            Action notifyEntriesChanged,
            Func<string> chooseImportPath,
            Func<string> chooseTableExportPath,
            Func<string> chooseProjectExportPath,
            Func<string, CancellationToken, Task> exportProject,
            Func<string, CancellationToken, string> convertToTraditional,
            Action<string> copyText,
            Action<bool> busyChanged = null,
            Func<string> chooseRamCacheImportPath = null,
            Func<string> chooseRamCacheExportPath = null,
            Func<bool, bool, CancellationToken, Task> clearTranslationCaches = null,
            Func<bool, bool, bool> confirmCacheClear = null)
        {
            _getAllEntries = getAllEntries ?? throw new ArgumentNullException(nameof(getAllEntries));
            _getVisibleEntries = getVisibleEntries ?? throw new ArgumentNullException(nameof(getVisibleEntries));
            _getSelectedEntry = getSelectedEntry ?? throw new ArgumentNullException(nameof(getSelectedEntry));
            _notifyEntriesChanged = notifyEntriesChanged ??
                throw new ArgumentNullException(nameof(notifyEntriesChanged));
            _chooseImportPath = chooseImportPath ?? (() => string.Empty);
            _chooseTableExportPath = chooseTableExportPath ?? (() => string.Empty);
            _chooseProjectExportPath = chooseProjectExportPath ?? (() => string.Empty);
            _exportProject = exportProject;
            _convertToTraditional = convertToTraditional;
            _copyText = copyText ?? (value => { });
            _busyChanged = busyChanged ?? (value => { });
            _tableService = new PreviewTranslationTableService();
            _ramCacheService = new PreviewRamCacheService();
            _chooseRamCacheImportPath = chooseRamCacheImportPath ?? (() => string.Empty);
            _chooseRamCacheExportPath = chooseRamCacheExportPath ?? (() => string.Empty);
            _clearTranslationCaches = clearTranslationCaches;
            _confirmCacheClear = confirmCacheClear ?? ((provider, user) => false);
            _interactiveService = new PreviewInteractiveExchangeService();
            _findText = string.Empty;
            _replacementText = string.Empty;
            _terminologySearch = string.Empty;
            _interactiveRequest = string.Empty;
            _interactiveResponse = string.Empty;
            _statusText = PreviewMessageCatalog.Get("Workspace_Tools_Ready");
            Terminology = new ObservableCollection<PreviewTerminologyEntry>();
            ScopeOptions = new[]
            {
                new PreviewWorkspaceToolScopeOption(
                    PreviewWorkspaceToolScope.Current,
                    PreviewMessageCatalog.Get("Workspace_Tools_Scope_Current")),
                new PreviewWorkspaceToolScopeOption(
                    PreviewWorkspaceToolScope.Visible,
                    PreviewMessageCatalog.Get("Workspace_Tools_Scope_Visible")),
                new PreviewWorkspaceToolScopeOption(
                    PreviewWorkspaceToolScope.All,
                    PreviewMessageCatalog.Get("Workspace_Tools_Scope_All"))
            };
            _selectedScope = ScopeOptions[1];

            PreviewReplaceCommand = new PreviewShellCommand(parameter => RefreshPreview());
            ApplyReplaceCommand = new PreviewShellCommand(
                parameter => ApplyReplace(),
                parameter => PreviewCount > 0 && !IsBusy);
            UndoCommand = new PreviewShellCommand(parameter => Undo(), parameter => CanUndo && !IsBusy);
            AddSelectedTermCommand = new PreviewShellCommand(
                parameter => AddSelectedTerm(),
                parameter => _getSelectedEntry() != null);
            RemoveTermCommand = new PreviewShellCommand(
                parameter => RemoveSelectedTerm(),
                parameter => SelectedTerm != null);
            ApplyTerminologyCommand = new PreviewShellCommand(
                parameter => ApplyTerminology(),
                parameter => Terminology.Count > 0 && !IsBusy);
            ImportTableCommand = new PreviewShellCommand(parameter => ImportTable(), parameter => !IsBusy);
            ExportTableCommand = new PreviewShellCommand(
                parameter => ExportTable(),
                parameter => _getAllEntries().Count > 0 && !IsBusy);
            ImportRamCacheCommand = new PreviewShellCommand(parameter => ImportRamCache(), parameter => !IsBusy);
            ExportRamCacheCommand = new PreviewShellCommand(
                parameter => ExportRamCache(),
                parameter => _getAllEntries().Count > 0 && !IsBusy);
            ClearCachesCommand = new PreviewShellCommand(
                parameter => ClearCaches(),
                parameter => _clearTranslationCaches != null && (ClearProviderCache || ClearUserCache) && !IsBusy);
            ExportProjectCommand = new PreviewShellCommand(
                parameter => ExportProject(),
                parameter => _exportProject != null && !IsExportBlocked && !IsBusy);
            ConvertCommand = new PreviewShellCommand(
                parameter => ConvertToTraditional(),
                parameter => _convertToTraditional != null && !IsBusy);
            ApplyConversionCommand = new PreviewShellCommand(
                parameter => ApplyConversion(),
                parameter => CanApplyConversion && !IsBusy);
            PrepareInteractiveCommand = new PreviewShellCommand(
                parameter => PrepareInteractive(),
                parameter => _getSelectedEntry() != null && !IsBusy);
            CopyInteractiveCommand = new PreviewShellCommand(
                parameter => _copyText(InteractiveRequest),
                parameter => !string.IsNullOrWhiteSpace(InteractiveRequest));
            ApplyInteractiveCommand = new PreviewShellCommand(
                parameter => ApplyInteractive(),
                parameter => CanApplyInteractive && !IsBusy);
            CancelCommand = new PreviewShellCommand(
                parameter => _cancellation?.Cancel(),
                parameter => _cancellation != null);
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the localized operation scopes.</summary>
        public IReadOnlyList<PreviewWorkspaceToolScopeOption> ScopeOptions { get; private set; }

        /// <summary>Gets the editable project terminology pairs.</summary>
        public ObservableCollection<PreviewTerminologyEntry> Terminology { get; private set; }

        /// <summary>Gets the command that refreshes the replacement preview.</summary>
        public ICommand PreviewReplaceCommand { get; private set; }
        /// <summary>Gets the command that applies the previewed replacement.</summary>
        public ICommand ApplyReplaceCommand { get; private set; }
        /// <summary>Gets the command that restores targets changed by the last tool.</summary>
        public ICommand UndoCommand { get; private set; }
        /// <summary>Gets the command that creates a terminology pair from the selected entry.</summary>
        public ICommand AddSelectedTermCommand { get; private set; }
        /// <summary>Gets the command that removes the selected terminology pair.</summary>
        public ICommand RemoveTermCommand { get; private set; }
        /// <summary>Gets the command that applies exact terminology matches to draft targets.</summary>
        public ICommand ApplyTerminologyCommand { get; private set; }
        /// <summary>Gets the command that imports draft targets from a bounded table.</summary>
        public ICommand ImportTableCommand { get; private set; }
        /// <summary>Gets the command that exports the normalized translation table.</summary>
        public ICommand ExportTableCommand { get; private set; }
        /// <summary>Gets the command that imports draft targets from a compatible RamCache file.</summary>
        public ICommand ImportRamCacheCommand { get; private set; }
        /// <summary>Gets the command that exports a compatible RamCache file.</summary>
        public ICommand ExportRamCacheCommand { get; private set; }
        /// <summary>Gets the command that clears explicitly selected project cache stores.</summary>
        public ICommand ClearCachesCommand { get; private set; }
        /// <summary>Gets the command that exports the translated project through its format writer.</summary>
        public ICommand ExportProjectCommand { get; private set; }
        /// <summary>Gets the command that prepares a writing-variant conversion preview.</summary>
        public ICommand ConvertCommand { get; private set; }
        /// <summary>Gets the command that applies the prepared writing-variant conversion.</summary>
        public ICommand ApplyConversionCommand { get; private set; }
        /// <summary>Gets the command that prepares a manual provider request.</summary>
        public ICommand PrepareInteractiveCommand { get; private set; }
        /// <summary>Gets the command that copies the prepared provider request.</summary>
        public ICommand CopyInteractiveCommand { get; private set; }
        /// <summary>Gets the command that validates and applies a provider response.</summary>
        public ICommand ApplyInteractiveCommand { get; private set; }
        /// <summary>Gets the command that cancels active conversion or export work.</summary>
        public ICommand CancelCommand { get; private set; }

        /// <summary>Gets or sets the exact target text to find.</summary>
        public string FindText
        {
            get => _findText;
            set
            {
                if (SetField(ref _findText, value ?? string.Empty))
                {
                    RefreshPreview();
                }
            }
        }

        /// <summary>Gets or sets the replacement target text.</summary>
        public string ReplacementText
        {
            get => _replacementText;
            set => SetField(ref _replacementText, value ?? string.Empty);
        }

        /// <summary>Gets or sets the scope used by editing tools.</summary>
        public PreviewWorkspaceToolScopeOption SelectedScope
        {
            get => _selectedScope;
            set
            {
                if (value != null && SetField(ref _selectedScope, value))
                {
                    RefreshPreview();
                }
            }
        }

        /// <summary>Gets the number of entries affected by the replacement preview.</summary>
        public int PreviewCount
        {
            get => _previewCount;
            private set
            {
                if (SetField(ref _previewCount, value))
                {
                    OnPropertyChanged(nameof(PreviewText));
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets the localized replacement preview summary.</summary>
        public string PreviewText => PreviewMessageCatalog.Format("Workspace_Tools_ReplacePreview", PreviewCount);

        /// <summary>Gets whether draft or rejected content blocks project export.</summary>
        public bool IsExportBlocked => _getAllEntries().Count == 0 ||
            _getAllEntries().Any(entry => entry.IsDraft || entry.ReviewState == PreviewReviewState.Rejected);

        /// <summary>Gets the localized ready, warning, or blocked export state.</summary>
        public string ExportReadinessText
        {
            get
            {
                IReadOnlyList<PreviewTranslationEntry> entries = _getAllEntries();
                if (entries.Count == 0)
                {
                    return PreviewMessageCatalog.Get("Workspace_Tools_Export_NoProject");
                }

                int blocked = entries.Count(entry => entry.IsDraft || entry.ReviewState == PreviewReviewState.Rejected);
                if (blocked > 0)
                {
                    return PreviewMessageCatalog.Format("Workspace_Tools_Export_Blocked", blocked);
                }

                int unapproved = entries.Count(entry => entry.ReviewState != PreviewReviewState.Approved);
                return unapproved == 0
                    ? PreviewMessageCatalog.Get("Workspace_Tools_Export_Ready")
                    : PreviewMessageCatalog.Format("Workspace_Tools_Export_Warning", unapproved);
            }
        }

        /// <summary>Gets or sets the terminology search query.</summary>
        public string TerminologySearch
        {
            get => _terminologySearch;
            set
            {
                if (SetField(ref _terminologySearch, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(FilteredTerminology));
                }
            }
        }

        /// <summary>Gets terminology pairs matching the current query.</summary>
        public IReadOnlyList<PreviewTerminologyEntry> FilteredTerminology => Terminology
            .Where(term => string.IsNullOrWhiteSpace(TerminologySearch) ||
                Contains(term.Source, TerminologySearch) || Contains(term.Target, TerminologySearch))
            .ToList();

        /// <summary>Gets or sets the selected terminology pair.</summary>
        public PreviewTerminologyEntry SelectedTerm
        {
            get => _selectedTerm;
            set
            {
                if (SetField(ref _selectedTerm, value))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets the prepared manual provider request.</summary>
        public string InteractiveRequest
        {
            get => _interactiveRequest;
            private set
            {
                if (SetField(ref _interactiveRequest, value ?? string.Empty))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets or sets the untrusted manual provider response.</summary>
        public string InteractiveResponse
        {
            get => _interactiveResponse;
            set
            {
                if (SetField(ref _interactiveResponse, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(CanApplyInteractive));
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets whether the response contains the matching request identity and a target.</summary>
        public bool CanApplyInteractive
        {
            get
            {
                string target;
                return _interactiveService.TryExtractTarget(InteractiveResponse, _interactiveRequestId, out target);
            }
        }

        /// <summary>Gets the localized status of the last workspace-tool operation.</summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value ?? string.Empty);
        }

        /// <summary>Gets whether a cancellable workspace-tool operation is active.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    _busyChanged(value);
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets whether the last applied tool mutation can be restored.</summary>
        public bool CanUndo => _undoTargets != null && _undoTargets.Count > 0;

        /// <summary>Gets whether a writing-variant preview is ready to apply.</summary>
        public bool CanApplyConversion => _pendingConversion != null && _pendingConversion.Count > 0;

        /// <summary>Gets the localized writing-variant preview summary.</summary>
        public string ConversionPreviewText => PreviewMessageCatalog.Format(
            "Workspace_Tools_ConversionPreview",
            _pendingConversion == null ? 0 : _pendingConversion.Count);

        internal void RefreshProjectState()
        {
            _undoTargets = null;
            _pendingConversion = null;
            PreviewCount = 0;
            InteractiveRequest = string.Empty;
            InteractiveResponse = string.Empty;
            _interactiveRequestId = null;
            StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Ready");
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanApplyConversion));
            OnPropertyChanged(nameof(ConversionPreviewText));
            OnPropertyChanged(nameof(IsExportBlocked));
            OnPropertyChanged(nameof(ExportReadinessText));
            RaiseCommands();
        }

        internal void RefreshPreview()
        {
            PreviewCount = string.IsNullOrEmpty(FindText)
                ? 0
                : GetScopeEntries().Count(entry => ContainsOrdinal(entry.TargetText, FindText));
        }

        internal void RefreshReadiness()
        {
            OnPropertyChanged(nameof(IsExportBlocked));
            OnPropertyChanged(nameof(ExportReadinessText));
            RaiseCommands();
        }

        internal int ApplyReplace()
        {
            if (string.IsNullOrEmpty(FindText))
            {
                return 0;
            }

            Dictionary<PreviewTranslationEntry, string> changes = GetScopeEntries()
                .Where(entry => ContainsOrdinal(entry.TargetText, FindText))
                .ToDictionary(entry => entry, entry => entry.TargetText);
            foreach (KeyValuePair<PreviewTranslationEntry, string> change in changes)
            {
                change.Key.TargetText = change.Value.Replace(FindText, ReplacementText);
            }

            RecordMutation(changes, "Workspace_Tools_ReplaceApplied");
            RefreshPreview();
            return changes.Count;
        }

        internal int ApplyTerminology()
        {
            Dictionary<string, string> validTerms = Terminology
                .Where(term => !string.IsNullOrEmpty(term.Source) && !string.IsNullOrEmpty(term.Target))
                .GroupBy(term => term.Source, StringComparer.CurrentCulture)
                .ToDictionary(group => group.Key, group => group.Last().Target, StringComparer.CurrentCulture);
            var changes = new Dictionary<PreviewTranslationEntry, string>();
            foreach (PreviewTranslationEntry entry in GetScopeEntries())
            {
                string target;
                if (entry.IsDraft && validTerms.TryGetValue(entry.SourceText, out target))
                {
                    changes.Add(entry, entry.TargetText);
                    entry.TargetText = target;
                }
            }

            RecordMutation(changes, "Workspace_Tools_TerminologyApplied");
            return changes.Count;
        }

        internal void ImportTable(string path)
        {
            Dictionary<PreviewTranslationEntry, string> changes =
                _tableService.ImportDraftTargets(path, _getAllEntries());
            RecordMutation(changes, "Workspace_Tools_TableImported");
        }

        internal void ExportTable(string path)
        {
            _tableService.Export(path, _getAllEntries());
        }

        /// <summary>Gets or sets whether provider-produced project cache entries are cleared.</summary>
        public bool ClearProviderCache
        {
            get => _clearProviderCache;
            set
            {
                if (SetField(ref _clearProviderCache, value))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets or sets whether user-entered project cache entries are cleared.</summary>
        public bool ClearUserCache
        {
            get => _clearUserCache;
            set
            {
                if (SetField(ref _clearUserCache, value))
                {
                    RaiseCommands();
                }
            }
        }

        internal void ImportRamCache(string path)
        {
            Dictionary<PreviewTranslationEntry, string> changes =
                _ramCacheService.ImportDraftTargets(path, _getAllEntries());
            RecordMutation(changes, "Workspace_Tools_RamCacheImported");
        }

        internal void ExportRamCache(string path)
        {
            _ramCacheService.Export(path, _getAllEntries());
        }

        private void AddSelectedTerm()
        {
            PreviewTranslationEntry entry = _getSelectedEntry();
            if (entry == null)
            {
                return;
            }

            var term = new PreviewTerminologyEntry(entry.SourceText, entry.TargetText);
            Terminology.Add(term);
            SelectedTerm = term;
            OnPropertyChanged(nameof(FilteredTerminology));
            RaiseCommands();
        }

        private void RemoveSelectedTerm()
        {
            if (SelectedTerm != null)
            {
                Terminology.Remove(SelectedTerm);
                SelectedTerm = null;
                OnPropertyChanged(nameof(FilteredTerminology));
            }
        }

        private void Undo()
        {
            Dictionary<PreviewTranslationEntry, string> changes = _undoTargets;
            _undoTargets = null;
            if (changes == null)
            {
                return;
            }

            foreach (KeyValuePair<PreviewTranslationEntry, string> change in changes)
            {
                change.Key.TargetText = change.Value;
            }

            _notifyEntriesChanged();
            StatusText = PreviewMessageCatalog.Format("Workspace_Tools_Undone", changes.Count);
            OnPropertyChanged(nameof(CanUndo));
            RaiseCommands();
        }

        private async void ConvertToTraditional()
        {
            IReadOnlyList<PreviewTranslationEntry> entries = GetScopeEntries();
            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            IsBusy = true;
            StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Converting");
            RaiseCommands();
            try
            {
                var staged = await Task.Run(() =>
                {
                    var values = new Dictionary<PreviewTranslationEntry, string>();
                    foreach (PreviewTranslationEntry entry in entries)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        string source = string.IsNullOrWhiteSpace(entry.TargetText)
                            ? entry.SourceText
                            : entry.TargetText;
                        string converted = _convertToTraditional(source, cancellation.Token);
                        if (!string.IsNullOrWhiteSpace(converted) &&
                            !string.Equals(converted, entry.TargetText, StringComparison.Ordinal))
                        {
                            values.Add(entry, converted);
                        }
                    }

                    return values;
                });
                cancellation.Token.ThrowIfCancellationRequested();
                _pendingConversion = staged;
                StatusText = PreviewMessageCatalog.Format("Workspace_Tools_ConversionPreview", staged.Count);
                OnPropertyChanged(nameof(CanApplyConversion));
                OnPropertyChanged(nameof(ConversionPreviewText));
            }
            catch (OperationCanceledException)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Cancelled");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                IsBusy = false;
                RaiseCommands();
            }
        }

        private void ApplyConversion()
        {
            Dictionary<PreviewTranslationEntry, string> staged = _pendingConversion;
            _pendingConversion = null;
            if (staged == null)
            {
                return;
            }

            var changes = staged.ToDictionary(pair => pair.Key, pair => pair.Key.TargetText);
            foreach (KeyValuePair<PreviewTranslationEntry, string> value in staged)
            {
                value.Key.TargetText = value.Value;
            }

            OnPropertyChanged(nameof(CanApplyConversion));
            OnPropertyChanged(nameof(ConversionPreviewText));
            RecordMutation(changes, "Workspace_Tools_ConversionApplied");
        }

        private async void ExportProject()
        {
            string path = _chooseProjectExportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            IsBusy = true;
            StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Exporting");
            RaiseCommands();
            try
            {
                await _exportProject(path, cancellation.Token);
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Exported");
            }
            catch (OperationCanceledException)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Cancelled");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                IsBusy = false;
                RaiseCommands();
            }
        }

        private void ImportTable()
        {
            string path = _chooseImportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ImportTable(path);
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
        }

        private void ExportTable()
        {
            string path = _chooseTableExportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ExportTable(path);
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_TableExported");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
        }

        private void ImportRamCache()
        {
            string path = _chooseRamCacheImportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ImportRamCache(path);
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
        }

        private void ExportRamCache()
        {
            string path = _chooseRamCacheExportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ExportRamCache(path);
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_RamCacheExported");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
        }

        private async void ClearCaches()
        {
            bool provider = ClearProviderCache;
            bool user = ClearUserCache;
            if ((!provider && !user) || !_confirmCacheClear(provider, user))
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            IsBusy = true;
            StatusText = PreviewMessageCatalog.Get("Workspace_Tools_CacheClearing");
            try
            {
                await _clearTranslationCaches(provider, user, cancellation.Token);
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_CacheCleared");
            }
            catch (OperationCanceledException)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_Cancelled");
            }
            catch (Exception)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_OperationFailed");
            }
            finally
            {
                cancellation.Dispose();
                _cancellation = null;
                IsBusy = false;
                RaiseCommands();
            }
        }

        private void PrepareInteractive()
        {
            PreviewTranslationEntry entry = _getSelectedEntry();
            if (entry == null)
            {
                return;
            }

            InteractiveRequest = _interactiveService.Prepare(entry.SourceText, out _interactiveRequestId);
            InteractiveResponse = string.Empty;
            OnPropertyChanged(nameof(CanApplyInteractive));
            StatusText = PreviewMessageCatalog.Get("Workspace_Tools_InteractivePrepared");
        }

        private void ApplyInteractive()
        {
            if (!CanApplyInteractive)
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_InteractiveMismatch");
                return;
            }

            PreviewTranslationEntry entry = _getSelectedEntry();
            if (entry == null)
            {
                return;
            }

            var changes = new Dictionary<PreviewTranslationEntry, string> { { entry, entry.TargetText } };
            string target;
            if (!_interactiveService.TryExtractTarget(InteractiveResponse, _interactiveRequestId, out target))
            {
                StatusText = PreviewMessageCatalog.Get("Workspace_Tools_InteractiveMismatch");
                return;
            }

            entry.ApplyGeneratedTarget(target);
            RecordMutation(changes, "Workspace_Tools_InteractiveApplied");
        }

        private IReadOnlyList<PreviewTranslationEntry> GetScopeEntries()
        {
            switch (SelectedScope.Value)
            {
                case PreviewWorkspaceToolScope.Current:
                    PreviewTranslationEntry selected = _getSelectedEntry();
                    return selected == null ? new PreviewTranslationEntry[0] : new[] { selected };
                case PreviewWorkspaceToolScope.Visible:
                    return _getVisibleEntries();
                default:
                    return _getAllEntries();
            }
        }

        private void RecordMutation(Dictionary<PreviewTranslationEntry, string> changes, string messageId)
        {
            _undoTargets = changes.Count == 0 ? null : changes;
            _notifyEntriesChanged();
            StatusText = PreviewMessageCatalog.Format(messageId, changes.Count);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(IsExportBlocked));
            OnPropertyChanged(nameof(ExportReadinessText));
            RaiseCommands();
        }

        private static bool Contains(string value, string search)
        {
            return value != null && value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static bool ContainsOrdinal(string value, string search)
        {
            return value != null && value.IndexOf(search, StringComparison.Ordinal) >= 0;
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void RaiseCommands()
        {
            foreach (ICommand command in new[]
            {
                ApplyReplaceCommand, UndoCommand, AddSelectedTermCommand, RemoveTermCommand,
                ApplyTerminologyCommand, ImportTableCommand, ExportTableCommand, ImportRamCacheCommand,
                ExportRamCacheCommand, ClearCachesCommand, ExportProjectCommand,
                ConvertCommand, ApplyConversionCommand, PrepareInteractiveCommand, CopyInteractiveCommand,
                ApplyInteractiveCommand, CancelCommand
            })
            {
                var previewCommand = command as PreviewShellCommand;
                previewCommand?.RaiseCanExecuteChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
