using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using NIMEngine.Language;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Hosts preview workflow navigation while retaining the complete legacy workspace as a fallback.
    /// </summary>
    public partial class PreviewShellWindow : Window
    {
        private NIMGui _legacyWorkspace;
        private readonly PreviewShellViewModel _shellViewModel;
        private readonly PreviewProjectHubViewModel _projectHubViewModel;
        private readonly PreviewTranslationWorkspaceViewModel _translationWorkspaceViewModel;
        private readonly PreviewReviewQualityViewModel _reviewQualityViewModel;
        private readonly PreviewHistoryUpdateViewModel _historyUpdateViewModel;
        private readonly PreviewSettingsViewModel _settingsViewModel;
        private readonly PreviewAdvancedToolsViewModel _advancedToolsViewModel;
        private readonly PreviewDiagnosticService _diagnostics;
        private readonly PreviewWorkflowRolloutViewModel _rolloutViewModel;
        private readonly IPreviewDialogService _dialogService;
        private readonly PreviewShellServicesViewModel _shellServicesViewModel;

        /// <summary>
        /// Creates the preview application shell in its no-project state.
        /// </summary>
        public PreviewShellWindow()
            : this(new PreviewDiagnosticService())
        {
        }

        /// <summary>
        /// Creates the preview application shell over an existing startup diagnostic lifetime.
        /// </summary>
        /// <param name="diagnostics">The bounded startup and shell diagnostic service.</param>
        internal PreviewShellWindow(PreviewDiagnosticService diagnostics)
        {
            InitializeComponent();
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _diagnostics.Record(PreviewDiagnosticSeverity.Information, "shell.opened");
            _dialogService = new WpfPreviewDialogService(() => this);
            _rolloutViewModel = new PreviewWorkflowRolloutViewModel(new PreviewWorkflowRolloutStore());
            _shellViewModel = new PreviewShellViewModel(OpenLegacyWorkspace, _rolloutViewModel, _diagnostics);
            _translationWorkspaceViewModel = new PreviewTranslationWorkspaceViewModel(
                SelectPreviewProject,
                OpenLegacyWorkspace,
                _shellViewModel,
                PreviewTranslationProject.Open,
                OpenProjectFromWorkflowAsync,
                SelectTranslationTableImport,
                SelectTranslationTableExport,
                SelectProjectExport,
                ConvertToTraditional,
                CopyWorkspaceText,
                SelectRamCacheImport,
                SelectRamCacheExport,
                ConfirmCacheClear);
            _projectHubViewModel = new PreviewProjectHubViewModel(
                _translationWorkspaceViewModel,
                _shellViewModel,
                SelectPreviewProject,
                ConfirmProjectReplacement,
                new PreviewRecentProjectStore(),
                OpenLegacyWorkspace);
            _shellServicesViewModel = new PreviewShellServicesViewModel(
                _diagnostics,
                SelectDiagnosticExport,
                _dialogService);
            _reviewQualityViewModel = new PreviewReviewQualityViewModel(
                _shellViewModel,
                _translationWorkspaceViewModel,
                new PreviewQualityAnalyzer(),
                new PreviewReviewStateStore(),
                ConfirmBulkApproval,
                OpenLegacyWorkspace);
            _historyUpdateViewModel = new PreviewHistoryUpdateViewModel(
                _shellViewModel,
                _translationWorkspaceViewModel,
                new PreviewProjectComparisonService(),
                new PreviewRevisionHistoryStore(),
                SelectPreviousRevision,
                PreviewTranslationProject.Open,
                ConfirmConflictReuse,
                ConfirmBulkReuse,
                OpenLegacyWorkspace,
                ConfirmHistoryDelete,
                ConfirmHistoryClear);
            _settingsViewModel = new PreviewSettingsViewModel(
                _shellViewModel,
                new LegacyPreviewSettingsStore(),
                ConfirmSettingsReset,
                ConfirmSettingsDiscard,
                OpenLegacyWorkspace,
                _rolloutViewModel);
            _advancedToolsViewModel = new PreviewAdvancedToolsViewModel(
                new LegacyPreviewAdvancedToolsStore(),
                _shellViewModel,
                ConfirmDatabaseMutation);
            DataContext = _shellViewModel;
            ProjectHub.DataContext = _projectHubViewModel;
            TranslationWorkspace.DataContext = _translationWorkspaceViewModel;
            ReviewQualityWorkspace.DataContext = _reviewQualityViewModel;
            HistoryUpdateWorkspace.DataContext = _historyUpdateViewModel;
            SettingsWorkspace.DataContext = _settingsViewModel;
            AdvancedToolsWorkspace.DataContext = _advancedToolsViewModel;
            ShellServicesWorkspace.DataContext = _shellServicesViewModel;
            ShellServicesWorkspace.CloseRequested += ShellServicesCloseRequested;
            _shellViewModel.PropertyChanged += ShellViewModelPropertyChanged;
        }

        private bool ConfirmBulkApproval(int entryCount)
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Format("Review_ApproveScope_Confirmation", entryCount),
                PreviewMessageCatalog.Get("Review_ApproveScope_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmProjectReplacement()
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Get("Projects_Replace_Confirmation"),
                PreviewMessageCatalog.Get("Projects_Replace_ConfirmationTitle"),
                PreviewDialogSeverity.Warning);
        }

        private System.Threading.Tasks.Task<bool> OpenProjectFromWorkflowAsync(string path)
        {
            return _projectHubViewModel.OpenProjectAsync(path);
        }

        private string SelectPreviewProject()
        {
            IInputElement previousFocus = Keyboard.FocusedElement;
            var dialog = new OpenFileDialog
            {
                Title = PreviewMessageCatalog.Get("Workspace_Open_Title"),
                Filter = PreviewMessageCatalog.Get("Workspace_Project_Filter"),
                CheckFileExists = true,
                Multiselect = false
            };
            bool? result = dialog.ShowDialog(this);
            RestoreFocus(previousFocus);
            return result == true ? dialog.FileName : string.Empty;
        }

        private string SelectPreviousRevision()
        {
            IInputElement previousFocus = Keyboard.FocusedElement;
            var dialog = new OpenFileDialog
            {
                Title = PreviewMessageCatalog.Get("Update_Compare_Title"),
                Filter = PreviewMessageCatalog.Get("Workspace_Project_Filter"),
                CheckFileExists = true,
                Multiselect = false
            };
            bool? result = dialog.ShowDialog(this);
            RestoreFocus(previousFocus);
            return result == true ? dialog.FileName : string.Empty;
        }

        private string SelectTranslationTableImport()
        {
            return ShowOpenDialog(
                PreviewMessageCatalog.Get("Workspace_Tools_ImportTable_Title"),
                PreviewMessageCatalog.Get("Workspace_Tools_Table_Filter"));
        }

        private string SelectTranslationTableExport()
        {
            return ShowSaveDialog(
                PreviewMessageCatalog.Get("Workspace_Tools_ExportTable_Title"),
                PreviewMessageCatalog.Get("Workspace_Tools_Table_Filter"),
                "translation-table.tsv");
        }

        private string SelectRamCacheImport()
        {
            return ShowOpenDialog(
                PreviewMessageCatalog.Get("Workspace_Tools_ImportRamCache_Title"),
                PreviewMessageCatalog.Get("Workspace_Tools_RamCache_Filter"));
        }

        private string SelectRamCacheExport()
        {
            return ShowSaveDialog(
                PreviewMessageCatalog.Get("Workspace_Tools_ExportRamCache_Title"),
                PreviewMessageCatalog.Get("Workspace_Tools_RamCache_Filter"),
                "translation-cache.json");
        }

        private string SelectProjectExport()
        {
            string sourcePath = _translationWorkspaceViewModel.ProjectPath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string extension = Path.GetExtension(sourcePath);
            string filter = string.Format("{0} project|*{0}", extension);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath) + "-export" + extension;
            return ShowSaveDialog(PreviewMessageCatalog.Get("Workspace_Tools_ExportProject_Title"), filter, fileName);
        }

        private string SelectDiagnosticExport()
        {
            return ShowSaveDialog(
                PreviewMessageCatalog.Get("ShellServices_Diagnostics_ExportTitle"),
                PreviewMessageCatalog.Get("ShellServices_Diagnostics_Filter"),
                "nim-diagnostics.txt");
        }

        private string ShowOpenDialog(string title, string filter)
        {
            IInputElement previousFocus = Keyboard.FocusedElement;
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false
            };
            bool? result = dialog.ShowDialog(this);
            RestoreFocus(previousFocus);
            return result == true ? dialog.FileName : string.Empty;
        }

        private string ShowSaveDialog(string title, string filter, string fileName)
        {
            IInputElement previousFocus = Keyboard.FocusedElement;
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = fileName,
                AddExtension = true,
                OverwritePrompt = true
            };
            bool? result = dialog.ShowDialog(this);
            RestoreFocus(previousFocus);
            return result == true ? dialog.FileName : string.Empty;
        }

        private static string ConvertToTraditional(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string converted = ChineseVariantMap.SimplifiedToTraditionalByReq(value ?? string.Empty);
            cancellationToken.ThrowIfCancellationRequested();
            return converted;
        }

        private static void CopyWorkspaceText(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Clipboard.SetText(value);
            }
        }

        private bool ConfirmConflictReuse(int entryCount)
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Format("Update_Conflict_Confirmation", entryCount),
                PreviewMessageCatalog.Get("Update_Conflict_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmBulkReuse(int entryCount)
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Format("Update_ReuseVisible_Confirmation", entryCount),
                PreviewMessageCatalog.Get("Update_ReuseVisible_ConfirmationTitle"),
                PreviewDialogSeverity.Information);
        }

        private bool ConfirmSettingsReset()
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Get("Settings_Reset_Confirmation"),
                PreviewMessageCatalog.Get("Settings_Reset_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmSettingsDiscard()
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Get("Settings_Discard_Confirmation"),
                PreviewMessageCatalog.Get("Settings_Discard_ConfirmationTitle"),
                PreviewDialogSeverity.Warning);
        }

        private bool ConfirmDatabaseMutation()
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Get("Advanced_Database_Mutation_Confirmation"),
                PreviewMessageCatalog.Get("Advanced_Database_Mutation_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmCacheClear(bool clearProviderCache, bool clearUserCache)
        {
            string scope = clearProviderCache && clearUserCache
                ? PreviewMessageCatalog.Get("Workspace_Tools_CacheScope_All")
                : clearProviderCache
                    ? PreviewMessageCatalog.Get("Workspace_Tools_CacheScope_Provider")
                    : PreviewMessageCatalog.Get("Workspace_Tools_CacheScope_User");
            return ShowConfirmation(
                PreviewMessageCatalog.Format("Workspace_Tools_CacheClear_Confirmation", scope),
                PreviewMessageCatalog.Get("Workspace_Tools_CacheClear_Title"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmHistoryDelete()
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Get("TranslationHistory_Delete_Confirmation"),
                PreviewMessageCatalog.Get("TranslationHistory_Delete_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private bool ConfirmHistoryClear(int count)
        {
            return ShowConfirmation(
                PreviewMessageCatalog.Format("TranslationHistory_Clear_Confirmation", count),
                PreviewMessageCatalog.Get("TranslationHistory_Clear_ConfirmationTitle"),
                PreviewDialogSeverity.Destructive);
        }

        private void OpenLegacyWorkspace()
        {
            if (_legacyWorkspace == null)
            {
                _legacyWorkspace = new NIMGui(_diagnostics);
                NIMApp.WorkWin = _legacyWorkspace;
                NIMApp.SelfSetting.Layout = NIMLayout.Classic;//Update the configuration file; the Classic layout will be selected on the next startup.
                NIMApp.SelfSetting.SaveConfig();
                _legacyWorkspace.Closed += LegacyWorkspaceClosed;
                _legacyWorkspace.Show();

                this.Hide();//I didn't see where Wuerfelhusten bound the implementation of the `Closed` event¡ªit likely calls `CloseAny`, causing the main program to exit completely. For now, I'm just using `Hide` as a quick fix; if you see this, perhaps you could help me handle it properly.
            }

            if (_legacyWorkspace.WindowState == WindowState.Minimized)
            {
                _legacyWorkspace.WindowState = WindowState.Normal;
            }

            _legacyWorkspace.Activate();
        }

        private void LegacyWorkspaceClosed(object sender, EventArgs e)
        {
            _legacyWorkspace.Closed -= LegacyWorkspaceClosed;
            _legacyWorkspace = null;
        }

        private bool ShowConfirmation(
            string message,
            string title,
            PreviewDialogSeverity severity)
        {
            return _dialogService.Show(new PreviewDialogRequest(title, message, severity, true));
        }

        private void RestoreFocus(IInputElement previousFocus)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => previousFocus?.Focus()));
        }

        private void ShellViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewShellViewModel.IsShellServicesVisible))
            {
                if (_shellViewModel.IsShellServicesVisible)
                {
                    _shellServicesViewModel.Refresh();
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusCurrentWorkflow));
                return;
            }

            if (e.PropertyName != nameof(PreviewShellViewModel.CurrentDestination))
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusCurrentWorkflow));
        }

        private void FocusCurrentWorkflow()
        {
            if (_shellViewModel.IsShellServicesVisible)
            {
                ShellServicesWorkspace.FocusInitialControl();
                return;
            }

            switch (_shellViewModel.CurrentDestination)
            {
                case PreviewShellDestination.Projects:
                    ProjectHub.FocusInitialControl();
                    break;
                case PreviewShellDestination.Translate:
                    TranslationWorkspace.FocusInitialControl();
                    break;
                case PreviewShellDestination.Review:
                case PreviewShellDestination.Quality:
                    ReviewQualityWorkspace.FocusInitialControl();
                    break;
                case PreviewShellDestination.History:
                case PreviewShellDestination.ProjectUpdate:
                    HistoryUpdateWorkspace.FocusInitialControl();
                    break;
                case PreviewShellDestination.Settings:
                    SettingsWorkspace.FocusInitialControl();
                    break;
                case PreviewShellDestination.AdvancedTools:
                    AdvancedToolsWorkspace.FocusInitialControl();
                    break;
                default:
                    PrimaryNavigation.Focus();
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FocusCurrentWorkflow();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F1)
            {
                return;
            }

            ShowKeyboardHelp();
            e.Handled = true;
        }

        private void KeyboardHelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowKeyboardHelp();
        }

        private void ShowKeyboardHelp()
        {
            _dialogService.Show(new PreviewDialogRequest(
                PreviewMessageCatalog.Get("Accessibility_KeyboardHelp_Title"),
                PreviewMessageCatalog.Get("Accessibility_KeyboardHelp_Content"),
                PreviewDialogSeverity.Information,
                false));
        }

        private void ShellServicesCloseRequested(object sender, EventArgs e)
        {
            _shellViewModel.IsShellServicesVisible = false;
            FocusCurrentWorkflow();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_settingsViewModel.TryDiscardForClose())
            {
                e.Cancel = true;
                return;
            }

            _settingsViewModel.Dispose();
            _advancedToolsViewModel.Dispose();
            ShellServicesWorkspace.CloseRequested -= ShellServicesCloseRequested;
            _shellViewModel.PropertyChanged -= ShellViewModelPropertyChanged;
            _projectHubViewModel.Dispose();
            _historyUpdateViewModel.Dispose();
            _reviewQualityViewModel.Dispose();
            _translationWorkspaceViewModel.Dispose();
            NIMApp.CloseAny();
        }
    }
}
