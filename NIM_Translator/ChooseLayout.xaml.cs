using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NIM.ApplicationLayer;
using NIM.UIManagement.Preview;

namespace NIM
{
    /// <summary>
    /// Interaction logic for ChooseLayout.xaml
    /// </summary>
    public partial class ChooseLayout : Window
    {
        public static bool ModernIsReady = false;
        public static bool ClassicIsReady = true;

        private readonly PreviewDiagnosticService _diagnostics;
        internal ChooseLayout(PreviewDiagnosticService diagnostics)
        {
            NIMApp.SelfSetting.ReadConfig();
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

            InitializeComponent();

          
        }

        private void RunModern()
        {
            NIMApp.CurrentLayout = new PreviewShellWindow(_diagnostics);
            NIMApp.CurrentLayout.Show();

            this.Close();
        }

        private void RunClassic()
        {
            NIMApp.WorkWin = new NIMGui(_diagnostics);
            NIMApp.CurrentLayout = NIMApp.WorkWin;
            NIMApp.WorkWin.Show();

            this.Close();
        }

        private void Modern_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MessageBoxExtend.Show(this, "Use this layout?", "Wuerfelhusten is still working on this interface, so the core functionality is currently incomplete; it is recommended to use the classic version for now~", PreviewDialogSeverity.Information, true))
            {
                NIMApp.SelfSetting.Layout = NIMLayout.Modern;
                RunModern();
            }
        }
        private void Classic_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            NIMApp.SelfSetting.Layout = NIMLayout.Classic;
            RunClassic();
        }

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            NIMApp.CloseAny();
        }

        private void Modern_MouseEnter(object sender, MouseEventArgs e)
        {
            Modern.BorderBrush = new SolidColorBrush(Color.FromRgb(250,227,6));
        }

        private void Modern_MouseLeave(object sender, MouseEventArgs e)
        {
            Modern.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));
        }

        private void Classic_MouseEnter(object sender, MouseEventArgs e)
        {
            Classic.BorderBrush = new SolidColorBrush(Color.FromRgb(250, 227, 6));
        }

        private void Classic_MouseLeave(object sender, MouseEventArgs e)
        {
            Classic.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));
        }
        public void ChangeState(Grid Parent,bool IsReady)
        {
            this.Dispatcher.Invoke(new Action(() => {
                if (Parent.Children.Count == 2)
                {
                    if (IsReady)
                    {
                        (Parent.Children[0] as Border).Visibility = Visibility.Visible;
                        (Parent.Children[1] as Border).Visibility = Visibility.Collapsed;

                    }
                    else
                    {
                        (Parent.Children[0] as Border).Visibility = Visibility.Collapsed;
                        (Parent.Children[1] as Border).Visibility = Visibility.Visible;
                    }
                }
            }));
        }
        public void AutoShow()
        {
            this.Hide();

            ChangeState(ModernState, ModernIsReady);
            ChangeState(ClassicState, ClassicIsReady);

            if (NIMApp.SelfSetting.Layout != NIMLayout.Null)
            {
                if (NIMApp.SelfSetting.Layout == NIMLayout.Modern)
                {
                    RunModern();
                }
                else
                if (NIMApp.SelfSetting.Layout == NIMLayout.Classic)
                {
                    RunClassic();
                }
            }
            else
            {
                this.Show();
            }
        }

        public bool IsLeftMouseDown = false;

        private void WinHead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IsLeftMouseDown = true;
            }

            if (IsLeftMouseDown)
            {
                try
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.DragMove();
                    }));

                    IsLeftMouseDown = false;
                }
                catch { }
            }
        }
    }
}
