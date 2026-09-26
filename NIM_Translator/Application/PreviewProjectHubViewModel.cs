using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Coordinates project opening, safe recent-project metadata, and navigation into project workflows.
    /// </summary>
    internal sealed class PreviewProjectHubViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PreviewTranslationWorkspaceViewModel _workspace;
        private readonly PreviewShellViewModel _shell;
        private readonly Func<string> _chooseProjectPath;
        private readonly Func<bool> _confirmProjectReplacement;
        private readonly IPreviewRecentProjectStore _recentStore;
        private readonly Action _openLegacyWorkspace;
        private PreviewRecentProject _selectedRecentProject;

        /// <summary>
        /// Creates a Project Hub over the shared normalized translation workspace.
        /// </summary>
        /// <param name="workspace">The shared project and translation state owner.</param>
        /// <param name="shell">The persistent shell state owner.</param>
        /// <param name="chooseProjectPath">Selects a supported project path.</param>
        /// <param name="confirmProjectReplacement">Confirms replacement of unsaved work.</param>
        /// <param name="recentStore">Persists bounded private recent-project references.</param>
        /// <param name="openLegacyWorkspace">Opens the workflow-specific legacy fallback.</param>
        internal PreviewProjectHubViewModel(
            PreviewTranslationWorkspaceViewModel workspace,
            PreviewShellViewModel shell,
            Func<string> chooseProjectPath,
            Func<bool> confirmProjectReplacement,
            IPreviewRecentProjectStore recentStore,
            Action openLegacyWorkspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _chooseProjectPath = chooseProjectPath ?? throw new ArgumentNullException(nameof(chooseProjectPath));
            _confirmProjectReplacement = confirmProjectReplacement ??
                throw new ArgumentNullException(nameof(confirmProjectReplacement));
            _recentStore = recentStore ?? throw new ArgumentNullException(nameof(recentStore));
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));

            RecentProjects = new ObservableCollection<PreviewRecentProject>(_recentStore.Load());
            OpenProjectCommand = new PreviewShellCommand(parameter => OpenSelectedProject());
            OpenRecentProjectCommand = new PreviewShellCommand(
                parameter => OpenRecentProject(parameter as PreviewRecentProject),
                parameter => parameter is PreviewRecentProject &&
                    ((PreviewRecentProject)parameter).IsAvailable && !IsBusy);
            RemoveRecentProjectCommand = new PreviewShellCommand(
                parameter => RemoveRecentProject(parameter as PreviewRecentProject),
                parameter => parameter is PreviewRecentProject && !IsBusy);
            ContinueCommand = new PreviewShellCommand(
                parameter => _shell.CurrentDestination = PreviewShellDestination.Translate,
                parameter => HasProject && !IsBusy);
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());

            _workspace.PropertyChanged += WorkspacePropertyChanged;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bounded newest-first recent-project list.
        /// </summary>
        public ObservableCollection<PreviewRecentProject> RecentProjects { get; private set; }

        /// <summary>
        /// Gets or sets the recent project selected for keyboard operation.
        /// </summary>
        public PreviewRecentProject SelectedRecentProject
        {
            get => _selectedRecentProject;
            set
            {
                if (ReferenceEquals(_selectedRecentProject, value))
                {
                    return;
                }

                _selectedRecentProject = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets whether at least one recent project is available for display.
        /// </summary>
        public bool HasRecentProjects => RecentProjects.Count > 0;

        /// <summary>
        /// Gets whether one normalized project is active.
        /// </summary>
        public bool HasProject => _workspace.HasProject;

        /// <summary>
        /// Gets whether project loading or persistence work is active.
        /// </summary>
        public bool IsBusy => _workspace.IsBusy;

        /// <summary>
        /// Gets the safe active project file name.
        /// </summary>
        public string ActiveProjectName => _shell.ProjectIdentity;

        /// <summary>
        /// Gets the safe active project format.
        /// </summary>
        public string ActiveProjectFormat => System.IO.Path.GetExtension(_workspace.ProjectPath ?? string.Empty)
            .TrimStart('.')
            .ToUpperInvariant();

        /// <summary>
        /// Gets the localized active project format summary.
        /// </summary>
        public string ActiveProjectFormatText => PreviewMessageCatalog.Format(
            "Projects_Active_Format",
            ActiveProjectFormat);

        /// <summary>
        /// Gets the command that opens a file picker for a project.
        /// </summary>
        public ICommand OpenProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command that reopens an available recent project.
        /// </summary>
        public ICommand OpenRecentProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command that removes a recent reference without deleting the project file.
        /// </summary>
        public ICommand RemoveRecentProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command that enters Translate with the active project.
        /// </summary>
        public ICommand ContinueCommand { get; private set; }

        /// <summary>
        /// Gets the command that opens the legacy project workspace.
        /// </summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>
        /// Opens one private project path after protecting unsaved work.
        /// </summary>
        /// <param name="path">The selected or dropped private path.</param>
        /// <returns><see langword="true"/> when the project was opened successfully.</returns>
        internal async Task<bool> OpenProjectAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || IsBusy)
            {
                return false;
            }

            if (_workspace.IsModified && !_confirmProjectReplacement())
            {
                return false;
            }

            bool opened = await _workspace.OpenProjectAsync(path);
            if (!opened)
            {
                return false;
            }

            RememberProject(path);
            _shell.CurrentDestination = PreviewShellDestination.Translate;
            return true;
        }

        /// <summary>
        /// Opens exactly one dropped project and rejects ambiguous multi-file input.
        /// </summary>
        /// <param name="paths">The dropped private paths.</param>
        /// <returns><see langword="true"/> when one project was opened successfully.</returns>
        internal async Task<bool> OpenDroppedProjectsAsync(IEnumerable<string> paths)
        {
            string[] candidates = (paths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(2)
                .ToArray();
            if (candidates.Length != 1)
            {
                _shell.ShowNotification(
                    PreviewShellNotificationSeverity.Warning,
                    "Projects_Drop_OneFileRequired");
                return false;
            }

            return await OpenProjectAsync(candidates[0]);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _workspace.PropertyChanged -= WorkspacePropertyChanged;
        }

        private async void OpenSelectedProject()
        {
            await OpenProjectAsync(_chooseProjectPath());
        }

        private async void OpenRecentProject(PreviewRecentProject project)
        {
            if (project != null && project.IsAvailable)
            {
                await OpenProjectAsync(project.Path);
            }
        }

        private void RemoveRecentProject(PreviewRecentProject project)
        {
            if (project == null || !RecentProjects.Remove(project))
            {
                return;
            }

            if (ReferenceEquals(SelectedRecentProject, project))
            {
                SelectedRecentProject = RecentProjects.FirstOrDefault();
            }

            PersistRecentProjects();
            OnPropertyChanged(nameof(HasRecentProjects));
        }

        private void RememberProject(string path)
        {
            PreviewRecentProject existing = RecentProjects.FirstOrDefault(
                project => string.Equals(project.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RecentProjects.Remove(existing);
            }

            RecentProjects.Insert(0, new PreviewRecentProject(path, DateTime.UtcNow));
            while (RecentProjects.Count > 12)
            {
                RecentProjects.RemoveAt(RecentProjects.Count - 1);
            }

            SelectedRecentProject = RecentProjects[0];
            PersistRecentProjects();
            OnPropertyChanged(nameof(HasRecentProjects));
        }

        private void PersistRecentProjects()
        {
            try
            {
                _recentStore.Save(RecentProjects.ToArray());
            }
            catch (IOException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "Projects_Recent_SaveFailed");
            }
            catch (UnauthorizedAccessException)
            {
                _shell.ShowNotification(PreviewShellNotificationSeverity.Warning, "Projects_Recent_SaveFailed");
            }
        }

        private void WorkspacePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewTranslationWorkspaceViewModel.HasProject) ||
                e.PropertyName == nameof(PreviewTranslationWorkspaceViewModel.IsBusy) ||
                e.PropertyName == nameof(PreviewTranslationWorkspaceViewModel.IsModified))
            {
                OnPropertyChanged(nameof(HasProject));
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(ActiveProjectName));
                OnPropertyChanged(nameof(ActiveProjectFormat));
                OnPropertyChanged(nameof(ActiveProjectFormatText));
                RaiseCommandAvailability();
            }
        }

        private void RaiseCommandAvailability()
        {
            ((PreviewShellCommand)OpenRecentProjectCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)RemoveRecentProjectCommand).RaiseCanExecuteChanged();
            ((PreviewShellCommand)ContinueCommand).RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
