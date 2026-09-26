using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies a primary destination hosted by the preview shell.
    /// </summary>
    internal enum PreviewShellDestination
    {
        Projects,
        Translate,
        Review,
        Quality,
        History,
        ProjectUpdate,
        Settings,
        AdvancedTools
    }

    /// <summary>
    /// Identifies the visual and semantic severity of a shell notification.
    /// </summary>
    internal enum PreviewShellNotificationSeverity
    {
        Information,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Describes one keyboard-addressable preview navigation destination.
    /// </summary>
    internal sealed class PreviewShellNavigationItem
    {
        /// <summary>
        /// Creates a navigation item.
        /// </summary>
        /// <param name="destination">The destination selected by the item.</param>
        /// <param name="label">The localized visible label.</param>
        internal PreviewShellNavigationItem(PreviewShellDestination destination, string label)
        {
            Destination = destination;
            Label = label;
        }

        /// <summary>
        /// Gets the destination selected by this item.
        /// </summary>
        public PreviewShellDestination Destination { get; private set; }

        /// <summary>
        /// Gets the localized navigation label.
        /// </summary>
        public string Label { get; private set; }
    }

    /// <summary>
    /// Owns preview-shell navigation and user-visible project and operation status.
    /// </summary>
    internal sealed class PreviewShellViewModel : INotifyPropertyChanged
    {
        private readonly Action _openLegacyWorkspace;
        private readonly PreviewWorkflowRolloutViewModel _rollout;
        private readonly PreviewDiagnosticService _diagnostics;
        private PreviewShellDestination _currentDestination;
        private string _projectName;
        private bool _isModified;
        private int _warningCount;
        private bool _isOperationRunning;
        private string _operationText;
        private double _operationProgress;
        private string _notificationMessage;
        private PreviewShellNotificationSeverity _notificationSeverity;
        private bool _isShellServicesVisible;

        /// <summary>
        /// Creates the initial no-project shell state.
        /// </summary>
        /// <param name="openLegacyWorkspace">The integration action that opens the legacy workspace.</param>
        /// <exception cref="ArgumentNullException"><paramref name="openLegacyWorkspace"/> is <see langword="null"/>.</exception>
        internal PreviewShellViewModel(Action openLegacyWorkspace)
            : this(openLegacyWorkspace, null, null)
        {
        }

        /// <summary>Creates shell routing with independently applied workflow rollout state.</summary>
        /// <param name="openLegacyWorkspace">Opens the recoverable legacy workspace.</param>
        /// <param name="rollout">The applied per-workflow routing state.</param>
        /// <param name="diagnostics">Records privacy-safe fallback events.</param>
        internal PreviewShellViewModel(
            Action openLegacyWorkspace,
            PreviewWorkflowRolloutViewModel rollout,
            PreviewDiagnosticService diagnostics)
        {
            _openLegacyWorkspace = openLegacyWorkspace ?? throw new ArgumentNullException(nameof(openLegacyWorkspace));
            _rollout = rollout;
            _diagnostics = diagnostics;
            _currentDestination = PreviewShellDestination.Projects;
            _operationText = string.Empty;

            NavigationItems = new[]
            {
                CreateNavigationItem(PreviewShellDestination.Projects, "Shell_Navigation_Projects"),
                CreateNavigationItem(PreviewShellDestination.Translate, "Shell_Navigation_Translate"),
                CreateNavigationItem(PreviewShellDestination.Review, "Shell_Navigation_Review"),
                CreateNavigationItem(PreviewShellDestination.Quality, "Shell_Navigation_Quality"),
                CreateNavigationItem(PreviewShellDestination.History, "Shell_Navigation_History"),
                CreateNavigationItem(PreviewShellDestination.ProjectUpdate, "Shell_Navigation_ProjectUpdate"),
                CreateNavigationItem(PreviewShellDestination.Settings, "Shell_Navigation_Settings"),
                CreateNavigationItem(PreviewShellDestination.AdvancedTools, "Shell_Navigation_AdvancedTools")
            };

            NavigateCommand = new PreviewShellCommand(NavigateFromParameter);
            OpenLegacyWorkspaceCommand = new PreviewShellCommand(parameter => _openLegacyWorkspace());
            DismissNotificationCommand = new PreviewShellCommand(parameter => ClearNotification());
            OpenShellServicesCommand = new PreviewShellCommand(parameter => IsShellServicesVisible = true);
            CloseShellServicesCommand = new PreviewShellCommand(parameter => IsShellServicesVisible = false);
            if (_rollout != null)
            {
                _rollout.Applied += RolloutApplied;
            }
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the stable ordered primary navigation items.
        /// </summary>
        public IReadOnlyList<PreviewShellNavigationItem> NavigationItems { get; private set; }

        /// <summary>
        /// Gets or sets the selected preview destination.
        /// </summary>
        public PreviewShellDestination CurrentDestination
        {
            get => _currentDestination;
            set
            {
                if (!IsWorkflowEnabled(value))
                {
                    _diagnostics?.Record(
                        PreviewDiagnosticSeverity.Information,
                        "rollout.legacy-fallback.opened",
                        value.ToString());
                    _openLegacyWorkspace();
                    return;
                }

                if (_currentDestination == value)
                {
                    return;
                }

                _currentDestination = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentDescription));
                OnPropertyChanged(nameof(IsProjectHubVisible));
                OnPropertyChanged(nameof(IsTranslationWorkspaceVisible));
                OnPropertyChanged(nameof(IsReviewQualityWorkspaceVisible));
                OnPropertyChanged(nameof(IsHistoryUpdateWorkspaceVisible));
                OnPropertyChanged(nameof(IsSettingsWorkspaceVisible));
                OnPropertyChanged(nameof(IsAdvancedToolsWorkspaceVisible));
                OnPropertyChanged(nameof(IsPreviewFallbackVisible));
            }
        }

        /// <summary>
        /// Gets the localized heading for the selected destination.
        /// </summary>
        public string CurrentTitle => PreviewMessageCatalog.Get(GetDestinationMessageId("Title"));

        /// <summary>
        /// Gets the localized rollout description for the selected destination.
        /// </summary>
        public string CurrentDescription => PreviewMessageCatalog.Get(GetDestinationMessageId("Description"));

        /// <summary>
        /// Gets whether the preview Project Hub owns the current page.
        /// </summary>
        public bool IsProjectHubVisible => _currentDestination == PreviewShellDestination.Projects &&
            IsWorkflowEnabled(_currentDestination);

        /// <summary>
        /// Gets whether the preview translation workspace owns the current page.
        /// </summary>
        public bool IsTranslationWorkspaceVisible => _currentDestination == PreviewShellDestination.Translate &&
            IsWorkflowEnabled(_currentDestination);

        /// <summary>
        /// Gets whether the combined review and quality workspace owns the current page.
        /// </summary>
        public bool IsReviewQualityWorkspaceVisible =>
            (_currentDestination == PreviewShellDestination.Review ||
            _currentDestination == PreviewShellDestination.Quality) && IsWorkflowEnabled(_currentDestination);

        /// <summary>
        /// Gets whether the history and project-update workspace owns the current page.
        /// </summary>
        public bool IsHistoryUpdateWorkspaceVisible =>
            (_currentDestination == PreviewShellDestination.History ||
            _currentDestination == PreviewShellDestination.ProjectUpdate) && IsWorkflowEnabled(_currentDestination);

        /// <summary>
        /// Gets whether the unified Settings Center owns the current page.
        /// </summary>
        public bool IsSettingsWorkspaceVisible => _currentDestination == PreviewShellDestination.Settings &&
            IsWorkflowEnabled(_currentDestination);

        /// <summary>Gets whether the categorized Advanced Tools workspace owns the current page.</summary>
        public bool IsAdvancedToolsWorkspaceVisible => _currentDestination == PreviewShellDestination.AdvancedTools &&
            IsWorkflowEnabled(_currentDestination);

        /// <summary>
        /// Gets whether the selected destination still uses the shared preview fallback page.
        /// </summary>
        public bool IsPreviewFallbackVisible =>
            !IsProjectHubVisible &&
            !IsTranslationWorkspaceVisible &&
            !IsReviewQualityWorkspaceVisible &&
            !IsHistoryUpdateWorkspaceVisible &&
            !IsSettingsWorkspaceVisible &&
            !IsAdvancedToolsWorkspaceVisible;

        /// <summary>
        /// Gets the product version shown independently from dependency versions.
        /// </summary>
        public string ProductVersion => typeof(PreviewShellViewModel).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Gets the current project identity or the localized no-project label.
        /// </summary>
        public string ProjectIdentity => string.IsNullOrWhiteSpace(_projectName)
            ? PreviewMessageCatalog.Get("Shell_Project_None")
            : _projectName;

        /// <summary>
        /// Gets whether a project identity is available.
        /// </summary>
        public bool HasProject => !string.IsNullOrWhiteSpace(_projectName);

        /// <summary>
        /// Gets whether the current project contains unsaved changes.
        /// </summary>
        public bool IsModified => _isModified;

        /// <summary>
        /// Gets the localized modified-state label.
        /// </summary>
        public string ModifiedText => PreviewMessageCatalog.Get("Common_State_Modified");

        /// <summary>
        /// Gets whether the shell has one or more project warnings.
        /// </summary>
        public bool HasWarnings => _warningCount > 0;

        /// <summary>
        /// Gets the localized warning-count summary.
        /// </summary>
        public string WarningText => PreviewMessageCatalog.Format("Shell_Status_WarningCount", _warningCount);

        /// <summary>
        /// Gets whether a long-running shell operation is active.
        /// </summary>
        public bool IsOperationRunning => _isOperationRunning;

        /// <summary>
        /// Gets the localized active-operation description.
        /// </summary>
        public string OperationText => _operationText;

        /// <summary>
        /// Gets the current operation progress from zero through one hundred.
        /// </summary>
        public double OperationProgress => _operationProgress;

        /// <summary>
        /// Gets the localized idle or active shell status.
        /// </summary>
        public string StatusText => _isOperationRunning
            ? _operationText
            : PreviewMessageCatalog.Get("Common_State_Ready");

        /// <summary>
        /// Gets the command that selects a preview destination by enum or enum name.
        /// </summary>
        public ICommand NavigateCommand { get; private set; }

        /// <summary>
        /// Gets the command that opens the complete legacy workspace.
        /// </summary>
        public ICommand OpenLegacyWorkspaceCommand { get; private set; }

        /// <summary>
        /// Gets the command that dismisses the current non-blocking notification.
        /// </summary>
        public ICommand DismissNotificationCommand { get; private set; }

        /// <summary>Gets the command that opens About, diagnostics, credits, and licenses.</summary>
        public ICommand OpenShellServicesCommand { get; private set; }

        /// <summary>Gets the command that returns from shell services to the active workflow.</summary>
        public ICommand CloseShellServicesCommand { get; private set; }

        /// <summary>Gets or sets whether shell services cover the active workflow.</summary>
        public bool IsShellServicesVisible
        {
            get => _isShellServicesVisible;
            set
            {
                if (_isShellServicesVisible == value)
                {
                    return;
                }

                _isShellServicesVisible = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets whether a non-blocking shell notification is visible.
        /// </summary>
        public bool HasNotification => !string.IsNullOrWhiteSpace(_notificationMessage);

        /// <summary>
        /// Gets the localized and user-safe notification message.
        /// </summary>
        public string NotificationMessage => _notificationMessage;

        /// <summary>
        /// Gets the semantic severity of the current notification.
        /// </summary>
        public PreviewShellNotificationSeverity NotificationSeverity => _notificationSeverity;

        /// <summary>
        /// Gets the localized semantic label for the current notification severity.
        /// </summary>
        public string NotificationSeverityText
        {
            get
            {
                switch (_notificationSeverity)
                {
                    case PreviewShellNotificationSeverity.Success:
                        return PreviewMessageCatalog.Get("Common_Severity_Success");
                    case PreviewShellNotificationSeverity.Warning:
                        return PreviewMessageCatalog.Get("Common_Severity_Warning");
                    case PreviewShellNotificationSeverity.Error:
                        return PreviewMessageCatalog.Get("Common_Severity_Error");
                    default:
                        return PreviewMessageCatalog.Get("Common_Severity_Information");
                }
            }
        }

        /// <summary>
        /// Updates the project identity and unsaved state visible throughout the shell.
        /// </summary>
        /// <param name="projectName">The safe display name, or <see langword="null"/> to clear the project.</param>
        /// <param name="isModified">Whether the project contains unsaved changes.</param>
        internal void SetProject(string projectName, bool isModified)
        {
            _projectName = projectName;
            _isModified = !string.IsNullOrWhiteSpace(projectName) && isModified;
            OnPropertyChanged(nameof(ProjectIdentity));
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(IsModified));
        }

        /// <summary>
        /// Updates the warning count visible throughout the shell.
        /// </summary>
        /// <param name="warningCount">The non-negative project warning count.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="warningCount"/> is negative.</exception>
        internal void SetWarningCount(int warningCount)
        {
            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningCount));
            }

            _warningCount = warningCount;
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(WarningText));
        }

        /// <summary>
        /// Starts or updates a localized long-running operation.
        /// </summary>
        /// <param name="messageId">The registered operation message identifier.</param>
        /// <param name="progress">Progress from zero through one hundred.</param>
        /// <param name="arguments">Values for the message's documented placeholders.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="progress"/> is outside zero through one hundred.</exception>
        internal void SetOperation(string messageId, double progress, params object[] arguments)
        {
            if (progress < 0 || progress > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            _operationText = arguments == null || arguments.Length == 0
                ? PreviewMessageCatalog.Get(messageId)
                : PreviewMessageCatalog.Format(messageId, arguments);
            _operationProgress = progress;
            _isOperationRunning = true;
            OnPropertyChanged(nameof(IsOperationRunning));
            OnPropertyChanged(nameof(OperationText));
            OnPropertyChanged(nameof(OperationProgress));
            OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>
        /// Starts or updates a long-running operation with already localized user-safe text.
        /// </summary>
        /// <param name="message">The localized user-safe operation message.</param>
        /// <param name="progress">Progress from zero through one hundred.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="progress"/> is outside zero through one hundred.</exception>
        internal void SetOperationText(string message, double progress)
        {
            if (progress < 0 || progress > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            _operationText = message ?? string.Empty;
            _operationProgress = progress;
            _isOperationRunning = true;
            OnPropertyChanged(nameof(IsOperationRunning));
            OnPropertyChanged(nameof(OperationText));
            OnPropertyChanged(nameof(OperationProgress));
            OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>
        /// Returns the shell to its idle operation state.
        /// </summary>
        internal void CompleteOperation()
        {
            _isOperationRunning = false;
            _operationText = string.Empty;
            _operationProgress = 0;
            OnPropertyChanged(nameof(IsOperationRunning));
            OnPropertyChanged(nameof(OperationText));
            OnPropertyChanged(nameof(OperationProgress));
            OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>
        /// Shows a localized non-blocking notification in the persistent shell boundary.
        /// </summary>
        /// <param name="severity">The semantic notification severity.</param>
        /// <param name="messageId">The registered user-safe message identifier.</param>
        /// <param name="arguments">Values for the message's documented placeholders.</param>
        internal void ShowNotification(
            PreviewShellNotificationSeverity severity,
            string messageId,
            params object[] arguments)
        {
            _notificationSeverity = severity;
            _notificationMessage = arguments == null || arguments.Length == 0
                ? PreviewMessageCatalog.Get(messageId)
                : PreviewMessageCatalog.Format(messageId, arguments);
            OnPropertyChanged(nameof(HasNotification));
            OnPropertyChanged(nameof(NotificationMessage));
            OnPropertyChanged(nameof(NotificationSeverityText));
            OnPropertyChanged(nameof(NotificationSeverity));
        }

        /// <summary>
        /// Clears the current non-blocking shell notification.
        /// </summary>
        internal void ClearNotification()
        {
            _notificationMessage = string.Empty;
            OnPropertyChanged(nameof(HasNotification));
            OnPropertyChanged(nameof(NotificationMessage));
        }

        private static PreviewShellNavigationItem CreateNavigationItem(
            PreviewShellDestination destination,
            string messageId)
        {
            return new PreviewShellNavigationItem(destination, PreviewMessageCatalog.Get(messageId));
        }

        private void NavigateFromParameter(object parameter)
        {
            if (parameter is PreviewShellDestination)
            {
                CurrentDestination = (PreviewShellDestination)parameter;
                return;
            }

            PreviewShellDestination destination;
            if (parameter != null && Enum.TryParse(parameter.ToString(), true, out destination))
            {
                CurrentDestination = destination;
            }
        }

        private string GetDestinationMessageId(string suffix)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Shell_Page_{0}_{1}",
                _currentDestination,
                suffix);
        }

        private bool IsWorkflowEnabled(PreviewShellDestination destination)
        {
            return _rollout == null || _rollout.IsEnabled(ToWorkflow(destination));
        }

        private static PreviewWorkflow ToWorkflow(PreviewShellDestination destination)
        {
            switch (destination)
            {
                case PreviewShellDestination.Projects: return PreviewWorkflow.ProjectHub;
                case PreviewShellDestination.Translate: return PreviewWorkflow.Translation;
                case PreviewShellDestination.Review: return PreviewWorkflow.Review;
                case PreviewShellDestination.Quality: return PreviewWorkflow.Quality;
                case PreviewShellDestination.History: return PreviewWorkflow.History;
                case PreviewShellDestination.ProjectUpdate: return PreviewWorkflow.ProjectUpdate;
                case PreviewShellDestination.Settings: return PreviewWorkflow.Settings;
                default: return PreviewWorkflow.AdvancedTools;
            }
        }

        private void RolloutApplied(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsProjectHubVisible));
            OnPropertyChanged(nameof(IsTranslationWorkspaceVisible));
            OnPropertyChanged(nameof(IsReviewQualityWorkspaceVisible));
            OnPropertyChanged(nameof(IsHistoryUpdateWorkspaceVisible));
            OnPropertyChanged(nameof(IsSettingsWorkspaceVisible));
            OnPropertyChanged(nameof(IsAdvancedToolsWorkspaceVisible));
            OnPropertyChanged(nameof(IsPreviewFallbackVisible));
            if (!IsWorkflowEnabled(_currentDestination))
            {
                _diagnostics?.Record(
                    PreviewDiagnosticSeverity.Information,
                    "rollout.workflow.reverted",
                    _currentDestination.ToString());
                _openLegacyWorkspace();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
