using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Describes one localized revision-comparison filter.
    /// </summary>
    internal sealed class PreviewComparisonFilterOption
    {
        /// <summary>
        /// Creates a localized filter option.
        /// </summary>
        /// <param name="value">The optional comparison state, or <see langword="null"/> for all states.</param>
        /// <param name="label">The localized visible label.</param>
        internal PreviewComparisonFilterOption(PreviewRevisionComparisonState? value, string label)
        {
            Value = value;
            Label = label;
        }

        /// <summary>Gets the optional comparison state.</summary>
        public PreviewRevisionComparisonState? Value { get; private set; }

        /// <summary>Gets the localized visible label.</summary>
        public string Label { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Owns the preview history timeline and explicit project-revision update workflow.
    /// </summary>
    internal sealed class PreviewHistoryUpdateViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PreviewShellViewModel _shell;
        private readonly PreviewTranslationWorkspaceViewModel _workspace;
        private readonly PreviewProjectComparisonService _comparisonService;
        private readonly PreviewRevisionHistoryStore _historyStore;
        private readonly Func<string> _chooseRevisionPath;
        private readonly Func<string, IPreviewTranslationProject> _openProject;
        private readonly Func<int, bool> _confirmConflictReuse;
        private readonly Func<int, bool> _confirmBulkReuse;
        private readonly Action _openLegacyWorkspace;
        private IReadOnlyList<PreviewProjectComparisonItem> _allComparisonItems;
        private IPreviewTranslationProject _previousProject;
        private PreviewComparisonFilterOption _selectedFilter;
        private PreviewProjectComparisonItem _selectedComparisonItem;
        private PreviewRevisionHistoryEntry _selectedHistoryEntry;
        private string _searchText;
        private string _previousRevisionName;
        private bool _isBusy;
        private List<UpdateSnapshot> _undoSnapshots;
        private CancellationTokenSource _comparisonCancellation;
        private readonly Func<bool> _confirmHistoryDelete;
        private readonly Func<int, bool> _confirmHistoryClear;
        private IReadOnlyList<PreviewTranslationHistoryItem> _allTranslationHistory;
        private PreviewTranslationHistoryItem _selectedTranslationHistoryItem;
        private string _translationHistorySearchText;
        private bool _isHistoryBusy;
        private CancellationTokenSource _historyCancellation;

        /// <summary>
        /// Creates the combined history and project-update workflow.
        /// </summary>
        /// <param name="shell">The persistent shell status owner.</param>
        /// <param name="workspace">The active translation workspace.</param>
        /// <param name="comparisonService">The stable-identity comparison service.</param>
        /// <param name="historyStore">The privacy-preserving event store.</param>
        /// <param name="chooseRevisionPath">Selects a previous revision path.</param>
        /// <param name="openProject">Opens a selected revision through the parser boundary.</param>
        /// <param name="confirmConflictReuse">Confirms replacement of conflicting targets.</param>
        /// <param name="confirmBulkReuse">Confirms reuse of a safe visible scope.</param>
        /// <param name="openLegacyWorkspace">Opens the complete legacy fallback.</param>
        /// <param name="confirmHistoryDelete">Confirms deletion of one selected history record.</param>
        /// <param name="confirmHistoryClear">Confirms clearing the exact number of history records.</param>
        internal PreviewHistoryUpdateViewModel(
            PreviewShellViewModel shell,
            PreviewTranslationWorkspaceViewModel workspace,
            PreviewProjectComparisonService comparisonService,
            PreviewRevisionHistoryStore historyStore,
            Func<string> chooseRevisionPath,
            Func<string, IPreviewTranslationProject> openProject,
            Func<int, bool> confirmConflictReuse,
            Func<int, bool> confirmBulkReuse,
            Action openLegacyWorkspace,
            Func<bool> confirmHistoryDelete = null,
            Func<int, bool> confirmHistoryClear = null)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
            _chooseRevisionPath = chooseRevisionPath ?? throw new ArgumentNullException(nameof(chooseRevisionPath));
            _openProject = openProject ?? throw new ArgumentNullException(nameof(openProject));
            _confirmConflictReuse = confirmConflictReuse ?? throw new ArgumentNullException(nameof(confirmConflictReuse));
            _confirmBulkReuse = confirmBulkReuse ?? throw new ArgumentNullException(nameof(confirmBulkReuse));
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));
            _confirmHistoryDelete = confirmHistoryDelete ?? (() => false);
            _confirmHistoryClear = confirmHistoryClear ?? (count => false);
            _searchText = string.Empty;
            _translationHistorySearchText = string.Empty;
            _previousRevisionName = string.Empty;
            _allComparisonItems = new List<PreviewProjectComparisonItem>();
            ComparisonItems = _allComparisonItems;
            HistoryEntries = new List<PreviewRevisionHistoryEntry>();
            _allTranslationHistory = new List<PreviewTranslationHistoryItem>();
            TranslationHistoryEntries = _allTranslationHistory;
            Filters = new[]
            {
                CreateFilter(null, "Update_Filter_All"),
                CreateFilter(PreviewRevisionComparisonState.Reusable, "Update_State_Reusable"),
                CreateFilter(PreviewRevisionComparisonState.Conflict, "Update_State_Conflict"),
                CreateFilter(PreviewRevisionComparisonState.Changed, "Update_State_Changed"),
                CreateFilter(PreviewRevisionComparisonState.Added, "Update_State_Added"),
                CreateFilter(PreviewRevisionComparisonState.Removed, "Update_State_Removed"),
                CreateFilter(PreviewRevisionComparisonState.Unchanged, "Update_State_Unchanged")
            };
            _selectedFilter = Filters[0];

            OpenProjectCommand = workspace.OpenProjectCommand;
            CompareRevisionCommand = new PreviewShellCommand(parameter => CompareSelectedRevision(), parameter => HasProject && !IsBusy);
            CancelComparisonCommand = new PreviewShellCommand(
                parameter => CancelComparison(),
                parameter => IsBusy && _comparisonCancellation != null);
            ReuseSelectedCommand = new PreviewShellCommand(parameter => ReuseSelected(), parameter => CanReuseSelected && !IsBusy);
            ReuseVisibleCommand = new PreviewShellCommand(parameter => ReuseVisible(), parameter => ReusableVisibleCount > 0 && !IsBusy);
            KeepCurrentCommand = new PreviewShellCommand(parameter => KeepCurrent(), parameter => IsSelectedConflict && !IsBusy);
            UndoCommand = new PreviewShellCommand(parameter => UndoLastUpdate(), parameter => CanUndo && !IsBusy);
            GoToEntryCommand = new PreviewShellCommand(parameter => GoToSelectedEntry(), parameter => SelectedComparisonItem?.CurrentEntry != null);
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());
            RefreshTranslationHistoryCommand = new PreviewShellCommand(
                parameter => ReloadTranslationHistory(), parameter => HasProject && !IsHistoryBusy);
            RestoreTranslationHistoryCommand = new PreviewShellCommand(
                parameter => RestoreTranslationHistory(), parameter => SelectedTranslationHistoryItem != null && !IsHistoryBusy);
            SetCurrentTranslationHistoryCommand = new PreviewShellCommand(
                parameter => SetCurrentTranslationHistory(), parameter => SelectedTranslationHistoryItem != null && !IsHistoryBusy);
            DeleteTranslationHistoryCommand = new PreviewShellCommand(
                parameter => DeleteTranslationHistory(), parameter => SelectedTranslationHistoryItem != null && !IsHistoryBusy);
            ClearTranslationHistoryCommand = new PreviewShellCommand(
                parameter => ClearTranslationHistory(), parameter => _allTranslationHistory.Count > 0 && !IsHistoryBusy);

            _workspace.ProjectChanged += WorkspaceProjectChanged;
            _shell.PropertyChanged += ShellPropertyChanged;
            ReloadHistory();
            ReloadTranslationHistory();
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the localized comparison filters.</summary>
        public IReadOnlyList<PreviewComparisonFilterOption> Filters { get; private set; }

        /// <summary>Gets the filtered comparison results.</summary>
        public IReadOnlyList<PreviewProjectComparisonItem> ComparisonItems { get; private set; }

        /// <summary>Gets the newest-first project history events.</summary>
        public IReadOnlyList<PreviewRevisionHistoryEntry> HistoryEntries { get; private set; }

        /// <summary>Gets filtered content-bearing engine translation history.</summary>
        public IReadOnlyList<PreviewTranslationHistoryItem> TranslationHistoryEntries { get; private set; }

        /// <summary>Gets the command that opens an active project.</summary>
        public ICommand OpenProjectCommand { get; private set; }

        /// <summary>Gets the command that selects and compares a previous revision.</summary>
        public ICommand CompareRevisionCommand { get; private set; }

        /// <summary>Gets the command that cooperatively cancels an active revision comparison.</summary>
        public ICommand CancelComparisonCommand { get; private set; }

        /// <summary>Gets the command that explicitly reuses the selected prior target.</summary>
        public ICommand ReuseSelectedCommand { get; private set; }

        /// <summary>Gets the command that reuses only visible safe candidates.</summary>
        public ICommand ReuseVisibleCommand { get; private set; }

        /// <summary>Gets the command that records retention of a conflicting current target.</summary>
        public ICommand KeepCurrentCommand { get; private set; }

        /// <summary>Gets the command that restores the most recent reuse action.</summary>
        public ICommand UndoCommand { get; private set; }

        /// <summary>Gets the command that reveals the selected current entry.</summary>
        public ICommand GoToEntryCommand { get; private set; }

        /// <summary>Gets the command that opens the complete legacy workflow.</summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>Gets the command that reloads engine translation history.</summary>
        public ICommand RefreshTranslationHistoryCommand { get; private set; }
        /// <summary>Gets the command that stages the selected historical target.</summary>
        public ICommand RestoreTranslationHistoryCommand { get; private set; }
        /// <summary>Gets the command that marks the selected history row current.</summary>
        public ICommand SetCurrentTranslationHistoryCommand { get; private set; }
        /// <summary>Gets the command that deletes the selected history row after confirmation.</summary>
        public ICommand DeleteTranslationHistoryCommand { get; private set; }
        /// <summary>Gets the command that clears all active-project history after confirmation.</summary>
        public ICommand ClearTranslationHistoryCommand { get; private set; }

        /// <summary>Gets whether an active project is available.</summary>
        public bool HasProject => !string.IsNullOrWhiteSpace(_workspace.ProjectPath);

        /// <summary>Gets whether project history contains events.</summary>
        public bool HasHistory => HistoryEntries.Count > 0;

        /// <summary>Gets whether a revision comparison is available.</summary>
        public bool HasComparison => _allComparisonItems.Count > 0;

        /// <summary>Gets whether the shell currently displays project history.</summary>
        public bool IsHistoryMode => _shell.CurrentDestination == PreviewShellDestination.History;

        /// <summary>Gets whether the shell currently displays project updates.</summary>
        public bool IsProjectUpdateMode => _shell.CurrentDestination == PreviewShellDestination.ProjectUpdate;

        /// <summary>Gets whether a revision is being opened and compared.</summary>
        public bool IsBusy => _isBusy;

        /// <summary>Gets whether content-bearing history work is active.</summary>
        public bool IsHistoryBusy => _isHistoryBusy;

        /// <summary>Gets or sets the selected content-bearing history record.</summary>
        public PreviewTranslationHistoryItem SelectedTranslationHistoryItem
        {
            get => _selectedTranslationHistoryItem;
            set
            {
                if (ReferenceEquals(_selectedTranslationHistoryItem, value)) return;
                _selectedTranslationHistoryItem = value;
                OnPropertyChanged();
                RaiseCommandAvailability();
            }
        }

        /// <summary>Gets or sets text used to filter source, target, and entry identity in history.</summary>
        public string TranslationHistorySearchText
        {
            get => _translationHistorySearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_translationHistorySearchText, normalized, StringComparison.Ordinal)) return;
                _translationHistorySearchText = normalized;
                OnPropertyChanged();
                RefreshTranslationHistoryFilter();
            }
        }

        /// <summary>Gets whether the most recent reuse action can be restored.</summary>
        public bool CanUndo => _undoSnapshots != null && _undoSnapshots.Count > 0;

        /// <summary>Gets whether the selected prior target can be explicitly reused.</summary>
        public bool CanReuseSelected => SelectedComparisonItem != null && SelectedComparisonItem.CanReuse;

        /// <summary>Gets whether the selected result requires an explicit conflict decision.</summary>
        public bool IsSelectedConflict => SelectedComparisonItem?.State == PreviewRevisionComparisonState.Conflict;

        /// <summary>Gets the visible safe-reuse candidate count.</summary>
        public int ReusableVisibleCount => ComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Reusable);

        /// <summary>Gets the safe display name of the compared revision.</summary>
        public string PreviousRevisionName => _previousRevisionName;

        /// <summary>Gets the localized aggregate comparison counts.</summary>
        public string ComparisonSummary => PreviewMessageCatalog.Format(
            "Update_Comparison_Summary",
            _allComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Added),
            _allComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Removed),
            _allComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Changed),
            _allComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Reusable),
            _allComparisonItems.Count(item => item.State == PreviewRevisionComparisonState.Conflict));

        /// <summary>Gets or sets the selected comparison-state filter.</summary>
        public PreviewComparisonFilterOption SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (ReferenceEquals(_selectedFilter, value) || value == null)
                {
                    return;
                }

                _selectedFilter = value;
                OnPropertyChanged();
                RefreshComparisonFilter();
            }
        }

        /// <summary>Gets or sets the comparison search text.</summary>
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
                RefreshComparisonFilter();
            }
        }

        /// <summary>Gets or sets the comparison result shown in the detail pane.</summary>
        public PreviewProjectComparisonItem SelectedComparisonItem
        {
            get => _selectedComparisonItem;
            set
            {
                if (ReferenceEquals(_selectedComparisonItem, value))
                {
                    return;
                }

                _selectedComparisonItem = value;
                OnPropertyChanged();
                RaiseCommandAvailability();
                OnPropertyChanged(nameof(CanReuseSelected));
                OnPropertyChanged(nameof(IsSelectedConflict));
            }
        }

        /// <summary>Gets or sets the history event shown in the detail pane.</summary>
        public PreviewRevisionHistoryEntry SelectedHistoryEntry
        {
            get => _selectedHistoryEntry;
            set
            {
                if (ReferenceEquals(_selectedHistoryEntry, value))
                {
                    return;
                }

                _selectedHistoryEntry = value;
                OnPropertyChanged();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _comparisonCancellation?.Cancel();
            _historyCancellation?.Cancel();
            _workspace.ProjectChanged -= WorkspaceProjectChanged;
            _shell.PropertyChanged -= ShellPropertyChanged;
            _previousProject?.Dispose();
        }

        private async void CompareSelectedRevision()
        {
            string path = _chooseRevisionPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!string.Equals(Path.GetExtension(path), Path.GetExtension(_workspace.ProjectPath), StringComparison.OrdinalIgnoreCase))
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "Update_Comparison_Incompatible");
                return;
            }

            var cancellation = new CancellationTokenSource();
            _comparisonCancellation = cancellation;
            SetBusy(true);
            _shell.SetOperation("Update_Comparison_Progress", 20);
            IPreviewTranslationProject previousProject = null;
            try
            {
                previousProject = await Task.Run(() => _openProject(path), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                IReadOnlyList<PreviewTranslationEntry> currentEntries = _workspace.ProjectEntries;
                IReadOnlyList<PreviewProjectComparisonItem> comparison = await Task.Run(
                    () => _comparisonService.Compare(currentEntries, previousProject.Entries, cancellation.Token),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                _previousProject?.Dispose();
                _previousProject = previousProject;
                previousProject = null;
                _previousRevisionName = _previousProject.DisplayName;
                _allComparisonItems = comparison;
                SelectedComparisonItem = comparison.FirstOrDefault(item => item.State != PreviewRevisionComparisonState.Unchanged)
                    ?? comparison.FirstOrDefault();
                RefreshComparisonFilter();
                AppendProjectHistory("Compared", _previousRevisionName);
                _shell.ShowNotification(PreviewShellNotificationSeverity.Success, "Update_Comparison_Completed");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidDataException || exception is InvalidOperationException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Error, "Update_Comparison_Failed");
            }
            finally
            {
                previousProject?.Dispose();
                _shell.CompleteOperation();
                if (ReferenceEquals(_comparisonCancellation, cancellation))
                {
                    _comparisonCancellation = null;
                }
                cancellation.Dispose();
                SetBusy(false);
            }
        }

        private void CancelComparison()
        {
            _comparisonCancellation?.Cancel();
        }

        private void ReuseSelected()
        {
            PreviewProjectComparisonItem item = SelectedComparisonItem;
            if (item == null || !item.CanReuse)
            {
                return;
            }

            if (item.State == PreviewRevisionComparisonState.Conflict && !_confirmConflictReuse(1))
            {
                return;
            }

            ApplyReuse(new[] { item });
        }

        private void ReuseVisible()
        {
            List<PreviewProjectComparisonItem> reusable = ComparisonItems
                .Where(item => item.State == PreviewRevisionComparisonState.Reusable && item.CanReuse).ToList();
            if (reusable.Count == 0 || !_confirmBulkReuse(reusable.Count))
            {
                return;
            }

            ApplyReuse(reusable);
        }

        private void ApplyReuse(IEnumerable<PreviewProjectComparisonItem> items)
        {
            List<PreviewProjectComparisonItem> selectedItems = items.ToList();
            _undoSnapshots = selectedItems.Select(item => new UpdateSnapshot(
                item.CurrentEntry,
                item.CurrentEntry.TargetText,
                item.CurrentEntry.ReviewState,
                item.PreviousTargetText)).ToList();
            foreach (UpdateSnapshot snapshot in _undoSnapshots)
            {
                PreviewProjectComparisonItem item = selectedItems.First(
                    candidate => ReferenceEquals(candidate.CurrentEntry, snapshot.Entry));
                snapshot.Entry.ApplyReusedTarget(snapshot.ReusedTargetText);
                AppendEntryHistory(
                    item.State == PreviewRevisionComparisonState.Conflict ? "ConflictResolved" : "Reused",
                    snapshot.Entry);
            }

            RefreshComparison();
            _shell.ShowNotification(PreviewShellNotificationSeverity.Success, "Update_Reuse_Completed", _undoSnapshots.Count);
        }

        private void KeepCurrent()
        {
            if (!IsSelectedConflict)
            {
                return;
            }

            AppendEntryHistory("KeptCurrent", SelectedComparisonItem.CurrentEntry);
            _shell.ShowNotification(PreviewShellNotificationSeverity.Information, "Update_KeepCurrent_Completed");
        }

        private void UndoLastUpdate()
        {
            if (!CanUndo)
            {
                return;
            }

            foreach (UpdateSnapshot snapshot in _undoSnapshots)
            {
                snapshot.Entry.TargetText = snapshot.TargetText;
                snapshot.Entry.SetReviewState(snapshot.ReviewState);
                AppendEntryHistory("Undone", snapshot.Entry);
            }

            int restoredCount = _undoSnapshots.Count;
            _undoSnapshots = null;
            RefreshComparison();
            _shell.ShowNotification(PreviewShellNotificationSeverity.Information, "Update_Undo_Completed", restoredCount);
        }

        private void GoToSelectedEntry()
        {
            PreviewTranslationEntry entry = SelectedComparisonItem?.CurrentEntry;
            if (entry == null)
            {
                return;
            }

            _workspace.RevealEntry(entry);
            _shell.CurrentDestination = PreviewShellDestination.Translate;
        }

        private void WorkspaceProjectChanged(object sender, EventArgs e)
        {
            _previousProject?.Dispose();
            _previousProject = null;
            _previousRevisionName = string.Empty;
            _allComparisonItems = new List<PreviewProjectComparisonItem>();
            ComparisonItems = _allComparisonItems;
            SelectedComparisonItem = null;
            _undoSnapshots = null;
            ReloadHistory();
            ReloadTranslationHistory();
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(HasComparison));
            OnPropertyChanged(nameof(PreviousRevisionName));
            OnPropertyChanged(nameof(ComparisonSummary));
            RaiseCommandAvailability();
        }

        private void ShellPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewShellViewModel.CurrentDestination))
            {
                OnPropertyChanged(nameof(IsHistoryMode));
                OnPropertyChanged(nameof(IsProjectUpdateMode));
            }
        }

        private void ReloadHistory()
        {
            try
            {
                HistoryEntries = HasProject
                    ? _historyStore.Load(_workspace.ProjectPath)
                    : new List<PreviewRevisionHistoryEntry>();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                HistoryEntries = new List<PreviewRevisionHistoryEntry>();
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "History_Load_Failed");
            }

            SelectedHistoryEntry = HistoryEntries.FirstOrDefault();
            OnPropertyChanged(nameof(HistoryEntries));
            OnPropertyChanged(nameof(HasHistory));
        }

        private async void ReloadTranslationHistory()
        {
            _historyCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _historyCancellation = cancellation;
            SetHistoryBusy(true);
            try
            {
                _allTranslationHistory = await _workspace.LoadTranslationHistoryAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                RefreshTranslationHistoryFilter();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidOperationException || exception is ArgumentException)
            {
                _allTranslationHistory = new List<PreviewTranslationHistoryItem>();
                RefreshTranslationHistoryFilter();
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "TranslationHistory_Load_Failed");
            }
            finally
            {
                if (ReferenceEquals(_historyCancellation, cancellation))
                {
                    _historyCancellation = null;
                    SetHistoryBusy(false);
                }
                cancellation.Dispose();
            }
        }

        private async void RestoreTranslationHistory()
        {
            PreviewTranslationHistoryItem item = SelectedTranslationHistoryItem;
            if (item == null) return;
            bool reload = false;
            SetHistoryBusy(true);
            try
            {
                PreviewTranslationEntry restored = await _workspace.RestoreTranslationHistoryAsync(
                    item.RowId, CancellationToken.None);
                if (restored != null)
                {
                    _shell.ShowNotification(PreviewShellNotificationSeverity.Success, "TranslationHistory_Restored");
                    reload = true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Error, "TranslationHistory_Operation_Failed");
            }
            finally
            {
                SetHistoryBusy(false);
            }
            if (reload) ReloadTranslationHistory();
        }

        private async void SetCurrentTranslationHistory()
        {
            PreviewTranslationHistoryItem item = SelectedTranslationHistoryItem;
            if (item == null) return;
            await ExecuteHistoryMutation(
                () => _workspace.SetCurrentTranslationHistoryAsync(item.RowId),
                "TranslationHistory_Current_Set");
        }

        private async void DeleteTranslationHistory()
        {
            PreviewTranslationHistoryItem item = SelectedTranslationHistoryItem;
            if (item == null || !_confirmHistoryDelete()) return;
            await ExecuteHistoryMutation(
                () => _workspace.DeleteTranslationHistoryAsync(item.RowId),
                "TranslationHistory_Deleted");
        }

        private async void ClearTranslationHistory()
        {
            int count = _allTranslationHistory.Count;
            if (count == 0 || !_confirmHistoryClear(count)) return;
            await ExecuteHistoryMutation(
                () => _workspace.ClearTranslationHistoryAsync(),
                "TranslationHistory_Cleared");
        }

        private async Task ExecuteHistoryMutation(Func<Task> operation, string successMessageId)
        {
            bool reload = false;
            SetHistoryBusy(true);
            try
            {
                await operation();
                _shell.ShowNotification(PreviewShellNotificationSeverity.Success, successMessageId);
                reload = true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Error, "TranslationHistory_Operation_Failed");
            }
            finally
            {
                SetHistoryBusy(false);
            }
            if (reload) ReloadTranslationHistory();
        }

        private void RefreshTranslationHistoryFilter()
        {
            PreviewTranslationHistoryItem previous = SelectedTranslationHistoryItem;
            IEnumerable<PreviewTranslationHistoryItem> query = _allTranslationHistory;
            if (!string.IsNullOrWhiteSpace(_translationHistorySearchText))
            {
                query = query.Where(item => Contains(item.EntryKey, _translationHistorySearchText) ||
                    Contains(item.SourceText, _translationHistorySearchText) ||
                    Contains(item.TargetText, _translationHistorySearchText));
            }

            TranslationHistoryEntries = query.Reverse().ToList();
            OnPropertyChanged(nameof(TranslationHistoryEntries));
            SelectedTranslationHistoryItem = TranslationHistoryEntries.Contains(previous)
                ? previous
                : TranslationHistoryEntries.FirstOrDefault();
            RaiseCommandAvailability();
        }

        private void SetHistoryBusy(bool value)
        {
            if (_isHistoryBusy == value) return;
            _isHistoryBusy = value;
            OnPropertyChanged(nameof(IsHistoryBusy));
            RaiseCommandAvailability();
        }

        private void AppendEntryHistory(string actionId, PreviewTranslationEntry entry)
        {
            if (!HasProject || entry == null)
            {
                return;
            }

            TryAppend(PreviewRevisionHistoryStore.Create(actionId, entry, DateTime.UtcNow));
        }

        private void AppendProjectHistory(string actionId, string record)
        {
            if (!HasProject)
            {
                return;
            }

            TryAppend(new PreviewRevisionHistoryEntry(
                DateTime.UtcNow, actionId, "project", record, string.Empty, string.Empty, string.Empty));
        }

        private void TryAppend(PreviewRevisionHistoryEntry entry)
        {
            try
            {
                _historyStore.Append(_workspace.ProjectPath, entry);
                ReloadHistory();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "History_Save_Failed");
            }
        }

        private void RefreshComparison()
        {
            if (_previousProject == null)
            {
                return;
            }

            string selectedKey = SelectedComparisonItem?.Key;
            _allComparisonItems = _comparisonService.Compare(_workspace.ProjectEntries, _previousProject.Entries);
            RefreshComparisonFilter();
            SelectedComparisonItem = _allComparisonItems.FirstOrDefault(item => item.Key == selectedKey)
                ?? ComparisonItems.FirstOrDefault();
        }

        private void RefreshComparisonFilter()
        {
            IEnumerable<PreviewProjectComparisonItem> query = _allComparisonItems;
            if (_selectedFilter?.Value != null)
            {
                query = query.Where(item => item.State == _selectedFilter.Value.Value);
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                query = query.Where(item => Contains(item.Key, _searchText) || Contains(item.Record, _searchText) ||
                    Contains(item.CurrentSourceText, _searchText) || Contains(item.PreviousSourceText, _searchText) ||
                    Contains(item.CurrentTargetText, _searchText) || Contains(item.PreviousTargetText, _searchText));
            }

            ComparisonItems = query.ToList();
            OnPropertyChanged(nameof(ComparisonItems));
            OnPropertyChanged(nameof(HasComparison));
            OnPropertyChanged(nameof(ComparisonSummary));
            OnPropertyChanged(nameof(ReusableVisibleCount));
            RaiseCommandAvailability();
        }

        private void SetBusy(bool value)
        {
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            RaiseCommandAvailability();
        }

        private void RaiseCommandAvailability()
        {
            ((PreviewShellCommand)CompareRevisionCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)CancelComparisonCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)ReuseSelectedCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)ReuseVisibleCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)KeepCurrentCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)UndoCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)GoToEntryCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)RefreshTranslationHistoryCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)RestoreTranslationHistoryCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)SetCurrentTranslationHistoryCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)DeleteTranslationHistoryCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)ClearTranslationHistoryCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanUndo));
        }

        private static PreviewComparisonFilterOption CreateFilter(
            PreviewRevisionComparisonState? value,
            string messageId)
        {
            return new PreviewComparisonFilterOption(value, PreviewMessageCatalog.Get(messageId));
        }

        private static bool Contains(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class UpdateSnapshot
        {
            internal UpdateSnapshot(
                PreviewTranslationEntry entry,
                string targetText,
                PreviewReviewState reviewState,
                string reusedTargetText)
            {
                Entry = entry;
                TargetText = targetText;
                ReviewState = reviewState;
                ReusedTargetText = reusedTargetText;
            }

            internal PreviewTranslationEntry Entry { get; private set; }
            internal string TargetText { get; private set; }
            internal PreviewReviewState ReviewState { get; private set; }
            internal string ReusedTargetText { get; private set; }
        }
    }
}
