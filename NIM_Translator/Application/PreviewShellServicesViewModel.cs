using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Presents authoritative versions, credits, licenses, and privacy-safe diagnostics in the preview shell.
    /// </summary>
    internal sealed class PreviewShellServicesViewModel : INotifyPropertyChanged
    {
        private readonly PreviewDiagnosticService _diagnostics;
        private readonly Func<string> _chooseExportPath;
        private readonly IPreviewDialogService _dialogs;
        private IReadOnlyList<PreviewDiagnosticEntry> _entries;
        private IReadOnlyList<PreviewComponentVersion> _components;

        /// <summary>
        /// Creates the shell services model over diagnostic, file-selection, and modal interaction boundaries.
        /// </summary>
        /// <param name="diagnostics">The bounded application-lifetime diagnostic service.</param>
        /// <param name="chooseExportPath">Selects a private diagnostic export destination.</param>
        /// <param name="dialogs">Shows user-safe export results.</param>
        internal PreviewShellServicesViewModel(
            PreviewDiagnosticService diagnostics,
            Func<string> chooseExportPath,
            IPreviewDialogService dialogs)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _chooseExportPath = chooseExportPath ?? throw new ArgumentNullException(nameof(chooseExportPath));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _entries = new PreviewDiagnosticEntry[0];
            _components = new PreviewComponentVersion[0];
            RefreshCommand = new PreviewShellCommand(parameter => Refresh());
            ExportDiagnosticsCommand = new PreviewShellCommand(parameter => ExportDiagnostics());
            Refresh();
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the authoritative Translator version.</summary>
        public string ProductVersion => typeof(PreviewShellServicesViewModel).Assembly.GetName().Version.ToString();

        /// <summary>Gets the known product and dependency versions.</summary>
        public IReadOnlyList<PreviewComponentVersion> Components
        {
            get => _components;
            private set
            {
                _components = value ?? new PreviewComponentVersion[0];
                OnPropertyChanged();
            }
        }

        /// <summary>Gets retained newest-first privacy-safe diagnostic events.</summary>
        public IReadOnlyList<PreviewDiagnosticEntry> Entries
        {
            get => _entries;
            private set
            {
                _entries = value ?? new PreviewDiagnosticEntry[0];
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasEntries));
            }
        }

        /// <summary>Gets whether at least one diagnostic event is retained.</summary>
        public bool HasEntries => Entries.Count > 0;

        /// <summary>Gets the command that refreshes versions and retained events.</summary>
        public ICommand RefreshCommand { get; private set; }

        /// <summary>Gets the command that exports a bounded redacted diagnostic report.</summary>
        public ICommand ExportDiagnosticsCommand { get; private set; }

        /// <summary>Gets localized project credits.</summary>
        public string CreditsText => PreviewMessageCatalog.Get("ShellServices_Credits_Content");

        /// <summary>Gets localized license routing guidance.</summary>
        public string LicenseText => PreviewMessageCatalog.Get("ShellServices_Licenses_Content");

        /// <summary>Refreshes authoritative component states and the retained event snapshot.</summary>
        internal void Refresh()
        {
            Components = _diagnostics.GetComponentVersions();
            Entries = _diagnostics.GetEntries();
        }

        private void ExportDiagnostics()
        {
            string path = _chooseExportPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                _diagnostics.Export(path);
                _diagnostics.Record(PreviewDiagnosticSeverity.Information, "diagnostics.export.succeeded");
                Refresh();
                _dialogs.Show(new PreviewDialogRequest(
                    PreviewMessageCatalog.Get("ShellServices_Diagnostics_ExportSuccessTitle"),
                    PreviewMessageCatalog.Get("ShellServices_Diagnostics_ExportSuccess"),
                    PreviewDialogSeverity.Information,
                    false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                _diagnostics.Record(
                    PreviewDiagnosticSeverity.Error,
                    "diagnostics.export.failed",
                    exception.GetType().Name);
                Refresh();
                _dialogs.Show(new PreviewDialogRequest(
                    PreviewMessageCatalog.Get("ShellServices_Diagnostics_ExportFailedTitle"),
                    PreviewMessageCatalog.Get("ShellServices_Diagnostics_ExportFailed"),
                    PreviewDialogSeverity.Error,
                    false));
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
