using System.Windows;
using NIM.UIManagement;
using PhoenixEngine.Common;
using PhoenixEngine.Engine.ADO;
using PhoenixEngine.Memory;
using PhoenixEngine.Translate;

namespace NIM
{
    /// <summary>
    /// Interaction logic for ReplaceWin.xaml
    /// </summary>
    public partial class ReplaceWin : Window
    {
        private TranslateView _Owner;
        public ReplaceWin(TranslateView Owner)
        {
            InitializeComponent();
            this._Owner = Owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //UIFindwhat.Content = UILanguageHelper.UICache["UIFindwhat"];
            //UIReplacewith.Content = UILanguageHelper.UICache["UIReplacewith"];
            //UIReplacein.Content = UILanguageHelper.UICache["UIReplacein"];
            //UIScope.Content = UILanguageHelper.UICache["UIScope"];
            //ReplaceButton.Content = UILanguageHelper.UICache["ReplaceButton"];

            Mode.Items.Clear();
            Mode.Items.Add("Source text");
            Mode.Items.Add("Translated text");

            Mode.SelectedValue = Mode.Items[Mode.Items.Count-1];

            Scope.Items.Clear();
            Scope.Items.Add("Current");
            Scope.Items.Add("All");

            Scope.SelectedValue = Scope.Items[0];
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (SourceStr.Text.Trim().Length > 0 && TargetStr.Text.Trim().Length > 0)
            {
                string GetMode = P_Convert.ObjToStr(Mode.SelectedValue);
                string GetScope = P_Convert.ObjToStr(Scope.SelectedValue);

                if (GetMode.Equals("Source text"))
                {
                    if (GetScope.Equals("Current"))
                    {
                        _Owner.ToStr.Text = _Owner.FromStr.Text.Replace(SourceStr.Text,TargetStr.Text);
                    }
                    else
                    {
                        if (_Owner.TransListView != null)
                        {
                            var GenRangeID = HistoryDBCache.GenRangeID();

                            for (int i = 0; i < _Owner.TransListView.Rows; i++)
                            {
                                var GetRow = _Owner.TransListView.RealLines[i];

                                bool IsCloud = false;
                                GetRow.SyncData(_Owner.Mod, ref IsCloud);

                                if (GetRow.Source.Contains(SourceStr.Text))
                                {
                                    string GetNewTrans = GetRow.Source.Replace(SourceStr.Text, TargetStr.Text);

                                    GetRow.Translated = GetNewTrans;

                                    _Owner.Mod.P_Translator.AutoSetLink(GetRow.Key, GetRow.Source, new P_String(GetRow.Translated,1,GenRangeID));

                                    GetRow.SyncUI(_Owner.TransListView);
                                }
                            }
                        }
                    }
                }
                else
                if (GetMode.Equals("Translated text"))
                {
                    if (GetScope.Equals("Current"))
                    {
                        _Owner.ToStr.Text = _Owner.ToStr.Text.Replace(SourceStr.Text, TargetStr.Text);
                    }
                    else
                    {
                        if (_Owner.TransListView != null)
                        {
                            var GenRangeID = HistoryDBCache.GenRangeID();

                            for (int i = 0; i < _Owner.TransListView.Rows; i++)
                            {
                                var GetRow = _Owner.TransListView.RealLines[i];

                                bool IsCloud = false;
                                GetRow.SyncData(_Owner.Mod,ref IsCloud);

                                if (GetRow.Translated.Contains(SourceStr.Text))
                                {
                                    string GetNewTrans = GetRow.Translated.Replace(SourceStr.Text, TargetStr.Text);

                                    GetRow.Translated = GetNewTrans;

                                    _Owner.Mod.P_Translator.AutoSetLink(GetRow.Key, GetRow.Source, new P_String(GetRow.Translated,1,GenRangeID));

                                    GetRow.SyncUI(_Owner.TransListView);
                                }
                            }
                        }
                    }
                }

                _Owner.ReSetHistoryPointer();
            }

            SourceStr.Text = string.Empty;
            TargetStr.Text = string.Empty;

            this.Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
