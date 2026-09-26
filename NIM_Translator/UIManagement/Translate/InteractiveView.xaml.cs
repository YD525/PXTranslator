using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;

namespace NIM
{
    /// <summary>
    /// Interaction logic for InteractiveView.xaml
    /// </summary>
    public partial class InteractiveView : Window
    {
        public static void CloseAll()
        {
            Application.Current.Dispatcher.Invoke(new Action(() => 
            {
                foreach (var Get in InteractiveView.Views)
                {
                    try
                    {
                        Get.CanClose = true;
                        Get.Close();
                    }
                    catch { }
                }

                InteractiveView.Views.Clear();
            }));
        }
       
        public static List<InteractiveView> Views = new List<InteractiveView>();
        public InteractiveView()
        {
            InitializeComponent();
        }

        private static readonly Regex RequestIdRegex = new Regex(@"<!--\s*Request ID:\s*_?(\d+)\s*-->", RegexOptions.Compiled);

        public bool IsRequestIdMatch(string Prompt, string Result)
        {
            var ID1 = ExtractRequestId(Prompt);
            var ID2 = ExtractRequestId(Result);

            if (ID1 == null || ID2 == null)
                return false;

            return ID1 == ID2;
        }

        public static string ExtractRequestId(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var match = RequestIdRegex.Match(text);
            return match.Success ? match.Groups[1].Value : null;
        }


        public bool CanExit = false;
        public string Received = "";
        public void SetSend(string Send)
        { 
            this.SendStr.Text = Send +"\r\n"+ "Preserve the comment <!-- Request ID: ... --> exactly as-is in the output.";
            Views.Add(this);
            this.Show();
        }
        private void CopySendStr(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(this.SendStr.Text);
        }
        private void ApplyStr(object sender, RoutedEventArgs e)
        {
            if (IsRequestIdMatch(this.SendStr.Text, this.RecvStr.Text))
            {
                this.Received = this.RecvStr.Text;
                this.CanExit = true;
            }
            else
            {
                MessageBoxExtend.Show(this,"Msg", "The current content Request ID verification failed!",ApplicationLayer.PreviewDialogSeverity.Warning);
                this.RecvStr.Text = string.Empty;
            }
        }

        public bool CanClose = false;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Let the translation thread close this window.
            CanExit = true;

            if(!CanClose)
            e.Cancel = true;
        }
    }
}
