using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using NIM.UIManagement;
using NIMEngine.ADO;
using NIMEngine.Language;
using NIMEngine.Memory;

namespace NIM
{
    /// <summary>
    /// Interaction logic for TraditionalConvert.xaml
    /// </summary>
    public partial class TraditionalConvert : Window
    {
        private TranslateView _Owner;
        public TraditionalConvert(TranslateView Owner)
        {
            InitializeComponent();
            this._Owner = Owner;
        }
     
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Owner = _Owner._Parent;
        }

        public Thread ConvertTrd = null;
        private void ConvertCurrent_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string GetOriginal = _Owner.FromStr.Text;
            ConvertTrd = new Thread(() =>
            {
                string Result = ChineseVariantMap.SimplifiedToTraditionalByReq(GetOriginal);

                if (Result.Length > 0)
                {
                    _Owner.Dispatcher.Invoke(new Action(() => {
                        _Owner.ToStr.Text = Result;
                    }));
                }

                ConvertTrd = null;
            });
            ConvertTrd.Start();
        }

        private void ConvertAll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ConvertTrd = new Thread(() =>
            {
                int Total = 0;


                for (int i = 0; i < _Owner.TransListView.RealLines.Count; i++)
                {
                    if (_Owner.TransListView.RealLines[i].Score <= 0)
                    {
                        continue;
                    }

                    string Source = _Owner.TransListView.RealLines[i].Source;

                    bool IsCloud = false;
                    _Owner.TransListView.RealLines[i].SyncData(this._Owner.Mod, ref IsCloud);

                    if (_Owner.TransListView.RealLines[i].Translated.Length == 0)
                    {
                        Total++;
                    }
                }

                int Current = 0;
                for (int i = 0; i < _Owner.TransListView.RealLines.Count; i++)
                {
                    if (_Owner.TransListView.RealLines[i].Score <= 0)
                    {
                        continue;
                    }

                    string Source = _Owner.TransListView.RealLines[i].Source;

                    bool IsCloud = false;
                    _Owner.TransListView.RealLines[i].SyncData(this._Owner.Mod,ref IsCloud);

                    if (_Owner.TransListView.RealLines[i].Translated.Length == 0)
                    {
                        Current++;

                        var Result = ChineseVariantMap.SimplifiedToTraditionalByReq(Source);

                        _Owner.TransListView.RealLines[i].Translated = Result;

                        var Key = _Owner.TransListView.RealLines[i].Key;

                        var Link = _Owner.Mod.P_Translator.GetLink();

                        Link[Key] = new P_String(Result,1);

                        ConvertAllBtn.Dispatcher.Invoke(new Action(() => {
                            _Owner.TransListView.RealLines[i].SyncData(_Owner.Mod, ref IsCloud);
                        }));

                        CloudDBCache.AddCache(_Owner.Mod.P_Translator.GetFileUniqueKey(), Key, (int)_Owner.Mod.P_Translator.To, Source, Result);

                        ConvertAllBtn.Dispatcher.Invoke(new Action(() => {
                            _Owner.TransListView.RealLines[i].SyncUI(_Owner.TransListView);
                        }));
                    } 

                    ConvertAllBtn.Dispatcher.Invoke(new Action(() => {
                        ConvertAllBtn.Content = string.Format("Converting ({0}/{1})", Current, Total);
                    }));
                }

                ConvertAllBtn.Dispatcher.Invoke(new Action(() => {
                    ConvertAllBtn.Content = "All to Traditional";
                }));

                ConvertTrd = null;
            });
            ConvertTrd.Start();
        }
    }
}
