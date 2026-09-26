using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Loads and presents bounded technical context beside the selected workspace entry.
    /// </summary>
    internal sealed class PreviewContextInspectorViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int MaximumSearchResults = 200;
        private readonly Func<PreviewTranslationEntry, CancellationToken, PreviewEntryContext> _loadContext;
        private readonly Func<string, PreviewTranslationEntry> _findEntry;
        private readonly Action<PreviewTranslationEntry> _navigateToEntry;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _searchCancellation;
        private PreviewTranslationEntry _selectedEntry;
        private PreviewEntryContext _context;
        private PreviewContextState _state;
        private string _stateText;
        private string _codeSearchText;
        private IReadOnlyList<PreviewCodeSearchResult> _codeResults;
        private PreviewCodeSearchResult _selectedCodeResult;
        private PreviewContextRelation _selectedRelation;
        private PreviewNpcContext _selectedNpc;

        /// <summary>
        /// Creates an inspector over the project context and stable workspace navigation boundaries.
        /// </summary>
        /// <param name="loadContext">Loads context from the active parser project.</param>
        /// <param name="findEntry">Finds a normalized entry by stable project-local key.</param>
        /// <param name="navigateToEntry">Reveals an exact normalized entry in the workspace.</param>
        internal PreviewContextInspectorViewModel(
            Func<PreviewTranslationEntry, CancellationToken, PreviewEntryContext> loadContext,
            Func<string, PreviewTranslationEntry> findEntry,
            Action<PreviewTranslationEntry> navigateToEntry)
        {
            _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
            _findEntry = findEntry ?? throw new ArgumentNullException(nameof(findEntry));
            _navigateToEntry = navigateToEntry ?? throw new ArgumentNullException(nameof(navigateToEntry));
            _state = PreviewContextState.Empty;
            _stateText = PreviewMessageCatalog.Get("Workspace_Inspector_Empty");
            _codeSearchText = string.Empty;
            _codeResults = new PreviewCodeSearchResult[0];

            RetryCommand = new PreviewShellCommand(parameter => LoadSelectedEntry(), parameter => _selectedEntry != null);
            CancelCommand = new PreviewShellCommand(parameter => Cancel(), parameter => IsLoading);
            SearchCodeCommand = new PreviewShellCommand(
                parameter => SearchCode(),
                parameter => HasCode && !string.IsNullOrWhiteSpace(CodeSearchText));
            NavigateRelationCommand = new PreviewShellCommand(
                parameter => Navigate(SelectedRelation?.EntryKey),
                parameter => SelectedRelation != null);
            NavigateNpcCommand = new PreviewShellCommand(
                parameter => Navigate(SelectedNpc?.EntryKey),
                parameter => SelectedNpc != null);
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the command that retries loading the selected entry context.</summary>
        public ICommand RetryCommand { get; private set; }

        /// <summary>Gets the command that cancels active context work.</summary>
        public ICommand CancelCommand { get; private set; }

        /// <summary>Gets the command that performs a bounded exact code search.</summary>
        public ICommand SearchCodeCommand { get; private set; }

        /// <summary>Gets the command that navigates to the selected stable relationship.</summary>
        public ICommand NavigateRelationCommand { get; private set; }

        /// <summary>Gets the command that navigates to the selected NPC-owned entry.</summary>
        public ICommand NavigateNpcCommand { get; private set; }

        /// <summary>Gets the current context loading state.</summary>
        public PreviewContextState State
        {
            get => _state;
            private set
            {
                if (SetField(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(IsReady));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsFailed));
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets the localized loading, empty, ready, or failure message.</summary>
        public string StateText
        {
            get => _stateText;
            private set => SetField(ref _stateText, value ?? string.Empty);
        }

        /// <summary>Gets the current immutable context snapshot.</summary>
        public PreviewEntryContext Context
        {
            get => _context;
            private set
            {
                if (SetField(ref _context, value))
                {
                    OnPropertyChanged(nameof(HasCode));
                    OnPropertyChanged(nameof(IsCodeEmpty));
                    OnPropertyChanged(nameof(HasMetadata));
                    OnPropertyChanged(nameof(IsMetadataEmpty));
                    OnPropertyChanged(nameof(HasRelations));
                    OnPropertyChanged(nameof(IsRelationsEmpty));
                    OnPropertyChanged(nameof(HasNpcs));
                    OnPropertyChanged(nameof(IsNpcsEmpty));
                    OnPropertyChanged(nameof(HasAsset));
                    OnPropertyChanged(nameof(IsAssetEmpty));
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets or sets the exact case-insensitive code-search query.</summary>
        public string CodeSearchText
        {
            get => _codeSearchText;
            set
            {
                if (SetField(ref _codeSearchText, value ?? string.Empty))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets the bounded code-search results.</summary>
        public IReadOnlyList<PreviewCodeSearchResult> CodeResults
        {
            get => _codeResults;
            private set
            {
                if (SetField(ref _codeResults, value ?? new PreviewCodeSearchResult[0]))
                {
                    OnPropertyChanged(nameof(CodeResultText));
                }
            }
        }

        /// <summary>Gets the localized code-search result count.</summary>
        public string CodeResultText => PreviewMessageCatalog.Format(
            "Workspace_Inspector_Code_ResultCount",
            CodeResults.Count);

        /// <summary>Gets or sets the code result selected for exact line navigation.</summary>
        public PreviewCodeSearchResult SelectedCodeResult
        {
            get => _selectedCodeResult;
            set => SetField(ref _selectedCodeResult, value);
        }

        /// <summary>Gets or sets the related entry selected for workspace navigation.</summary>
        public PreviewContextRelation SelectedRelation
        {
            get => _selectedRelation;
            set
            {
                if (SetField(ref _selectedRelation, value))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets or sets the NPC owner selected for workspace navigation.</summary>
        public PreviewNpcContext SelectedNpc
        {
            get => _selectedNpc;
            set
            {
                if (SetField(ref _selectedNpc, value))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets whether context work is active.</summary>
        public bool IsLoading => State == PreviewContextState.Loading;

        /// <summary>Gets whether supported context loaded successfully.</summary>
        public bool IsReady => State == PreviewContextState.Ready;

        /// <summary>Gets whether no supported context exists for the selection.</summary>
        public bool IsEmpty => State == PreviewContextState.Empty;

        /// <summary>Gets whether parser or asset context loading failed.</summary>
        public bool IsFailed => State == PreviewContextState.Failed;

        /// <summary>Gets whether decompiled PEX code is available.</summary>
        public bool HasCode => !string.IsNullOrWhiteSpace(Context?.Code);

        /// <summary>Gets whether the selected entry has no decompiled code.</summary>
        public bool IsCodeEmpty => !HasCode;

        /// <summary>Gets whether parser-owned record metadata is available.</summary>
        public bool HasMetadata => Context?.Metadata.Count > 0;

        /// <summary>Gets whether the selected entry has no parser-owned record metadata.</summary>
        public bool IsMetadataEmpty => !HasMetadata;

        /// <summary>Gets whether stable related entries are available.</summary>
        public bool HasRelations => Context?.Relations.Count > 0;

        /// <summary>Gets whether the selected entry has no stable relationships.</summary>
        public bool IsRelationsEmpty => !HasRelations;

        /// <summary>Gets whether NPC ownership context is available.</summary>
        public bool HasNpcs => Context?.Npcs.Count > 0;

        /// <summary>Gets whether the selected entry has no NPC ownership context.</summary>
        public bool IsNpcsEmpty => !HasNpcs;

        /// <summary>Gets whether a validated bounded visual asset is available.</summary>
        public bool HasAsset => Context?.Asset != null;

        /// <summary>Gets whether the selected entry has no validated visual asset.</summary>
        public bool IsAssetEmpty => !HasAsset;

        /// <summary>
        /// Updates the selected stable entry and asynchronously loads its bounded context.
        /// </summary>
        /// <param name="entry">The newly selected normalized entry, or <c>null</c>.</param>
        internal void SelectEntry(PreviewTranslationEntry entry)
        {
            _selectedEntry = entry;
            LoadSelectedEntry();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
        }

        private async void LoadSelectedEntry()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _searchCancellation?.Cancel();
            CodeResults = new PreviewCodeSearchResult[0];
            SelectedCodeResult = null;
            SelectedRelation = null;
            SelectedNpc = null;
            Context = null;

            PreviewTranslationEntry entry = _selectedEntry;
            if (entry == null)
            {
                State = PreviewContextState.Empty;
                StateText = PreviewMessageCatalog.Get("Workspace_Inspector_Empty");
                return;
            }

            var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;
            State = PreviewContextState.Loading;
            StateText = PreviewMessageCatalog.Get("Workspace_Inspector_Loading");
            try
            {
                PreviewEntryContext context = await Task.Run(
                    () => _loadContext(entry, cancellation.Token),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(entry, _selectedEntry))
                {
                    return;
                }

                Context = context;
                State = context != null && context.HasAnyContext
                    ? PreviewContextState.Ready
                    : PreviewContextState.Empty;
                StateText = State == PreviewContextState.Ready
                    ? PreviewMessageCatalog.Get("Workspace_Inspector_Ready")
                    : PreviewMessageCatalog.Get("Workspace_Inspector_NoContext");
                if (HasCode)
                {
                    CodeSearchText = entry.SourceText;
                    SearchCode();
                }
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(entry, _selectedEntry))
                {
                    State = PreviewContextState.Empty;
                    StateText = PreviewMessageCatalog.Get("Workspace_Inspector_Cancelled");
                }
            }
            catch (Exception)
            {
                if (ReferenceEquals(entry, _selectedEntry))
                {
                    State = PreviewContextState.Failed;
                    StateText = PreviewMessageCatalog.Get("Workspace_Inspector_Failed");
                }
            }
            finally
            {
                if (ReferenceEquals(_loadCancellation, cancellation))
                {
                    _loadCancellation = null;
                }

                cancellation.Dispose();
                RaiseCommands();
            }
        }

        private async void SearchCode()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            string code = Context?.Code ?? string.Empty;
            string query = CodeSearchText;
            try
            {
                IReadOnlyList<PreviewCodeSearchResult> results = await Task.Run(
                    () => FindCodeResults(code, query, cancellation.Token),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                CodeResults = results;
                SelectedCodeResult = results.FirstOrDefault();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_searchCancellation, cancellation))
                {
                    _searchCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private static IReadOnlyList<PreviewCodeSearchResult> FindCodeResults(
            string code,
            string query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(query))
            {
                return new PreviewCodeSearchResult[0];
            }

            var results = new List<PreviewCodeSearchResult>();
            string[] lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int index = 0; index < lines.Length && results.Count < MaximumSearchResults; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int match = lines[index].IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
                if (match >= 0)
                {
                    results.Add(new PreviewCodeSearchResult(index + 1, lines[index].Trim()));
                }
            }

            return results;
        }

        private void Navigate(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return;
            }

            PreviewTranslationEntry entry = _findEntry(entryKey);
            if (entry != null)
            {
                _navigateToEntry(entry);
            }
        }

        private void Cancel()
        {
            _loadCancellation?.Cancel();
            _searchCancellation?.Cancel();
        }

        private void RaiseCommands()
        {
            foreach (ICommand command in new[]
            {
                RetryCommand,
                CancelCommand,
                SearchCodeCommand,
                NavigateRelationCommand,
                NavigateNpcCommand
            })
            {
                var previewCommand = command as PreviewShellCommand;
                previewCommand?.RaiseCanExecuteChanged();
            }
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

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
