using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PhoenixEngine;
using NIM.ApplicationLayer;

namespace NIM
{
    /// <summary>
    /// Hosts startup progress and recoverable initialization failures before the main shell opens.
    /// </summary>
    public partial class SplashWindow : Window
    {
        private readonly PreviewDiagnosticService _diagnostics = new PreviewDiagnosticService();
        private bool _isInitializing;
        private bool _startupSucceeded;
        private bool _isLeftMouseDown;

        /// <summary>Creates the startup and recovery surface.</summary>
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>Gets the main shell after successful initialization.</summary>
        public static Window Main { get; private set; }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_startupSucceeded)
            {
                PhoenixApp.CloseAny();
            }
        }

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void WinHead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isLeftMouseDown = true;
            }

            if (!_isLeftMouseDown)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _isLeftMouseDown = false;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Version.Content = PhoenixApp.CurrentVersion;
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;
            LoadingPanel.Visibility = Visibility.Visible;
            FailurePanel.Visibility = Visibility.Collapsed;
            SetLogMessage("Startup_Initializing");
            _diagnostics.Record(PreviewDiagnosticSeverity.Information, "startup.initialization.started");
            try
            {
                string applicationPath = PhoenixApp.GetFullPath(@"\");
                await Task.Run(() =>
                {
                    PhoenixApp.PrepareFileDirectory();
                    Phoenix.Init(applicationPath, step => SetLogMessage(GetStartupMessageId(step)));
                });
                SetLogMessage("Startup_Launching");
                _diagnostics.Record(PreviewDiagnosticSeverity.Information, "startup.initialization.succeeded");
                Main = new ChooseLayout(_diagnostics);
                (Main as ChooseLayout).AutoShow();
                _startupSucceeded = true;

                Close();
            }
            catch (Exception exception)
            {
                _diagnostics.Record(
                    PreviewDiagnosticSeverity.Error,
                    "startup.initialization.failed",
                    exception.GetType().Name);
                FailureDetail.Text = PreviewMessageCatalog.Format(
                    "Startup_Failed_Detail",
                    exception.GetType().Name);
                LoadingPanel.Visibility = Visibility.Collapsed;
                FailurePanel.Visibility = Visibility.Visible;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private static string GetStartupMessageId(int step)
        {
            switch (step)
            {
                case 1: return "Startup_Loading_MasterDatabase";
                case 2: return "Startup_Loading_AdvancedDictionary";
                case 3: return "Startup_Loading_Cache";
                case 5: return "Startup_Loading_Conversion";
                case 6: return "Startup_Loading_FileKeys";
                case 7: return "Startup_Loading_Settings";
                case 8: return "Startup_Loading_Proxy";
                case 9: return "Startup_Loading_Vocabulary";
                case 10: return "Startup_Loading_Providers";
                default: return "Startup_Initializing";
            }
        }

        private void SetLogMessage(string messageId)
        {
            Dispatcher.Invoke(new Action(() => Log.Content = PreviewMessageCatalog.Get(messageId)));
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
