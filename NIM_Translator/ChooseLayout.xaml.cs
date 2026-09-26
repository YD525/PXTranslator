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
            PhoenixApp.SelfSetting.ReadConfig();
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

            InitializeComponent();

          
        }

        private void RunModern()
        {
            PhoenixApp.CurrentLayout = new PreviewShellWindow(_diagnostics);
            PhoenixApp.CurrentLayout.Show();

            this.Close();
        }

        private void RunClassic()
        {
            PhoenixApp.WorkWin = new PhoenixGui(_diagnostics);
            PhoenixApp.CurrentLayout = PhoenixApp.WorkWin;
            PhoenixApp.WorkWin.Show();

            this.Close();
        }

        private void Modern_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PhoenixApp.SelfSetting.Layout = PhoenixLayout.Modern;
            RunModern();
        }
        private void Classic_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PhoenixApp.SelfSetting.Layout = PhoenixLayout.Classic;
            RunClassic();
        }

        private void Close_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PhoenixApp.CloseAny();
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

            if (PhoenixApp.SelfSetting.Layout != PhoenixLayout.Null)
            {
                if (PhoenixApp.SelfSetting.Layout == PhoenixLayout.Modern)
                {
                    RunModern();
                }
                else
                if (PhoenixApp.SelfSetting.Layout == PhoenixLayout.Classic)
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
