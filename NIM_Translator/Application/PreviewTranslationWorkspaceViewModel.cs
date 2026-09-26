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
    /// Identifies an entry-state filter in the preview translation workspace.
    /// </summary>
    internal enum PreviewTranslationStateFilter
    {
        All,
        Draft,
        Translated,
        Modified
    }

    /// <summary>
    /// Describes one localized entry-state filter option.
    /// </summary>
    internal sealed class PreviewTranslationStateFilterOption
    {
        /// <summary>
        /// Creates a localized state-filter option.
        /// </summary>
        /// <param name="value">The filter behavior.</param>
        /// <param name="label">The localized visible label.</param>
        internal PreviewTranslationStateFilterOption(PreviewTranslationStateFilter value, string label)
        {
            Value = value;
            Label = label;
        }

        /// <summary>
        /// Gets the filter behavior.
        /// </summary>
        public PreviewTranslationStateFilter Value { get; private set; }

        /// <summary>
        /// Gets the localized visible label.
        /// </summary>
        public string Label { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Owns preview translation workspace state, filtering, editing, persistence, and cancellable provider work.
    /// </summary>
    internal sealed class PreviewTranslationWorkspaceViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Func<string> _chooseProjectPath;
        private readonly Action _openLegacyWorkspace;
        private readonly PreviewShellViewModel _shell;
        private readonly Func<string, IPreviewTranslationProject> _openProject;
        private readonly Func<string, Task<bool>> _requestProjectOpen;
        private readonly List<PreviewTranslationEntry> _allEntries = new List<PreviewTranslationEntry>();
        private readonly HashSet<PreviewTranslationEntry> _draftEntries = new HashSet<PreviewTranslationEntry>();
        private readonly HashSet<PreviewTranslationEntry> _modifiedEntries = new HashSet<PreviewTranslationEntry>();
        private readonly HashSet<PreviewTranslationEntry> _visibleEntries = new HashSet<PreviewTranslationEntry>();
        private int _visibleDraftCount;
        private IPreviewTranslationProject _project;
        private PreviewTranslationEntry _selectedEntry;
        private PreviewTranslationStateFilterOption _selectedStateFilter;
        private string _selectedTypeFilter;
        private string _searchText;
        private bool _isBusy;
        private string _operationText;
        private double _operationProgress;
        private CancellationTokenSource _operationCancellation;

        /// <summary>
        /// Creates a workspace connected to the persistent preview shell state.
        /// </summary>
        /// <param name="chooseProjectPath">Selects a supported project path or returns an empty value.</param>
        /// <param name="openLegacyWorkspace">Opens the workflow-specific legacy fallback.</param>
        /// <param name="shell">The persistent shell status owner.</param>
        /// <param name="openProject">Opens the selected path through the parser boundary.</param>
        /// <param name="requestProjectOpen">Optionally routes user-initiated opens through the Project Hub.</param>
        /// <param name="chooseTableImportPath">Selects a bounded translation table for import.</param>
        /// <param name="chooseTableExportPath">Selects a new translation-table destination.</param>
        /// <param name="chooseProjectExportPath">Selects a new translated-project destination.</param>
        /// <param name="convertToTraditional">
        /// Converts one target through the configured writing-variant boundary.
        /// </param>
        /// <param name="copyText">Copies an interactive-provider request without exposing it to diagnostics.</param>
        /// <param name="chooseRamCacheImportPath">Selects a bounded RamCache JSON file.</param>
        /// <param name="chooseRamCacheExportPath">Selects a new RamCache JSON destination.</param>
        /// <param name="confirmCacheClear">Confirms the selected destructive project-cache scope.</param>
        internal PreviewTranslationWorkspaceViewModel(
            Func<string> chooseProjectPath,
            Action openLegacyWorkspace,
            PreviewShellViewModel shell,
            Func<string, IPreviewTranslationProject> openProject,
            Func<string, Task<bool>> requestProjectOpen = null,
            Func<string> chooseTableImportPath = null,
            Func<string> chooseTableExportPath = null,
            Func<string> chooseProjectExportPath = null,
            Func<string, CancellationToken, string> convertToTraditional = null,
            Action<string> copyText = null,
            Func<string> chooseRamCacheImportPath = null,
            Func<string> chooseRamCacheExportPath = null,
            Func<bool, bool, bool> confirmCacheClear = null)
        {
            _chooseProjectPath = chooseProjectPath ?? throw new ArgumentNullException(nameof(chooseProjectPath));
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _openProject = openProject ?? throw new ArgumentNullException(nameof(openProject));
            _requestProjectOpen = requestProjectOpen;
            _searchText = string.Empty;
            _operationText = PreviewMessageCatalog.Get("Common_State_Ready");

            StateFilters = new[]
            {
                CreateStateFilter(PreviewTranslationStateFilter.All, "Workspace_Filter_AllStates"),
                CreateStateFilter(PreviewTranslationStateFilter.Draft, "Workspace_State_Draft"),
                CreateStateFilter(PreviewTranslationStateFilter.Translated, "Workspace_State_Translated"),
                CreateStateFilter(PreviewTranslationStateFilter.Modified, "Workspace_State_Modified")
            };
            _selectedStateFilter = StateFilters[0];
            TypeFilters = new ObservableCollection<string>
            {
                PreviewMessageCatalog.Get("Workspace_Filter_AllTypes")
            };
            _selectedTypeFilter = TypeFilters[0];
            Entries = new List<PreviewTranslationEntry>();
            Tools = new PreviewWorkspaceToolsViewModel(
                () => _allEntries,
                () => Entries,
                () => SelectedEntry,
                OnEntryStateChanged,
                chooseTableImportPath,
                chooseTableExportPath,
                chooseProjectExportPath,
                ExportProjectAsync,
                convertToTraditional,
                copyText,
                OnToolsBusyChanged,
                chooseRamCacheImportPath,
                chooseRamCacheExportPath,
                ClearTranslationCachesAsync,
                confirmCacheClear);
            Inspectors = new PreviewContextInspectorViewModel(
                LoadEntryContext,
                key => _allEntries.FirstOrDefault(entry =>
                    string.Equals(entry.Key, key, StringComparison.Ordinal)),
                RevealEntry);

            OpenProjectCommand = new PreviewShellCommand(
                parameter => OpenSelectedProject(),
                parameter => !IsBusy && !Tools.IsBusy);
            SaveCommand = new PreviewShellCommand(
                parameter => SaveProject(),
                parameter => HasProject && IsModified && !IsBusy && !Tools.IsBusy);
            TranslateEntryCommand = new PreviewShellCommand(
                parameter => TranslateSelectedEntry(),
                parameter => SelectedEntry != null && !IsBusy && !Tools.IsBusy);
            TranslateScopeCommand = new PreviewShellCommand(
                parameter => TranslateScope(),
                parameter => Entries.Count > 0 && !IsBusy && !Tools.IsBusy);
            CancelOperationCommand = new PreviewShellCommand(
                parameter => CancelOperation(),
                parameter => CanCancelOperation);
            PreviousEntryCommand = new PreviewShellCommand(
                parameter => SelectPreviousEntry(),
                parameter => SelectedEntry != null && Entries.Count > 1 && !IsBusy);
            ApplyEntryCommand = new PreviewShellCommand(
                parameter => ApplyEntry(),
                parameter => SelectedEntry != null && !IsBusy);
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs after the normalized project entry set changes.
        /// </summary>
        internal event EventHandler ProjectChanged;

        /// <summary>
        /// Gets the filtered entries presented by the virtualized list.
        /// </summary>
        public IReadOnlyList<PreviewTranslationEntry> Entries { get; private set; }

        /// <summary>
        /// Gets every normalized entry in the current project without workspace filtering.
        /// </summary>
        internal IReadOnlyList<PreviewTranslationEntry> ProjectEntries => _allEntries;

        /// <summary>
        /// Gets the current project path for project-scoped metadata storage.
        /// </summary>
        internal string ProjectPath => _project?.Path;

        /// <summary>
        /// Gets the localized stable entry-state filters.
        /// </summary>
        public IReadOnlyList<PreviewTranslationStateFilterOption> StateFilters { get; private set; }

        /// <summary>
        /// Gets the record types available in the current project.
        /// </summary>
        public ObservableCollection<string> TypeFilters { get; private set; }

        /// <summary>
        /// Gets the cohesive editing, table, export, conversion, and interactive-provider tools.
        /// </summary>
        public PreviewWorkspaceToolsViewModel Tools { get; private set; }

        /// <summary>
        /// Gets the docked code, record, NPC, relationship, and asset inspectors.
        /// </summary>
        public PreviewContextInspectorViewModel Inspectors { get; private set; }

        /// <summary>
        /// Gets the command that selects and opens a supported project.
        /// </summary>
        public ICommand OpenProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command that persists all staged targets.
        /// </summary>
        public ICommand SaveCommand { get; private set; }

        /// <summary>
        /// Gets the command that translates the selected entry.
        /// </summary>
        public ICommand TranslateEntryCommand { get; private set; }

        /// <summary>
        /// Gets the command that translates the current filtered scope.
        /// </summary>
        public ICommand TranslateScopeCommand { get; private set; }

        /// <summary>
        /// Gets the command that requests cooperative operation cancellation.
        /// </summary>
        public ICommand CancelOperationCommand { get; private set; }

        /// <summary>
        /// Gets the command that moves selection to the previous visible entry.
        /// </summary>
        public ICommand PreviousEntryCommand { get; private set; }

        /// <summary>
        /// Gets the command that stages the current edit and advances selection.
        /// </summary>
        public ICommand ApplyEntryCommand { get; private set; }

        /// <summary>
        /// Gets the command that opens the complete legacy translation workflow.
        /// </summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>
        /// Gets or sets the entry presented by the editor and context inspector.
        /// </summary>
        public PreviewTranslationEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (ReferenceEquals(_selectedEntry, value))
                {
                    return;
                }

                _selectedEntry = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                Tools?.RefreshPreview();
                Inspectors?.SelectEntry(value);
                RaiseCommandAvailability();
            }
        }

        /// <summary>
        /// Gets or sets the active entry-state filter.
        /// </summary>
        public PreviewTranslationStateFilterOption SelectedStateFilter
        {
            get => _selectedStateFilter;
            set
            {
                if (value == null || ReferenceEquals(_selectedStateFilter, value))
                {
                    return;
                }

                _selectedStateFilter = value;
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        /// <summary>
        /// Gets or sets the active record-type filter.
        /// </summary>
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (string.Equals(_selectedTypeFilter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedTypeFilter = value;
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        /// <summary>
        /// Gets or sets the case-insensitive entry search query.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                string normalizedValue = value ?? string.Empty;
                if (string.Equals(_searchText, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _searchText = normalizedValue;
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        /// <summary>
        /// Gets whether a parser project is loaded.
        /// </summary>
        public bool HasProject => _project != null;

        /// <summary>
        /// Gets whether a visible entry is selected.
        /// </summary>
        public bool HasSelection => _selectedEntry != null;

        /// <summary>
        /// Gets whether the active provider operation supports cooperative cancellation.
        /// </summary>
        public bool CanCancelOperation => _operationCancellation != null;

        /// <summary>
        /// Gets whether project, provider, or persistence work is active.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
                RaiseCommandAvailability();
            }
        }

        /// <summary>
        /// Gets the localized active or idle operation text.
        /// </summary>
        public string OperationText
        {
            get => _operationText;
            private set
            {
                _operationText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets bounded operation progress from zero through one hundred.
        /// </summary>
        public double OperationProgress
        {
            get => _operationProgress;
            private set
            {
                _operationProgress = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the localized count of currently visible entries.
        /// </summary>
        public string EntryCountText => PreviewMessageCatalog.Format("Workspace_Entry_Count", Entries.Count);

        /// <summary>
        /// Gets the localized count of currently visible draft entries.
        /// </summary>
        public string DraftCountText => PreviewMessageCatalog.Format(
            "Workspace_Entry_DraftCount",
            _visibleDraftCount);

        /// <summary>
        /// Gets whether any project entry differs from its persisted target.
        /// </summary>
        public bool IsModified => _modifiedEntries.Count > 0;

        /// <summary>
        /// Opens and normalizes a selected project without blocking the UI thread.
        /// </summary>
        /// <param name="path">The selected private project path.</param>
        internal async Task<bool> OpenProjectAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || IsBusy || Tools.IsBusy)
            {
                return false;
            }

            BeginOperation(PreviewMessageCatalog.Get("Workspace_Project_Opening"), 0);
            try
            {
                IPreviewTranslationProject openedProject = await Task.Run(() => _openProject(path));
                IPreviewTranslationProject previousProject = _project;
                Inspectors.SelectEntry(null);
                _project = openedProject;
                if (previousProject != null)
                {
                    await Task.Run(() => previousProject.Dispose());
                }
                ReplaceEntries(openedProject.Entries);
                _shell.SetProject(openedProject.DisplayName, false);
                CompleteOperation();
                return true;
            }
            catch
            {
                FailOperation("Workspace_Project_OpenFailed");
                return false;
            }
        }

        /// <summary>
        /// Persists staged targets and reloads the project through its parser boundary.
        /// </summary>
        internal async Task SaveProjectAsync()
        {
            if (_project == null || IsBusy || Tools.IsBusy)
            {
                return;
            }

            BeginOperation(PreviewMessageCatalog.Get("Workspace_Project_Saving"), 0);
            try
            {
                string projectPath = _project.Path;
                await Task.Run(() => _project.Save());
                IPreviewTranslationProject reloadedProject = await Task.Run(() => _openProject(projectPath));
                IPreviewTranslationProject savedProject = _project;
                Inspectors.SelectEntry(null);
                _project = reloadedProject;
                await Task.Run(() => savedProject.Dispose());
                ReplaceEntries(reloadedProject.Entries);

                _shell.SetProject(_project.DisplayName, false);
                _shell.ShowNotification(
                    PreviewShellNotificationSeverity.Success,
                    "Workspace_Save_Succeeded");
                CompleteOperation();
            }
            catch
            {
                FailOperation("Workspace_Save_Failed");
            }
        }

        private Task ExportProjectAsync(string path, CancellationToken cancellationToken)
        {
            IPreviewTranslationProject project = _project;
            if (project == null)
            {
                throw new InvalidOperationException("A project must be open before export.");
            }

            return Task.Run(() => project.Export(path, cancellationToken), cancellationToken);
        }

        private Task ClearTranslationCachesAsync(
            bool clearProviderCache,
            bool clearUserCache,
            CancellationToken cancellationToken)
        {
            IPreviewTranslationProject project = _project;
            if (project == null)
            {
                throw new InvalidOperationException("A project must be open before clearing caches.");
            }

            return Task.Run(
                () => project.ClearTranslationCaches(clearProviderCache, clearUserCache, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Translates a stable entry snapshot sequentially with cooperative cancellation and bounded progress.
        /// </summary>
        /// <param name="entries">The selected or filtered entry snapshot.</param>
        internal async Task TranslateEntriesAsync(IReadOnlyList<PreviewTranslationEntry> entries)
        {
            if (_project == null || entries == null || entries.Count == 0 || IsBusy || Tools.IsBusy)
            {
                return;
            }

            _operationCancellation = new CancellationTokenSource();
            OnPropertyChanged(nameof(CanCancelOperation));
            CancellationToken cancellationToken = _operationCancellation.Token;
            int failures = 0;
            BeginOperation(PreviewMessageCatalog.Format(
                "Workspace_Operation_TranslatingProgress", 0, entries.Count), 0);

            try
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PreviewTranslationEntry entry = entries[index];
                    try
                    {
                        string translatedText = await Task.Run(
                            () => _project.Translate(entry, cancellationToken),
                            cancellationToken);
                        entry.ApplyGeneratedTarget(translatedText);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        failures++;
                    }

                    int completed = index + 1;
                    double progress = completed * 100d / entries.Count;
                    UpdateOperation(
                        PreviewMessageCatalog.Format(
                            "Workspace_Operation_TranslatingProgress", completed, entries.Count),
                        progress);
                }

                if (failures > 0)
                {
                    _shell.ShowNotification(
                        PreviewShellNotificationSeverity.Warning,
                        "Workspace_Operation_CompletedWithFailures",
                        failures);
                }

                CompleteOperation();
            }
            catch (OperationCanceledException)
            {
                CompleteOperation();
            }
            finally
            {
                _operationCancellation?.Dispose();
                _operationCancellation = null;
                OnPropertyChanged(nameof(CanCancelOperation));
                RaiseCommandAvailability();
                OnEntryStateChanged();
            }
        }

        /// <summary>Loads bounded translation history away from the UI thread.</summary>
        /// <param name="cancellationToken">Cancels the history read.</param>
        /// <returns>The active project's history records, or an empty list without a project.</returns>
        internal Task<IReadOnlyList<PreviewTranslationHistoryItem>> LoadTranslationHistoryAsync(
            CancellationToken cancellationToken)
        {
            IPreviewTranslationProject project = _project;
            return project == null
                ? Task.FromResult<IReadOnlyList<PreviewTranslationHistoryItem>>(
                    new List<PreviewTranslationHistoryItem>())
                : Task.Run(() => project.LoadTranslationHistory(cancellationToken), cancellationToken);
        }

        /// <summary>Restores one historical target and reveals its workspace entry.</summary>
        /// <param name="rowId">The persistent history row identifier.</param>
        /// <param name="cancellationToken">Cancels restoration before it is applied.</param>
        /// <returns>The restored entry, or <c>null</c> when no project is active.</returns>
        internal async Task<PreviewTranslationEntry> RestoreTranslationHistoryAsync(
            int rowId,
            CancellationToken cancellationToken)
        {
            IPreviewTranslationProject project = _project;
            if (project == null)
            {
                return null;
            }

            PreviewTranslationEntry entry = await Task.Run(
                () => project.RestoreTranslationHistory(rowId, cancellationToken),
                cancellationToken);
            if (entry != null)
            {
                OnEntryStateChanged();
                RevealEntry(entry);
            }
            return entry;
        }

        /// <summary>Marks one persisted history record as current.</summary>
        /// <param name="rowId">The persistent history row identifier.</param>
        /// <returns>A task representing the database operation.</returns>
        internal Task SetCurrentTranslationHistoryAsync(int rowId)
        {
            IPreviewTranslationProject project = _project;
            return project == null
                ? Task.FromResult(0)
                : Task.Run(() => project.SetCurrentTranslationHistory(rowId));
        }

        /// <summary>Deletes one persisted history record.</summary>
        /// <param name="rowId">The persistent history row identifier.</param>
        /// <returns>A task representing the database operation.</returns>
        internal Task DeleteTranslationHistoryAsync(int rowId)
        {
            IPreviewTranslationProject project = _project;
            return project == null
                ? Task.FromResult(0)
                : Task.Run(() => project.DeleteTranslationHistory(rowId));
        }

        /// <summary>Clears persisted translation history for the active project.</summary>
        /// <returns>A task representing the database operation.</returns>
        internal Task ClearTranslationHistoryAsync()
        {
            IPreviewTranslationProject project = _project;
            return project == null
                ? Task.FromResult(0)
                : Task.Run(() => project.ClearTranslationHistory());
        }

        /// <summary>
        /// Rebuilds the visible entry collection from the current search, state, and type filters.
        /// </summary>
        internal void RefreshFilter()
        {
            PreviewTranslationEntry previousSelection = SelectedEntry;
            var filteredEntries = new List<PreviewTranslationEntry>();
            foreach (PreviewTranslationEntry entry in _allEntries)
            {
                if (MatchesFilter(entry))
                {
                    filteredEntries.Add(entry);
                }
            }

            Entries = filteredEntries;
            _visibleEntries.Clear();
            foreach (PreviewTranslationEntry entry in filteredEntries)
            {
                _visibleEntries.Add(entry);
            }
            _visibleDraftCount = filteredEntries.Count(entry => _draftEntries.Contains(entry));
            OnPropertyChanged(nameof(Entries));
            SelectedEntry = Entries.Contains(previousSelection)
                ? previousSelection
                : Entries.FirstOrDefault();
            OnPropertyChanged(nameof(EntryCountText));
            OnPropertyChanged(nameof(DraftCountText));
            RaiseCommandAvailability();
            Tools?.RefreshPreview();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            Inspectors.Dispose();
            _project?.Dispose();
            _project = null;
        }

        private PreviewEntryContext LoadEntryContext(
            PreviewTranslationEntry entry,
            CancellationToken cancellationToken)
        {
            IPreviewTranslationProject project = _project;
            if (project == null)
            {
                return new PreviewEntryContext(
                    string.Empty,
                    string.Empty,
                    new PreviewContextMetadata[0],
                    new PreviewContextRelation[0],
                    new PreviewNpcContext[0],
                    null);
            }

            return project.LoadContext(entry, cancellationToken);
        }

        private static PreviewTranslationStateFilterOption CreateStateFilter(
            PreviewTranslationStateFilter value,
            string messageId)
        {
            return new PreviewTranslationStateFilterOption(value, PreviewMessageCatalog.Get(messageId));
        }

        private async void OpenSelectedProject()
        {
            string path = _chooseProjectPath();
            if (_requestProjectOpen != null)
            {
                await _requestProjectOpen(path);
                return;
            }

            await OpenProjectAsync(path);
        }

        private async void SaveProject()
        {
            await SaveProjectAsync();
        }

        private async void TranslateSelectedEntry()
        {
            if (SelectedEntry != null)
            {
                await TranslateEntriesAsync(new[] { SelectedEntry });
            }
        }

        private async void TranslateScope()
        {
            await TranslateEntriesAsync(Entries.ToList());
        }

        private void CancelOperation()
        {
            if (_operationCancellation == null)
            {
                return;
            }

            OperationText = PreviewMessageCatalog.Get("Workspace_Operation_Cancelling");
            _shell.SetOperation("Workspace_Operation_Cancelling", OperationProgress);
            _operationCancellation.Cancel();
        }

        private void SelectPreviousEntry()
        {
            int index = Entries.ToList().IndexOf(SelectedEntry);
            if (index > 0)
            {
                SelectedEntry = Entries[index - 1];
            }
        }

        private void ApplyEntry()
        {
            OnEntryStateChanged();
            int index = Entries.ToList().IndexOf(SelectedEntry);
            if (index >= 0 && index < Entries.Count - 1)
            {
                SelectedEntry = Entries[index + 1];
            }
        }

        private void ReplaceEntries(IEnumerable<PreviewTranslationEntry> entries)
        {
            foreach (PreviewTranslationEntry entry in _allEntries)
            {
                entry.PropertyChanged -= EntryPropertyChanged;
            }

            _allEntries.Clear();
            _allEntries.AddRange(entries);
            _draftEntries.Clear();
            _modifiedEntries.Clear();
            _visibleEntries.Clear();
            _visibleDraftCount = 0;
            foreach (PreviewTranslationEntry entry in _allEntries)
            {
                entry.PropertyChanged += EntryPropertyChanged;
                UpdateTrackedState(entry);
            }

            TypeFilters.Clear();
            TypeFilters.Add(PreviewMessageCatalog.Get("Workspace_Filter_AllTypes"));
            foreach (string type in _allEntries.Select(entry => entry.Type)
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(type => type, StringComparer.OrdinalIgnoreCase))
            {
                TypeFilters.Add(type);
            }

            SelectedTypeFilter = TypeFilters[0];
            RefreshFilter();
            Tools.RefreshProjectState();
            OnPropertyChanged(nameof(HasProject));
            RaiseCommandAvailability();
            Tools?.RefreshPreview();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Selects an entry in the translation workspace and clears filters that could hide it.
        /// </summary>
        /// <param name="entry">The project entry to reveal.</param>
        internal void RevealEntry(PreviewTranslationEntry entry)
        {
            if (entry == null || !_allEntries.Contains(entry))
            {
                return;
            }

            _searchText = string.Empty;
            _selectedStateFilter = StateFilters[0];
            _selectedTypeFilter = TypeFilters[0];
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStateFilter));
            OnPropertyChanged(nameof(SelectedTypeFilter));
            RefreshFilter();
            SelectedEntry = entry;
            _shell.CurrentDestination = PreviewShellDestination.Translate;
        }

        private bool MatchesFilter(PreviewTranslationEntry entry)
        {
            if (_selectedStateFilter.Value == PreviewTranslationStateFilter.Draft && !entry.IsDraft ||
                _selectedStateFilter.Value == PreviewTranslationStateFilter.Translated && entry.IsDraft ||
                _selectedStateFilter.Value == PreviewTranslationStateFilter.Modified && !entry.IsModified)
            {
                return false;
            }

            if (!string.Equals(_selectedTypeFilter, TypeFilters[0], StringComparison.Ordinal) &&
                !string.Equals(entry.Type, _selectedTypeFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return true;
            }

            return Contains(entry.SourceText, _searchText) ||
                Contains(entry.TargetText, _searchText) ||
                Contains(entry.Record, _searchText) ||
                Contains(entry.Type, _searchText);
        }

        private static bool Contains(string value, string searchText)
        {
            return value != null && value.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewTranslationEntry.TargetText))
            {
                UpdateTrackedState((PreviewTranslationEntry)sender);
                OnEntryStateChanged();
            }
            else if (e.PropertyName == nameof(PreviewTranslationEntry.ReviewState))
            {
                Tools.RefreshReadiness();
            }
        }

        private void UpdateTrackedState(PreviewTranslationEntry entry)
        {
            bool wasDraft = _draftEntries.Contains(entry);
            if (entry.IsDraft)
            {
                _draftEntries.Add(entry);
            }
            else
            {
                _draftEntries.Remove(entry);
            }

            if (_visibleEntries.Contains(entry) && wasDraft != entry.IsDraft)
            {
                _visibleDraftCount += entry.IsDraft ? 1 : -1;
            }

            if (entry.IsModified)
            {
                _modifiedEntries.Add(entry);
            }
            else
            {
                _modifiedEntries.Remove(entry);
            }
        }

        private void OnEntryStateChanged()
        {
            _shell.SetProject(_project?.DisplayName, IsModified);
            OnPropertyChanged(nameof(DraftCountText));
            OnPropertyChanged(nameof(IsModified));
            RaiseCommandAvailability();
            if (_selectedStateFilter.Value != PreviewTranslationStateFilter.All ||
                !string.IsNullOrWhiteSpace(_searchText))
            {
                RefreshFilter();
            }
        }

        private void BeginOperation(string message, double progress)
        {
            IsBusy = true;
            OperationText = message;
            OperationProgress = progress;
            _shell.SetOperationText(message, progress);
        }

        private void UpdateOperation(string message, double progress)
        {
            OperationText = message;
            OperationProgress = progress;
            _shell.SetOperationText(message, progress);
        }

        private void CompleteOperation()
        {
            IsBusy = false;
            OperationText = PreviewMessageCatalog.Get("Common_State_Ready");
            OperationProgress = 0;
            _shell.CompleteOperation();
        }

        private void FailOperation(string messageId)
        {
            _shell.ShowNotification(PreviewShellNotificationSeverity.Error, messageId);
            CompleteOperation();
        }

        private void RaiseCommandAvailability()
        {
            RaiseCanExecuteChanged(SaveCommand);
            RaiseCanExecuteChanged(TranslateEntryCommand);
            RaiseCanExecuteChanged(TranslateScopeCommand);
            RaiseCanExecuteChanged(CancelOperationCommand);
            RaiseCanExecuteChanged(PreviousEntryCommand);
            RaiseCanExecuteChanged(ApplyEntryCommand);
        }

        private void OnToolsBusyChanged(bool isBusy)
        {
            RaiseCommandAvailability();
        }

        private static void RaiseCanExecuteChanged(ICommand command)
        {
            var previewCommand = command as PreviewShellCommand;
            previewCommand?.RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
