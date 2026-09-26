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
    /// Owns the combined human-review, project-validation, and export-readiness workflow.
    /// </summary>
    internal sealed class PreviewReviewQualityViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PreviewShellViewModel _shell;
        private readonly PreviewTranslationWorkspaceViewModel _translationWorkspace;
        private readonly PreviewQualityAnalyzer _analyzer;
        private readonly PreviewReviewStateStore _stateStore;
        private readonly Func<int, bool> _confirmBulkApproval;
        private readonly Action _openLegacyWorkspace;
        private readonly HashSet<string> _acknowledgedFindingIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<PreviewTranslationEntry> _subscribedEntries = new HashSet<PreviewTranslationEntry>();
        private Dictionary<PreviewTranslationEntry, PreviewReviewState> _lastBulkStates;
        private IReadOnlyList<PreviewQualityFinding> _allFindings = new List<PreviewQualityFinding>();
        private PreviewTranslationEntry _selectedEntry;
        private PreviewQualityFinding _selectedFinding;
        private string _searchText;
        private string _selectedReviewFilter;
        private string _selectedSeverityFilter;
        private string _selectedFindingTypeFilter;
        private CancellationTokenSource _validationCancellation;
        private bool _isValidating;

        /// <summary>
        /// Creates a review and quality workflow over the active translation workspace.
        /// </summary>
        /// <param name="shell">The persistent shell status owner.</param>
        /// <param name="translationWorkspace">The normalized project and navigation owner.</param>
        /// <param name="analyzer">The normalized quality analyzer.</param>
        /// <param name="stateStore">The privacy-preserving review metadata store.</param>
        /// <param name="confirmBulkApproval">Confirms the exact number of entries affected by a bulk approval.</param>
        /// <param name="openLegacyWorkspace">Opens the workflow-specific legacy fallback.</param>
        internal PreviewReviewQualityViewModel(
            PreviewShellViewModel shell,
            PreviewTranslationWorkspaceViewModel translationWorkspace,
            PreviewQualityAnalyzer analyzer,
            PreviewReviewStateStore stateStore,
            Func<int, bool> confirmBulkApproval,
            Action openLegacyWorkspace)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _translationWorkspace = translationWorkspace ?? throw new ArgumentNullException(nameof(translationWorkspace));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _confirmBulkApproval = confirmBulkApproval ?? throw new ArgumentNullException(nameof(confirmBulkApproval));
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));
            _searchText = string.Empty;

            ReviewFilters = new[]
            {
                PreviewMessageCatalog.Get("Review_Filter_AllStates"),
                PreviewMessageCatalog.Get("Review_State_Unreviewed"),
                PreviewMessageCatalog.Get("Review_State_Reviewed"),
                PreviewMessageCatalog.Get("Review_State_Approved"),
                PreviewMessageCatalog.Get("Review_State_Rejected")
            };
            SeverityFilters = new[]
            {
                PreviewMessageCatalog.Get("Quality_Filter_AllSeverities"),
                PreviewMessageCatalog.Get("Quality_Severity_Error"),
                PreviewMessageCatalog.Get("Quality_Severity_Warning"),
                PreviewMessageCatalog.Get("Quality_Severity_Information")
            };
            FindingTypeFilters = new List<string> { PreviewMessageCatalog.Get("Quality_Filter_AllFindings") };
            _selectedReviewFilter = ReviewFilters[0];
            _selectedSeverityFilter = SeverityFilters[0];
            _selectedFindingTypeFilter = FindingTypeFilters[0];
            ReviewEntries = new List<PreviewTranslationEntry>();
            Findings = new List<PreviewQualityFinding>();

            MarkReviewedCommand = new PreviewShellCommand(parameter => SetSelectedReviewState(PreviewReviewState.Reviewed), CanReviewSelected);
            ApproveCommand = new PreviewShellCommand(parameter => SetSelectedReviewState(PreviewReviewState.Approved), CanReviewSelected);
            RejectCommand = new PreviewShellCommand(parameter => SetSelectedReviewState(PreviewReviewState.Rejected), CanReviewSelected);
            ApproveScopeCommand = new PreviewShellCommand(parameter => ApproveScope(), parameter => ReviewEntries.Any(CanApprove));
            UndoBulkCommand = new PreviewShellCommand(parameter => UndoBulk(), parameter => _lastBulkStates != null);
            AcknowledgeFindingCommand = new PreviewShellCommand(parameter => AcknowledgeFinding(), CanAcknowledgeFinding);
            GoToEntryCommand = new PreviewShellCommand(parameter => GoToEntry(), parameter => SelectedFinding != null);
            RevalidateCommand = new PreviewShellCommand(
                parameter => StartValidation(0),
                parameter => HasProject && !IsValidating);
            CancelValidationCommand = new PreviewShellCommand(
                parameter => CancelValidation(),
                parameter => IsValidating);
            OpenProjectCommand = _translationWorkspace.OpenProjectCommand;
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());

            _shell.PropertyChanged += ShellPropertyChanged;
            _translationWorkspace.ProjectChanged += TranslationWorkspaceProjectChanged;
            LoadProject();
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the filtered entries in the review queue.
        /// </summary>
        public IReadOnlyList<PreviewTranslationEntry> ReviewEntries { get; private set; }

        /// <summary>
        /// Gets the filtered normalized validation findings.
        /// </summary>
        public IReadOnlyList<PreviewQualityFinding> Findings { get; private set; }

        /// <summary>
        /// Gets the localized review-state filter labels.
        /// </summary>
        public IReadOnlyList<string> ReviewFilters { get; private set; }

        /// <summary>
        /// Gets the localized severity filter labels.
        /// </summary>
        public IReadOnlyList<string> SeverityFilters { get; private set; }

        /// <summary>
        /// Gets the stable finding types present in the current project.
        /// </summary>
        public IReadOnlyList<string> FindingTypeFilters { get; private set; }

        /// <summary>
        /// Gets whether a project is available for review.
        /// </summary>
        public bool HasProject => _translationWorkspace.HasProject;

        /// <summary>
        /// Gets whether the shell selected human review.
        /// </summary>
        public bool IsReviewMode => _shell.CurrentDestination == PreviewShellDestination.Review;

        /// <summary>
        /// Gets whether the shell selected quality assurance.
        /// </summary>
        public bool IsQualityMode => _shell.CurrentDestination == PreviewShellDestination.Quality;

        /// <summary>
        /// Gets or sets the entry selected for review.
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
                RaiseCommandAvailability();
            }
        }

        /// <summary>
        /// Gets or sets the finding selected for inspection.
        /// </summary>
        public PreviewQualityFinding SelectedFinding
        {
            get => _selectedFinding;
            set
            {
                if (ReferenceEquals(_selectedFinding, value))
                {
                    return;
                }

                _selectedFinding = value;
                OnPropertyChanged();
                RaiseCommandAvailability();
            }
        }

        /// <summary>
        /// Gets or sets text that filters entries and findings by source, target, record, or component.
        /// </summary>
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
                RefreshFilters();
            }
        }

        /// <summary>
        /// Gets or sets the active review-state filter.
        /// </summary>
        public string SelectedReviewFilter
        {
            get => _selectedReviewFilter;
            set
            {
                if (string.IsNullOrEmpty(value) || string.Equals(_selectedReviewFilter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedReviewFilter = value;
                OnPropertyChanged();
                RefreshFilters();
            }
        }

        /// <summary>
        /// Gets or sets the active finding-severity filter.
        /// </summary>
        public string SelectedSeverityFilter
        {
            get => _selectedSeverityFilter;
            set
            {
                if (string.IsNullOrEmpty(value) || string.Equals(_selectedSeverityFilter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedSeverityFilter = value;
                OnPropertyChanged();
                RefreshFilters();
            }
        }

        /// <summary>
        /// Gets or sets the active stable finding-type filter.
        /// </summary>
        public string SelectedFindingTypeFilter
        {
            get => _selectedFindingTypeFilter;
            set
            {
                if (string.IsNullOrEmpty(value) || string.Equals(_selectedFindingTypeFilter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedFindingTypeFilter = value;
                OnPropertyChanged();
                RefreshFilters();
            }
        }

        /// <summary>
        /// Gets the localized number of visible review entries.
        /// </summary>
        public string ReviewCountText => PreviewMessageCatalog.Format("Review_EntryCount", ReviewEntries.Count);

        /// <summary>
        /// Gets the localized number of visible findings.
        /// </summary>
        public string FindingCountText => PreviewMessageCatalog.Format("Quality_FindingCount", Findings.Count);

        /// <summary>
        /// Gets the localized export-readiness result.
        /// </summary>
        public string ExportReadinessText
        {
            get
            {
                if (_allFindings.Any(finding => finding.IsBlocking))
                {
                    return PreviewMessageCatalog.Get("Quality_Readiness_Blocked");
                }

                bool hasOpenWarnings = _allFindings.Any(finding =>
                    finding.Severity == PreviewFindingSeverity.Warning &&
                    finding.Resolution == PreviewFindingResolution.Open);
                if (hasOpenWarnings)
                {
                    return PreviewMessageCatalog.Get("Quality_Readiness_WarningsRequireReview");
                }

                return _allFindings.Any(finding => finding.Resolution == PreviewFindingResolution.Acknowledged)
                    ? PreviewMessageCatalog.Get("Quality_Readiness_AcknowledgedWarnings")
                    : PreviewMessageCatalog.Get("Quality_Readiness_Ready");
            }
        }

        /// <summary>
        /// Gets whether export readiness currently contains blocking findings.
        /// </summary>
        public bool IsExportBlocked => _allFindings.Any(finding => finding.IsBlocking);

        /// <summary>
        /// Gets the command that records inspection of the selected target.
        /// </summary>
        public ICommand MarkReviewedCommand { get; private set; }

        /// <summary>
        /// Gets the command that approves the selected target.
        /// </summary>
        public ICommand ApproveCommand { get; private set; }

        /// <summary>
        /// Gets the command that rejects the selected target for correction.
        /// </summary>
        public ICommand RejectCommand { get; private set; }

        /// <summary>
        /// Gets the command that confirms and approves every eligible entry in the visible scope.
        /// </summary>
        public ICommand ApproveScopeCommand { get; private set; }

        /// <summary>
        /// Gets the command that restores decisions changed by the most recent bulk approval.
        /// </summary>
        public ICommand UndoBulkCommand { get; private set; }

        /// <summary>
        /// Gets the command that acknowledges the selected non-blocking finding.
        /// </summary>
        public ICommand AcknowledgeFindingCommand { get; private set; }

        /// <summary>
        /// Gets the command that reveals the selected finding's exact translation entry.
        /// </summary>
        public ICommand GoToEntryCommand { get; private set; }

        /// <summary>
        /// Gets the command that rebuilds normalized findings without changing content.
        /// </summary>
        public ICommand RevalidateCommand { get; private set; }

        /// <summary>
        /// Gets the command that cooperatively cancels the active quality validation.
        /// </summary>
        public ICommand CancelValidationCommand { get; private set; }

        /// <summary>
        /// Gets the shared command that opens a supported translation project.
        /// </summary>
        public ICommand OpenProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command that opens the complete legacy workflow.
        /// </summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>
        /// Gets whether quality validation is running outside the UI thread.
        /// </summary>
        public bool IsValidating
        {
            get => _isValidating;
            private set
            {
                if (_isValidating == value)
                {
                    return;
                }

                _isValidating = value;
                OnPropertyChanged();
                RaiseCommandAvailability();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _validationCancellation?.Cancel();
            _validationCancellation = null;
            _shell.PropertyChanged -= ShellPropertyChanged;
            _translationWorkspace.ProjectChanged -= TranslationWorkspaceProjectChanged;
            UnsubscribeEntries();
        }

        private void TranslationWorkspaceProjectChanged(object sender, EventArgs e)
        {
            LoadProject();
        }

        private void ShellPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewShellViewModel.CurrentDestination))
            {
                OnPropertyChanged(nameof(IsReviewMode));
                OnPropertyChanged(nameof(IsQualityMode));
            }
        }

        private void LoadProject()
        {
            UnsubscribeEntries();
            _acknowledgedFindingIds.Clear();
            _lastBulkStates = null;
            if (HasProject)
            {
                PreviewReviewStateSnapshot snapshot = _stateStore.Load(_translationWorkspace.ProjectPath);
                foreach (PreviewTranslationEntry entry in _translationWorkspace.ProjectEntries)
                {
                    PreviewReviewDecision decision;
                    if (snapshot.Decisions.TryGetValue(entry.Key, out decision) &&
                        string.Equals(decision.TargetFingerprint, PreviewReviewStateStore.Fingerprint(entry.TargetText), StringComparison.Ordinal))
                    {
                        entry.SetReviewState(decision.State);
                    }

                    entry.PropertyChanged += EntryPropertyChanged;
                    _subscribedEntries.Add(entry);
                }

                _acknowledgedFindingIds.UnionWith(snapshot.AcknowledgedFindingIds);
            }

            StartValidation(0);
            OnPropertyChanged(nameof(HasProject));
            RaiseCommandAvailability();
        }

        private void UnsubscribeEntries()
        {
            foreach (PreviewTranslationEntry entry in _subscribedEntries)
            {
                entry.PropertyChanged -= EntryPropertyChanged;
            }

            _subscribedEntries.Clear();
        }

        private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewTranslationEntry.TargetText))
            {
                StartValidation(150);
                PersistState();
            }
            else if (e.PropertyName == nameof(PreviewTranslationEntry.ReviewState))
            {
                RefreshFilters();
            }
        }

        /// <summary>
        /// Rebuilds normalized findings on a worker thread and discards stale results.
        /// </summary>
        /// <param name="delayMilliseconds">The debounce delay before validation starts.</param>
        /// <returns>A task that completes when this validation request is applied or superseded.</returns>
        internal async Task RevalidateAsync(int delayMilliseconds = 0)
        {
            _validationCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _validationCancellation = cancellation;
            IsValidating = true;

            try
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds, cancellation.Token);
                }

                IReadOnlyList<PreviewTranslationEntry> entries = HasProject
                    ? _translationWorkspace.ProjectEntries.ToList()
                    : new List<PreviewTranslationEntry>();
                IReadOnlyList<PreviewQualityFinding> findings = await Task.Run(
                    () => _analyzer.Analyze(entries, cancellation.Token),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                _allFindings = findings;
                ApplyFindings();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(_validationCancellation, cancellation))
                {
                    _validationCancellation = null;
                    IsValidating = false;
                }

                cancellation.Dispose();
            }
        }

        private async void StartValidation(int delayMilliseconds)
        {
            await RevalidateAsync(delayMilliseconds);
        }

        private void CancelValidation()
        {
            _validationCancellation?.Cancel();
        }

        private void ApplyFindings()
        {
            foreach (PreviewQualityFinding finding in _allFindings.Where(finding =>
                _acknowledgedFindingIds.Contains(finding.StableId)))
            {
                finding.Acknowledge();
            }

            var types = new List<string> { PreviewMessageCatalog.Get("Quality_Filter_AllFindings") };
            types.AddRange(_allFindings.Select(finding => finding.Title)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
            FindingTypeFilters = types;
            if (!FindingTypeFilters.Contains(_selectedFindingTypeFilter))
            {
                _selectedFindingTypeFilter = FindingTypeFilters[0];
                OnPropertyChanged(nameof(SelectedFindingTypeFilter));
            }

            OnPropertyChanged(nameof(FindingTypeFilters));
            _shell.SetWarningCount(_allFindings.Count(finding =>
                finding.Severity == PreviewFindingSeverity.Warning &&
                finding.Resolution == PreviewFindingResolution.Open));
            RefreshFilters();
            OnPropertyChanged(nameof(ExportReadinessText));
            OnPropertyChanged(nameof(IsExportBlocked));
        }

        private void RefreshFilters()
        {
            PreviewTranslationEntry previousEntry = SelectedEntry;
            PreviewQualityFinding previousFinding = SelectedFinding;
            ReviewEntries = _translationWorkspace.ProjectEntries.Where(MatchesReviewFilter).ToList();
            Findings = _allFindings.Where(MatchesFindingFilter).ToList();
            SelectedEntry = ReviewEntries.Contains(previousEntry) ? previousEntry : ReviewEntries.FirstOrDefault();
            SelectedFinding = Findings.Contains(previousFinding) ? previousFinding : Findings.FirstOrDefault();
            OnPropertyChanged(nameof(ReviewEntries));
            OnPropertyChanged(nameof(Findings));
            OnPropertyChanged(nameof(ReviewCountText));
            OnPropertyChanged(nameof(FindingCountText));
            RaiseCommandAvailability();
        }

        private bool MatchesReviewFilter(PreviewTranslationEntry entry)
        {
            if (!string.Equals(_selectedReviewFilter, ReviewFilters[0], StringComparison.Ordinal) &&
                !string.Equals(entry.ReviewStateText, _selectedReviewFilter, StringComparison.Ordinal))
            {
                return false;
            }

            return MatchesSearch(entry, null);
        }

        private bool MatchesFindingFilter(PreviewQualityFinding finding)
        {
            if (!string.Equals(_selectedSeverityFilter, SeverityFilters[0], StringComparison.Ordinal) &&
                !string.Equals(finding.SeverityText, _selectedSeverityFilter, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(_selectedFindingTypeFilter, FindingTypeFilters[0], StringComparison.Ordinal) &&
                !string.Equals(finding.Title, _selectedFindingTypeFilter, StringComparison.Ordinal))
            {
                return false;
            }

            return MatchesSearch(finding.Entry, finding.Source) || Contains(finding.Title, _searchText);
        }

        private bool MatchesSearch(PreviewTranslationEntry entry, string component)
        {
            return string.IsNullOrWhiteSpace(_searchText) || Contains(entry.SourceText, _searchText) ||
                Contains(entry.TargetText, _searchText) || Contains(entry.Record, _searchText) ||
                Contains(entry.Type, _searchText) || Contains(entry.Provenance, _searchText) ||
                Contains(component, _searchText);
        }

        private static bool Contains(string value, string search)
        {
            return value != null && value.IndexOf(search ?? string.Empty, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private bool CanReviewSelected(object parameter)
        {
            return CanApprove(SelectedEntry);
        }

        private static bool CanApprove(PreviewTranslationEntry entry)
        {
            return entry != null && !string.IsNullOrWhiteSpace(entry.TargetText);
        }

        private void SetSelectedReviewState(PreviewReviewState state)
        {
            if (!CanApprove(SelectedEntry))
            {
                return;
            }

            SelectedEntry.SetReviewState(state);
            PersistState();
        }

        private void ApproveScope()
        {
            List<PreviewTranslationEntry> scope = ReviewEntries.Where(CanApprove).ToList();
            if (scope.Count == 0 || !_confirmBulkApproval(scope.Count))
            {
                return;
            }

            _lastBulkStates = scope.ToDictionary(entry => entry, entry => entry.ReviewState);
            foreach (PreviewTranslationEntry entry in scope)
            {
                entry.SetReviewState(PreviewReviewState.Approved);
            }

            PersistState();
            RefreshFilters();
            RaiseCommandAvailability();
        }

        private void UndoBulk()
        {
            if (_lastBulkStates == null)
            {
                return;
            }

            foreach (KeyValuePair<PreviewTranslationEntry, PreviewReviewState> state in _lastBulkStates)
            {
                state.Key.SetReviewState(state.Value);
            }

            _lastBulkStates = null;
            PersistState();
            RefreshFilters();
            RaiseCommandAvailability();
        }

        private bool CanAcknowledgeFinding(object parameter)
        {
            return SelectedFinding != null && SelectedFinding.Severity != PreviewFindingSeverity.Error &&
                SelectedFinding.Resolution == PreviewFindingResolution.Open;
        }

        private void AcknowledgeFinding()
        {
            if (!CanAcknowledgeFinding(null))
            {
                return;
            }

            _acknowledgedFindingIds.Add(SelectedFinding.StableId);
            SelectedFinding.Acknowledge();
            PersistState();
            RefreshFilters();
            OnPropertyChanged(nameof(ExportReadinessText));
        }

        private void GoToEntry()
        {
            if (SelectedFinding != null)
            {
                _translationWorkspace.RevealEntry(SelectedFinding.Entry);
            }
        }

        private void PersistState()
        {
            if (!HasProject)
            {
                return;
            }

            try
            {
                _stateStore.Save(
                    _translationWorkspace.ProjectPath,
                    _translationWorkspace.ProjectEntries,
                    _acknowledgedFindingIds);
            }
            catch (IOException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "Review_State_SaveFailed");
            }
            catch (UnauthorizedAccessException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "Review_State_SaveFailed");
            }
        }

        private void RaiseCommandAvailability()
        {
            RaiseCanExecuteChanged(MarkReviewedCommand);
            RaiseCanExecuteChanged(ApproveCommand);
            RaiseCanExecuteChanged(RejectCommand);
            RaiseCanExecuteChanged(ApproveScopeCommand);
            RaiseCanExecuteChanged(UndoBulkCommand);
            RaiseCanExecuteChanged(AcknowledgeFindingCommand);
            RaiseCanExecuteChanged(GoToEntryCommand);
            RaiseCanExecuteChanged(RevalidateCommand);
            RaiseCanExecuteChanged(CancelValidationCommand);
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
