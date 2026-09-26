using System;
using System.Windows.Controls;

namespace NIM.UIManagement
{
    public class LogHelper
    {
        private static void SetLog(TextBox Handle, string Msg)
        {
            Handle.Dispatcher.BeginInvoke(new Action(() =>
            {
                Handle.Text = Msg;
                Handle.ScrollToEnd();
            }));
        }

        public static void SetInputLog(string Text)
        {
            if (PhoenixApp.WorkWin != null)
            {
                SetLog(PhoenixApp.WorkWin.InputLog, Text);
            }
        }

        public static void SetOutputLog(string Text)
        {
            if (PhoenixApp.WorkWin != null)
            {
                SetLog(PhoenixApp.WorkWin.OutputLog, Text);
            }
        }

        public static void SetMainLog(string Text)
        {
            if (PhoenixApp.WorkWin != null)
            {
                SetLog(PhoenixApp.WorkWin.MainLog, Text);
            }
        }
    }
}
