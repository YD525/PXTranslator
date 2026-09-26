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
            if (NIMApp.WorkWin != null)
            {
                SetLog(NIMApp.WorkWin.InputLog, Text);
            }
        }

        public static void SetOutputLog(string Text)
        {
            if (NIMApp.WorkWin != null)
            {
                SetLog(NIMApp.WorkWin.OutputLog, Text);
            }
        }

        public static void SetMainLog(string Text)
        {
            if (NIMApp.WorkWin != null)
            {
                SetLog(NIMApp.WorkWin.MainLog, Text);
            }
        }
    }
}
