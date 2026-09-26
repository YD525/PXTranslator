using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using NIM.SkyrimManagement;
using NIM.TranslateManage;
using NIM.UIManage;
using PhoenixEngine.Common;
using static PexInterface.PexHeuristicAnalysis;
using System.Windows.Media;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using NIM.IDEManagement;
using PhoenixEngine.ADO;
using PhoenixEngine.Unit;
using PhoenixEngine.Translate;
using PhoenixEngine.Additional;
using NIM.SkyrimModManager;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using static NIM.SkyrimManagement.DSDConverter;
using PhoenixEngine.Platform.LocalAI;
using PhoenixEngine;
using PhoenixEngine.Memory;
using PhoenixEngine.Engine.ADO;
using NIM.ApplicationLayer;
using NIM.ModParser;

namespace NIM.UIManagement
{
    public class SearchData
    {
        public string FristChar = "";
        public Dictionary<string, int> KeyWords = new Dictionary<string, int>();
    }
    /// <summary>
    /// Interaction logic for TranslateView.xaml
    /// </summary>
    public partial class TranslateView : UserControl
    {
        public YDListView TransListView = null;
        public string Path = "";
        public ModFile Mod = null;
        public Completer Completer = null;

        public TranslateView()
        {
            InitializeComponent();
        }

        public PhoenixGui _Parent = null;
        public void SetFile(PhoenixGui Parent, string Path)
        {
            if (Mod == null)
            {
                ScanAnimator = new ScanAnimator(this.ScanTransform, this.ProcessBar, 60.0);

                this._Parent = Parent;

                this.Path = Path;

                Mod = new ModFile(Path);

                if (Mod.Type == GameFileType.ESP)
                {
                    var SourceFilterStr = Mod.EspReader.GetFilterByStr();

                    if (NIMApp.SelfSetting.CustomFilterStr.Trim().Length > 0)
                    {
                        if (SourceFilterStr.ToUpper() != NIMApp.SelfSetting.CustomFilterStr.ToUpper())
                        {
                            var FilterDict = Mod.EspReader.ParseFilterString(NIMApp.SelfSetting.CustomFilterStr);
                            Mod.EspReader.SetFilter(FilterDict);
                        }
                    }
                }

                TransListView = new YDListView(Mod, TransView);
                TransListView.Clear();

                Mod.Win = this;

                TransListView.LineSelectedEvent += new YDListView.LineSelected((Key) =>
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        SetSelectFromAndToText(Key);
                        //Auto Show View
                        MultiWindowController.AttachMod(Key, _Parent, Mod);
                    }));
                });

                Mod.SetListView(TransListView);

                this.Mod.P_Translator.From = NIMApp.SelfSetting.SourceLanguage;
                this.Mod.P_Translator.To = NIMApp.SelfSetting.TargetLanguage;

                this.ReloadStringsFile();

                Mod.Load();

                CompletionManager = new WordCompletionManager(ToStr);

                Completer = new Completer(this);

                this.Completer?.CheckLang(this.Mod.P_Translator.To);

                SyncConfig();

                if (Mod.Type != GameFileType.ESP)
                {
                    ReloadData();
                }

                ReSetTypes();

                UIHelper.SyncAvalonEditTextLayout(this);

                AutoShowTraditional();

                SyncTrd = new Thread(() =>
                {
                    while (true)
                    {
                        if (CanExitSyncTrd)
                        {
                            return;
                        }

                        Thread.Sleep(1000);

                        try
                        {
                            this.SyncTranslationStatus();
                        }
                        catch
                        {
                        }
                    }
                });

                SyncTrd.Start();

                if (!this.CheckDictionary())
                {
                    this.RefreshDictionary.Opacity = 0.5;
                }
                else
                {
                    this.RefreshDictionary.Opacity = 1.0;
                }

                ReSetHistoryPointer();
                CurrentHistory?.RefreshData();

                //Automatically shrink records when there are more than 10,000 records.
                HistoryDBCache.CompactHistory(this.Mod.P_Translator.GetFileUniqueKey());
            }
        }

        //If the user uses Ctrl + Z to scroll back multiple times, the current key may not be the latest. The key selection needs to be reset when reopening the file, including when applying string translations.
        public void ReSetHistoryPointer()
        {
            var GetLastID = HistoryDBCache.GetLastID(this.Mod.P_Translator.GetFileUniqueKey());
            if (GetLastID > 0)
            {
                HistoryDBCache.SelectID(this.Mod.P_Translator.GetFileUniqueKey(), GetLastID);
            }
        }

        public void Active()
        {
            SyncListView();
        }

        private DispatcherTimer _SyncTimer;
        private double DefWindowWidth = 0;
        public void SyncListView()
        {
            if (_SyncTimer == null)
            {
                _SyncTimer = new DispatcherTimer();
                _SyncTimer.Interval = TimeSpan.FromMilliseconds(100);

                _SyncTimer.Tick += (s, e) =>
                {
                    _SyncTimer.Stop();

                    if (NIMApp.WorkWin != null)
                        if (DefWindowWidth != NIMApp.WorkWin.ActualWidth)
                        {
                            TransListView.HotReload();
                            DefWindowWidth = NIMApp.WorkWin.ActualWidth;
                        }
                };
            }

            _SyncTimer.Stop();
            _SyncTimer.Start();
        }

        public void ReSetTypes()
        {
            List<string> Types = new List<string>();

            TypeSelector.Items.Clear();

            switch (Mod.Type)
            {
                case GameFileType.ESP:
                    {
                        Types.Add("ALL");

                        if (Mod.EspReader.Types != null)
                        {
                            Types.AddRange(Mod.EspReader.Types);
                        }
                    }
                    break;
                case GameFileType.PEX:
                    {
                        Types.Add("ALL");
                    }
                    break;
                default:
                    {
                        Types.Add("ALL");
                    }
                    break;
            }

            foreach (var GetType in Types)
            {
                TypeSelector.Items.Add(GetType);
            }

            TypeSelector.SelectedValue = TypeSelector.Items[0];
        }

        public void Close()
        {
            DisposeAny();
            Mod?.Close();
        }

        public void InitIDE()
        {
            string GetName = "NIM" + ".IDERule.TextStyle.xshd";

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

            using (System.IO.Stream s = assembly.GetManifestResourceStream(GetName))
            {
                using (System.Xml.XmlTextReader reader = new System.Xml.XmlTextReader(s))
                {
                    var xshd = HighlightingLoader.LoadXshd(reader);

                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        FromStr.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                        ToStr.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                    }));
                }
            }
        }

        public void SyncConfig()
        {
            InitIDE();

            if (NIMApp.SelfSetting.ViewMode == "Normal")
            {
                EnableNormalModel();

            }
            else
            {
                EnableQuickModel();
                EmptyFromAndToText();
            }

            if (NIMApp.SelfSetting.CanClearCloudTranslationCache)
            {
                CloudTranslationCache.IsChecked = true;
            }
            else
            {
                CloudTranslationCache.IsChecked = false;
            }

            if (NIMApp.SelfSetting.CanClearUserInputTranslationCache)
            {
                UserTranslationCache.IsChecked = true;
            }
            else
            {
                UserTranslationCache.IsChecked = false;
            }

            if (NIMApp.SelfSetting.AutoSpeak)
            {
                AutoSpeak.IsChecked = true;
            }
            else
            {
                AutoSpeak.IsChecked = false;
            }

            if (NIMApp.SelfSetting.TableAuto)
            {
                SetHotKeyDot(true);
                NextAutoEnable = 1;
                _AutoLoop = true;
            }

            if (NIMApp.SelfSetting.WordCompletion)
            {
                AutoWordCompletion.IsChecked = true;
            }
            else
            {
                AutoWordCompletion.IsChecked = false;
            }

            if (NIMApp.WordCompleter == null)
            {
                HideWordCompletion();
            }
            else
            {
                ShowWordCompletion();
            }
        }

        public void HideWordCompletion()
        {
            AutoWordCompletion.Visibility = Visibility.Collapsed;
            UIAutoWordCompletion.Visibility = Visibility.Collapsed;
        }

        public void ShowWordCompletion()
        {
            AutoWordCompletion.Visibility = Visibility.Visible;
            UIAutoWordCompletion.Visibility = Visibility.Visible;
        }


        public int NextAutoEnable = 0;
        private const string ToolTipNextAutoOff = "Auto Next: OFF — Click to enable. When enabled, pressing Tab will automatically jump to the next untranslated entry.";
        private const string ToolTipNextAutoOn = "Auto Next: ON — Tab key is now redirected to jump to the next untranslated entry. Click to disable.";
        private bool _AutoLoop = false;

        public void SetHotKeyDot(bool Enable)
        {
            if (Enable)
            {
                HotKeyDot.Fill = new SolidColorBrush(Color.FromRgb(247, 241, 186));
                HotKeyArea.ToolTip = ToolTipNextAutoOn;
            }
            else
            {
                HotKeyDot.Fill = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
                HotKeyArea.ToolTip = ToolTipNextAutoOff;
            }
        }

        public bool CheckDictionary()
        {
            return Mod.OriginalDictionaryReader.CheckDictionary();
        }

        public void SetLog(string Str)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                CurrentLog.Text = "Log: " + Str;
            }));
        }

        public string LastSetKey = "";

        public void EmptyFromAndToText()
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                CurrentKeyBox.Visibility = Visibility.Collapsed;
                FromStr.Text = string.Empty;
                ToStr.Text = string.Empty;
            }));
            LastSetKey = string.Empty;
        }

        public void SetSelectFromAndToText(string Key)
        {
            EmptyFromAndToText();

            LastSetKey = Key;

            if (Mod.Type == GameFileType.ESP)
            {
                if (Mod.EspReader.Records.ContainsKey(LastSetKey))
                {
                    var GetRecord = Mod.EspReader.Records[LastSetKey];
                    SetLog("KEY:" + Key + ",EDITID:" + GetRecord.EditorID);
                }
            }
            else
            {
                SetLog("KEY:" + LastSetKey);
            }

            if (Mod.Type == GameFileType.ESP)
            {
                if (Mod.EspReader.GameCharacters.ContainsKey(Key))
                {
                    NpcView.Visibility = Visibility.Visible;
                    NpcName.Text = Mod.EspReader.GameCharacters[Key][0].Name;
                    NpcSex.Content = Mod.EspReader.GameCharacters[Key][0].Gender.ToString();
                }
                else
                {
                    NpcView.Visibility = Visibility.Collapsed;
                }
            }


            if (Key.Length > 0)
            {
                CurrentKeyBox.Visibility = Visibility.Visible;
            }

            if (TransListView != null)
            {
                var GridHandle = TransListView.KeyToFakeGrid(Key);

                if (GridHandle != null)
                {
                    if (GridHandle.Score < 0)
                    {
                        LastSetKey = string.Empty;
                        return;
                    }

                    bool IsCloud = false;
                    GridHandle.SyncData(Mod, ref IsCloud);

                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        if (GridHandle.Score < 5)
                        {
                            ToStr.Foreground = new SolidColorBrush(Colors.Red);
                        }
                        else
                        {
                            ToStr.Foreground = new SolidColorBrush((Color)Application.Current.Resources["DefFontColor"]);
                        }


                        FromStr.Text = TransListView.RealLines[TransListView.SelectLineID].Source;
                        ToStr.Text = TransListView.RealLines[TransListView.SelectLineID].Translated;

                        UIHelper.ShowButton(ApplyOTButton, true);

                        if (FromStr.Text.Length > 0)
                        {
                            UIHelper.ShowButton(CancelOTButton, true);
                        }

                        if (NIMApp.SelfSetting.AutoSpeak)
                        {
                            SpeechHelper.TryPlaySound(this.Mod.P_Translator.From,FromStr.Text, true);
                        }

                        Point MousePos = Mouse.GetPosition(ToStr);
                        if (MousePos.X >= 0 && MousePos.X <= ToStr.ActualWidth &&
                            MousePos.Y >= 0 && MousePos.Y <= ToStr.ActualHeight)
                        {
                            ToStr.Focus();
                            ToStr.CaretOffset = ToStr.Text.Length;
                        }
                    }));

                    //DeFine.ExtendWin.SetOriginal(GridHandle.SourceText, Mod.EspReader.ToStringsFile.QueryData(GridHandle.Key));
                }
            }
        }

        private System.Timers.Timer ReloadDebounceTimer;
        private readonly object ReloadLock = new object();
        private bool UseHotReloadFlag;

        public bool ReadTrdWorkState = false;
        public void ReloadData(bool UseHotReload = false, bool ForceReload = false)
        {
            lock (ReloadLock)
            {
                UseHotReloadFlag = UseHotReload;

                if (ForceReload)
                {
                    ReloadDebounceTimer = null;
                }

                if (ReloadDebounceTimer == null)
                {
                    ReloadDebounceTimer = new System.Timers.Timer(200);
                    ReloadDebounceTimer.AutoReset = false;
                    ReloadDebounceTimer.Elapsed += (s, e) =>
                    {
                        if (!ReadTrdWorkState)
                        {
                            new Thread(() =>
                            {
                                ReloadDataFunc(UseHotReloadFlag);
                            }).Start();
                        }
                    };
                }

                ReloadDebounceTimer.Stop();
                ReloadDebounceTimer.Start();
            }
        }
        public void AutoShowTraditional()
        {
            bool IsVisible = false;
            if (NIMApp.SelfSetting.SourceLanguage == Languages.SimplifiedChinese || NIMApp.SelfSetting.SourceLanguage == Languages.TraditionalChinese)
            {
                if (NIMApp.SelfSetting.SourceLanguage == Languages.TraditionalChinese || NIMApp.SelfSetting.SourceLanguage == Languages.SimplifiedChinese)
                {
                    Traditional.Visibility = Visibility.Visible;
                    IsVisible = true;
                }
            }

            if (!IsVisible)
            {
                Traditional.Visibility = Visibility.Collapsed;
            }
        }
        public void ReloadStringsFile()
        {
            if (Mod.Type == GameFileType.ESP)
            {
                Mod.LoadStringsFile();

                if (Mod.FromStringsFile.Strings.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        FromStringsFile.Visibility = Visibility.Visible;
                        UIHelper.SyncFromStringsFile(Mod, TransListView);
                    }));
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        FromStringsFile.Visibility = Visibility.Collapsed;
                    }));
                }
            }
        }

        public object LockerAddTrd = new object();
        public string LastSetSig = "";
        public Thread DataLoadingTrd = null;


        public event Action OnDataReloadCompleted;

        public bool DataLoading = false;

        public string CurrentSig = "";
        public void ReloadDataFunc(bool UseHotReload = false)
        {
            lock (LockerAddTrd)
            {
                DataLoading = true;
                ReadTrdWorkState = true;

                if (LastSetSig != CurrentSig)
                {
                    LastSetSig = CurrentSig;
                    Mod.CancelTranslateWork();
                }

                if (!UseHotReload)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        TransListView.Clear();
                    }));
                }
                else
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        TransListView.HotReload();
                    }));
                }

                if (!UseHotReload)
                {
                    if (Mod.Type == GameFileType.ESP)
                    {
                        Mod.EspReader.Query();

                        if (DataLoadingTrd != null)
                        {
                            try
                            {
                                DataLoadingTrd.Abort();
                            }
                            catch { }

                            DataLoadingTrd = null;

                            TransListView.Parent.Dispatcher.Invoke(new Action(() =>
                            {
                                TransListView.Clear();
                            }));
                        }

                        DataLoadingTrd = new Thread(() =>
                        {
                            UIHelper.TransViewSyncEspRecord(Mod.EspReader, CurrentSig, TransListView);

                            ReloadStringsFile();

                            Thread.Sleep(100);

                            DataLoading = false;

                            DataLoadingTrd = null;
                        });

                        DataLoadingTrd.Start();
                    }
                    else
                    if (Mod.Type == GameFileType.MCM)
                    {
                        foreach (var GetItem in Mod.GetRecords<MCMStrings>())
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                TransListView.AddRowR(LineRenderer.CreateLine("MCM", GetItem.Value.EditID, GetItem.Key, GetItem.Value.Source, GetItem.Value.Translated, 999));
                            }));
                        }

                        DataLoading = false;
                    }
                    else
                    if (Mod.Type == GameFileType.PEX)
                    {
                        string AutoSig = CurrentSig;
                        if (AutoSig == "ALL")
                        {
                            AutoSig = string.Empty;
                        }

                        foreach (var GetItem in Mod.GetRecords<PexStrings>())
                        {
                            if (GetItem.Value.FunctionRef != null)
                            {
                                int CalcLineIndex = GetItem.Value.FunctionRef.PscStartLineIndex;

                                if (Mod.PexReader.PexLinks.ContainsKey(GetItem.Key))
                                {
                                    Mod.PexReader.PexLinks[GetItem.Key] = CalcLineIndex;
                                }
                                else
                                {
                                    Mod.PexReader.PexLinks.Add(GetItem.Key, CalcLineIndex);
                                }
                            }

                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                TransListView.AddRowR(LineRenderer.CreateLine("Auto", P_Convert.ObjToStr(GetItem.Value.StringTableID), GetItem.Key, GetItem.Value.Source, "", GetItem.Value.Score));
                            }));
                        }

                        DataLoading = false;
                    }
                    else
                    if (Mod.Type == GameFileType.JSON)
                    {
                        foreach (var GetItem in Mod.RamCacheReader.RamLines)
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                TransListView.AddRowR(LineRenderer.CreateLine(GetItem.Type, "", GetItem.Key, GetItem.Source, GetItem.Translated, GetItem.Score));
                            }));
                        }

                        DataLoading = false;
                    }
                    else
                    if (Mod.Type == GameFileType.XML)
                    {
                        foreach (var GetItem in Mod.GetRecords<XmlStrings>())
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                TransListView.AddRowR(LineRenderer.CreateLine("XML", GetItem.Value.EDID, GetItem.Key, GetItem.Value.Source, "", 999));
                            }));
                        }

                        DataLoading = false;
                    }
                }

                ReadTrdWorkState = false;

                this.Dispatcher.Invoke(new Action(() =>
                {
                    TransListView.UpdateVisibleRows(true);
                }));

                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
                {
                    OnDataReloadCompleted?.Invoke();
                }));
            }
        }

        public void SyncTransStateUI()
        {
            TStop.Opacity = 0.5;

            if (Mod.TranslationStatus == StateControl.Run)
            {
                TRun.Visibility = Visibility.Collapsed;
                TStop.Visibility = Visibility.Visible;
                TCancel.Visibility = Visibility.Visible;

                ThreadInFo.Visibility = Visibility.Visible;
            }
            else
            if (Mod.TranslationStatus == StateControl.Stop)
            {
                TStop.Opacity = 1;
                TRun.Visibility = Visibility.Collapsed;
                TStop.Visibility = Visibility.Visible;
                TCancel.Visibility = Visibility.Visible;

                ThreadInFo.Visibility = Visibility.Visible;
            }
            else
            if (Mod.TranslationStatus == StateControl.Cancel || Mod.TranslationStatus == StateControl.Null)
            {
                TRun.Visibility = Visibility.Visible;
                TStop.Visibility = Visibility.Collapsed;
                TCancel.Visibility = Visibility.Collapsed;

                ThreadInFo.Visibility = Visibility.Collapsed;
            }

            if (Mod.TranslationStatus == StateControl.Run || Mod.TranslationStatus == StateControl.Stop)
            {
                this._Parent.TranslateConfigView.SFrom.IsEnabled = false;
                this._Parent.TranslateConfigView.STo.IsEnabled = false;
            }
            else
            {
                this._Parent.TranslateConfigView.SFrom.IsEnabled = true;
                this._Parent.TranslateConfigView.STo.IsEnabled = true;
            }
        }

        private void ChangeTransState(object sender, MouseButtonEventArgs e)
        {
            bool IsKeep = false;
            bool CallSuccess = false;
            if (TransListView != null)
            {
                if (TransListView.Rows > 0)
                {
                    if (sender is Border)
                    {
                        Border ButtonHandle = (Border)sender;

                        string GetButtonName = ButtonHandle.Name;

                        switch (GetButtonName)
                        {
                            case "TRun":
                                {
                                    if (P_Convert.ObjToStr(TransProcess.Content).StartsWith("STRINGS("))
                                    {
                                        if (Mod.P_Translator.From == Mod.P_Translator.To)
                                        {
                                            MessageBoxExtend.Show(this._Parent,"Msg", "The source language and target language cannot be the same!",PreviewDialogSeverity.Warning);
                                            CallSuccess = false;

                                            ShowLocalEngineSettingView();
                                            return;
                                        }

                                        if (!Phoenix.CheckAvailableNodes())
                                        {
                                            MessageBoxExtend.Show(this._Parent, "Msg", "Please enable at least one translation platform node.",PreviewDialogSeverity.Warning);
                                            CallSuccess = false;

                                            if (!_Parent.IsNodeExpanded)
                                            {
                                                _Parent.SyncAnimation();
                                                _Parent.ShowNodeMenu(true);
                                                _Parent.LogView.Visibility = Visibility.Visible;
                                            }
                                            return;
                                        }

                                        if (NIMApp.EngineSetting.GetPlatformData(LMStudio.Type).Enable)
                                        {
                                            LMStudio.CurrentModel = string.Empty;
                                        }

                                        TRun.Visibility = Visibility.Collapsed;

                                        Mod.TranslationStatus = StateControl.Run;
                                        CallSuccess = true;
                                        IsKeep = false;
                                    }
                                }
                                break;
                            case "TStop":
                                {
                                    if (TStop.Opacity == 0.5)
                                    {
                                        Mod.TranslationStatus = StateControl.Stop;
                                        CallSuccess = true;
                                    }
                                    else
                                    {
                                        if (Mod.TranslationStatus == StateControl.Stop)
                                        {
                                            IsKeep = true;
                                        }

                                        Mod.TranslationStatus = StateControl.Run;
                                        CallSuccess = true;
                                    }
                                }
                                break;
                            case "TCancel":
                                {
                                    Mod.TranslationStatus = StateControl.Cancel;
                                    CallSuccess = true;
                                }
                                break;
                        }

                        if (CallSuccess)
                        {
                            Mod.SyncTransState(new Action(() =>
                            {
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    SyncTransStateUI();
                                }));
                            }), IsKeep);
                        }
                    }
                }
            }
            if (!CallSuccess)
            {
                MessageBoxExtend.Show(this._Parent,"Msg", "Batch translation is not possible at the current state.\nPlease wait until the file loading is finished.",PreviewDialogSeverity.Information);
            }
        }

        private void AutoSpeak_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSpeak.IsChecked == true)
            {
                NIMApp.SelfSetting.AutoSpeak = true;
            }
            else
            {
                NIMApp.SelfSetting.AutoSpeak = false;
            }
        }

        private void ReplaceStr(object sender, MouseButtonEventArgs e)
        {
            new ReplaceWin(this).Show();
        }


        private string LastUntranslatedKey = null;
        private void EnableHotKey_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _AutoLoop = !_AutoLoop;

            LastUntranslatedKey = null;

            if (_AutoLoop)
            {
                SetHotKeyDot(true);
                NextAutoEnable = 1;

                NIMApp.SelfSetting.TableAuto = true;
                NIMApp.SelfSetting.SaveConfig();
            }
            else
            {
                SetHotKeyDot(false);
                NextAutoEnable = 0;

                NIMApp.SelfSetting.TableAuto = false;
                NIMApp.SelfSetting.SaveConfig();
            }

            e.Handled = true;
        }

        private void ExportToDsd_Click(object sender, RoutedEventArgs e)
        {
            if (TransListView != null)
            {
                if (Mod.Type == GameFileType.ESP && TransListView.Rows > 0)
                {
                    if (Mod.EspReader.Records != null)
                    {
                        var GetWritePath = DataHelper.ShowSaveFileDialog(Mod.FileName + ".json", "DSD (*.json)|*.json");

                        var DSDFile = DSDConverter.RecordsToDSDFile(Mod, Mod.EspReader);
                        if (DSDFile != null)
                        {
                            if (DSDFile.DSDItems.Count > 0)
                            {
                                List<DSDItem> DSDItems = new List<DSDItem>();
                                DSDItems = DSDFile.DSDItems;
                                string GetJson = JsonConvert.SerializeObject(DSDItems, Formatting.Indented);

                                if (File.Exists(GetWritePath))
                                {
                                    File.Delete(GetWritePath);
                                }
                                DataHelper.WriteFile(GetWritePath, Encoding.UTF8.GetBytes(GetJson));
                            }
                        }
                    }
                }
                else
                {
                    MessageBoxExtend.Show(this._Parent,"Msg", "The current file does not support exporting to DSD format.", ApplicationLayer.PreviewDialogSeverity.Information);
                }
            }
        }
        public void UPDateFile(bool CanSetSource)
        {
            if (TransListView != null)
            {
                for (int i = 0; i < TransListView.Rows; i++)
                {
                    bool IsCloud = false;

                    TransListView.RealLines[i].SyncData(Mod, ref IsCloud);

                    string GetKey = TransListView.RealLines[i].Key;

                    string GetTransText = TransListView.RealLines[i].Translated;

                    if (CanSetSource)
                    {
                        if (string.IsNullOrEmpty(GetTransText))
                        {
                            GetTransText = TransListView.RealLines[i].Source;
                        }
                    }

                    var Link = Mod.P_Translator.GetLink();
                    Link[GetKey] = new P_String(GetTransText,0);
                }
            }
        }

        private void ExportToRamCache_Click(object sender, RoutedEventArgs e)
        {
            if (TransListView != null)
            {
                if (TransListView.Rows > 0)
                {
                    var GetWritePath = DataHelper.ShowSaveFileDialog(Mod.FileName + "_C.Json", "RamCache (*.Json)|*.Json");

                    UPDateFile(false);

                    string GetJson = JsonConvert.SerializeObject(TransListView.RealLines, Formatting.Indented);

                    if (GetWritePath != null)
                    {
                        if (GetWritePath.Trim().Length > 0)
                        {
                            if (File.Exists(GetWritePath))
                            {
                                File.Delete(GetWritePath);
                            }
                            DataHelper.WriteFile(GetWritePath, Encoding.UTF8.GetBytes(GetJson));
                        }
                    }
                }
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            NIMApp.SelfSetting.WritingAreaHeight = WritingArea.Height.Value;
        }

        private void AutoWordCompletion_Click(object sender, RoutedEventArgs e)
        {
            if (AutoWordCompletion.IsChecked == true)
            {
                NIMApp.SelfSetting.WordCompletion = true;
            }
            else
            {
                NIMApp.SelfSetting.WordCompletion = false;
            }

            NIMApp.SelfSetting.SaveConfig();
        }

        private void ImportRamCache_Click(object sender, RoutedEventArgs e)
        {
            var Dialog = new System.Windows.Forms.OpenFileDialog();
            Dialog.Title = "Please select a file";
            Dialog.Filter = "All files|*.*";
            Dialog.Multiselect = false;

            if (Dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string SelectedFile = Dialog.FileName;

                if (File.Exists(SelectedFile))
                {
                    if (TransListView != null)
                    {
                        if (TransListView.Rows > 0)
                        {
                            string GetRamCache = Encoding.UTF8.GetString(DataHelper.ReadFile(SelectedFile));
                            List<FakeGrid> RealLines = JsonConvert.DeserializeObject<List<FakeGrid>>(GetRamCache);

                            if (RealLines != null)
                            {
                                for (int i = 0; i < RealLines.Count; i++)
                                {
                                    if (RealLines[i].Source != RealLines[i].Translated)
                                    {
                                        Mod.P_Translator.SetLink(RealLines[i].Key,new P_String(RealLines[i].Translated,0));
                                    }
                                }

                                for (int i = 0; i < TransListView.Rows; i++)
                                {
                                    bool IsCloud = false;
                                    TransListView.RealLines[i].SyncData(Mod, ref IsCloud);
                                    TransListView.RealLines[i].SyncUI(TransListView);
                                }
                            }
                        }
                    }
                }
            }
        }

        private ScanAnimator ScanAnimator = null;
        private void ProcessBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScanAnimator != null)
            {
                if (Mod.P_Translator != null)
                {
                    var GetBatchCore = Mod.P_Translator.GetBatchCore();
                    if (GetBatchCore != null)
                        if (GetBatchCore.IsWorking && !GetBatchCore.IsStopped)
                        {
                            ScanAnimator.UpdateAnimationTarget();
                        }
                }
            }
        }

        private void RefreshDictionary_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MessageBoxExtend.Show(this._Parent, "Msg", "Are you sure you want to refresh the original text record? Doing so will lose the mod's original text information.", PreviewDialogSeverity.Warning) == false)
            {
                return;
            }
            if (!RefreshDictionary.IsEnabled)
            {
                return;
            }

            new Thread(() =>
            {
                RefreshDictionary.Dispatcher.Invoke(new Action(() =>
                {
                    RefreshDictionary.IsEnabled = false;
                }));
                var FileUniqueKey = Mod.P_Translator.GetFileUniqueKey();

                if (FileUniqueKey > 0)
                {
                    int CallFuncCount = 0;

                    string SetPath = NIMApp.GetFullPath(@"\Library\" + Mod.P_Translator.LastLoadFileName + ".Json");

                    if (File.Exists(SetPath))
                    {
                        File.Delete(SetPath);

                        CallFuncCount++;
                    }

                    Mod.OriginalDictionaryReader.Dictionary.Clear();

                    if (TransListView != null)
                    {
                        TransListView.QuickRefresh(Mod);
                    }

                    MessageBoxExtend.Show(this._Parent,"Msg", "Original source text has been refreshed from the current file.", ApplicationLayer.PreviewDialogSeverity.Information);
                }

                RefreshDictionary.Dispatcher.Invoke(new Action(() =>
                {
                    RefreshDictionary.IsEnabled = true;
                }));
            }).Start();
        }



        public SearchData CurrentSearchData = new SearchData();
        public void QuickSearch(bool MatchCase, bool FuzzyMatch)
        {
            if (SearchBox.Text.Trim().Length > 0)
            {
            NextSearch:
                string FristChar = SearchBox.Text.Substring(0, 1);

                if (!CurrentSearchData.FristChar.Equals(FristChar))
                {
                    CurrentSearchData.KeyWords.Clear();
                }

                CurrentSearchData.FristChar = FristChar;

                string SearchAny = SearchBox.Text;
                EmptyFromAndToText();

                int PreOffset = -1;
                int Complete = 0;
                string GetKey = "";

                StringComparison ComparisonType = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                Func<string, bool> IsTextMatch = (Text) =>
                {
                    if (string.IsNullOrEmpty(Text)) return false;

                    if (FuzzyMatch)
                    {
                        return Text.IndexOf(SearchAny, ComparisonType) >= 0;
                    }
                    else
                    {
                        return Text.Equals(SearchAny, ComparisonType);
                    }
                };

                for (int i = 0; i < TransListView.RealLines.Count; i++)
                {
                    if ((TransListView.RealLines[i].Key != null && TransListView.RealLines[i].Key.Equals(SearchAny, ComparisonType)) ||
                        IsTextMatch(TransListView.RealLines[i].Source) ||
                        IsTextMatch(TransListView.RealLines[i].Translated))
                    {
                        GetKey = TransListView.RealLines[i].Key;

                        if (CurrentSearchData.KeyWords.ContainsKey(SearchAny))
                        {
                            PreOffset = CurrentSearchData.KeyWords[SearchAny];
                        }
                        else
                        {
                            PreOffset = -1;
                            CurrentSearchData.KeyWords.Add(SearchAny, PreOffset);
                        }

                        if (i > PreOffset)
                        {
                            TransListView.Goto(GetKey);
                            CurrentSearchData.KeyWords[SearchAny] = i;
                            Complete = 1;
                            break;
                        }
                    }
                }

                if (PreOffset != -1 && Complete == 0 && CurrentSearchData.KeyWords.Count > 0)
                {
                    CurrentSearchData.KeyWords.Remove(SearchAny);
                    goto NextSearch;
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        SearchBox.Focus();
                    }));
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox TextBox)
                {
                    var MatchCaseBtn = TextBox.Template.FindName("IsMatchWholeWordBtn", TextBox) as System.Windows.Controls.Primitives.ToggleButton;
                    var FuzzyMatchBtn = TextBox.Template.FindName("IsMatchWildcardBtn", TextBox) as System.Windows.Controls.Primitives.ToggleButton;

                    bool MatchCase = MatchCaseBtn?.IsChecked ?? false;
                    bool FuzzyMatch = FuzzyMatchBtn?.IsChecked ?? true;

                    QuickSearch(MatchCase, FuzzyMatch);
                }
            }
        }

        private void ShowLocalEngineSettingView()
        {
            this._Parent.TranslateConfigView.Owner = this._Parent;
            this._Parent.TranslateConfigView.Init();
            this._Parent.TranslateConfigView.Show();
        }

        private void SyncColumnWidth(object sender, MouseButtonEventArgs e)
        {
            SyncColumnWidth();
        }

        public void SyncColumnWidth()
        {
            for (int i = 0; i < this.TransListView.VisibleRows.Count; i++)
            {
                Grid GetGrid = ((Border)(this.TransListView.VisibleRows[i].View.Children[0])).Child as Grid;

                for (int ir = 0; ir < GetGrid.ColumnDefinitions.Count; ir++)
                {
                    GetGrid.ColumnDefinitions[ir].Width = TransViewHeader.ColumnDefinitions[ir].Width;
                }
            }
        }

        private void TestAll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            for (int i = 0; i < TransListView.RealLines.Count; i++)
            {
                TransListView.RealLines[i].Translated = TransListView.RealLines[i].Source + "(" + i.ToString() + ")";

                var Link = Mod.P_Translator.GetLink();
                Link[TransListView.RealLines[i].Key] = new P_String(TransListView.RealLines[i].Translated,0);

                TransListView.RealLines[i].SyncUI(TransListView);
            }
        }

        private void Traditional_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            new TraditionalConvert(this).Show();
        }

        public void CheckCanClearCache(out bool Check)
        {
            if ((CloudTranslationCache.IsChecked == true || UserTranslationCache.IsChecked == true) == false)
            {
                Check = false;
                ClearCacheR.Opacity = 0.5;
                ClearCacheR.Cursor = null;
                ShowDataBaseR.Opacity = 0.5;
                ShowDataBaseR.Cursor = Cursors.Hand;
            }
            else
            {
                Check = true;
                ClearCacheR.Opacity = 1;
                ClearCacheR.Cursor = Cursors.Hand;
                ShowDataBaseR.Opacity = 1;
                ShowDataBaseR.Cursor = Cursors.Hand;
            }
        }

        public bool CacheViewIsShow = false;
        private void ManageCache_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Mod.State != GameFileState.Load)
            {
                MessageBoxExtend.Show(this._Parent, "Msg","Only currently open files can have their cache cleared.", ApplicationLayer.PreviewDialogSeverity.Information);
                return;
            }

            CheckCanClearCache(out bool Check);

            ClearCacheView.Visibility = Visibility.Visible;
            Mask.Visibility = Visibility.Visible;
            CacheViewIsShow = true;
        }


        private void FindNpc_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            NPCFinder NNPCFinder = new NPCFinder(Mod);
            NNPCFinder.Owner = _Parent;
            NNPCFinder.Show();
        }

        private void SpeakFromStr(object sender, MouseButtonEventArgs e)
        {
            SpeechHelper.TryPlaySound(this.Mod.P_Translator.From,FromStr.Text);
        }

        private void SpeakToStr(object sender, MouseButtonEventArgs e)
        {
            SpeechHelper.TryPlaySound(this.Mod.P_Translator.To, ToStr.Text);
        }
        private void UPSelecter_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            TransListView?.UP();
        }

        private void DownSelecter_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            TransListView?.Down();
        }

        private void ClearToStr(object sender, MouseButtonEventArgs e)
        {
            ToStr.Text = string.Empty;
        }

        private void CloneFromStr(object sender, MouseButtonEventArgs e)
        {
            if (ToStr.Text.Length == 0)
            {
                ToStr.Text = FromStr.Text;
            }
        }

        private void NextAuto_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            NextAuto();
        }

        public void RestorePreviousHistory()
        {
            var SelectIDs = GetSelectRecordHistoryKey();

            if (SelectIDs == null) return;

            if (SelectIDs.Count > 0)
            {
                List<int> PreviousIDs = HistoryDBCache.GetPreviousIDs(this.Mod.P_Translator.GetFileUniqueKey(), SelectIDs[0]);

                if (PreviousIDs.Count == 0) return;

                //Points to the current node.
                HistoryDBCache.SelectID(this.Mod.P_Translator.GetFileUniqueKey(), PreviousIDs[0]);

                RestoreRecordHistory(PreviousIDs, false);
                //must redirect to the affected line; otherwise, the user won't know where they've been rolled back to.
                var Current = HistoryDBCache.IDToHistoryItem(this.Mod.P_Translator.GetFileUniqueKey(), PreviousIDs[0]);
                if (Current != null)
                {
                    if (!this.TransListView.IsKeyInViewport(Current.Key))
                    {
                        this.TransListView.Goto(Current.Key);
                    }
                }
            }
        }

        public void RestoreNextHistory()
        {
            var SelectIDs = GetSelectRecordHistoryKey();

            if (SelectIDs == null) return;

            if (SelectIDs.Count > 0)
            {
                List<int> NextIDs = HistoryDBCache.GetNextIDs(this.Mod.P_Translator.GetFileUniqueKey(), SelectIDs[0]);

                if (NextIDs.Count == 0) return;

                HistoryDBCache.SelectID(this.Mod.P_Translator.GetFileUniqueKey(), NextIDs[0]);

                RestoreRecordHistory(NextIDs, true);

                var Current = HistoryDBCache.IDToHistoryItem(this.Mod.P_Translator.GetFileUniqueKey(), NextIDs[0]);
                if (Current != null)
                {
                    if (!this.TransListView.IsKeyInViewport(Current.Key))
                    {
                        this.TransListView.Goto(Current.Key);
                    }
                }
            }
        }

        WordCompletionManager CompletionManager = null;
        public void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                if (CompletionManager != null && CompletionManager.IsCompletionActive)
                {
                    return;
                }

                if (TransView.IsHitTestVisible == true)
                {
                    e.Handled = true;

                    if (NextAutoEnable == 0)
                    {
                        TransListView?.Down();
                    }
                    else
                    {
                        NextAuto();
                    }
                }

                return;
            }
            if (e.Key == Key.F2)
            {
                e.Handled = true;

                if (NIMApp.SelfSetting.ViewMode == "Normal")
                {
                    ApplyTranslatedText();
                    ReSetHistoryPointer();
                    CurrentHistory?.RefreshData();
                }

                return;
            }
            if (e.Key == Key.F1)
            {
                e.Handled = true;

                if (NIMApp.SelfSetting.ViewMode == "Normal")
                {
                    if (!Phoenix.CheckAvailableNodes())
                    {
                        MessageBoxExtend.Show(this._Parent,"Msg", "Please enable at least one translation platform node.", PreviewDialogSeverity.Warning);
                        return;
                    }

                    TranslateCurrent();
                    ReSetHistoryPointer();
                    CurrentHistory?.RefreshData();
                }

                return;
            }

            if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                RestorePreviousHistory();
                return;
            }

            if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                RestoreNextHistory();
                return;
            }
        }

        public List<int> GetSelectRecordHistoryKey()
        {
            int FileUniqueKey = this.Mod.P_Translator.GetFileUniqueKey();

            var IDs = HistoryDBCache.GetSelectIDs(FileUniqueKey);

            if (IDs.Count == 0)
            {
                var ID = HistoryDBCache.GetLastID(FileUniqueKey);
                if (ID <= 0)
                {
                    return null;
                }
                else
                {
                    IDs.Add(ID);
                }
            }

            return IDs;
        }

        public void RestoreRecordHistory(List<int> GetIDs,bool IsNext)
        {
            if (GetIDs.Count > 0)
            {
                List<string> UPDateKeys = new List<string>();
                for (int i = 0; i < GetIDs.Count; i++)
                {
                    var GetHistoryItem = HistoryDBCache.IDToHistoryItem(this.Mod.P_Translator.GetFileUniqueKey(), GetIDs[i]);

                    var Row = this.TransListView.KeyToFakeGrid(GetHistoryItem.Key);
                    bool IsCloud = false;
                    Row.SyncData(this.Mod,ref IsCloud);

                    if (IsCloud)
                    {
                        CloudDBCache.DeleteCache(GetHistoryItem.FileUniqueKey, GetHistoryItem.Key, this.Mod.P_Translator.To);
                    }
                    else
                    {
                        LocalDBCache.DeleteCache(GetHistoryItem.FileUniqueKey, GetHistoryItem.Key, this.Mod.P_Translator.To);
                    }

                    string NewText = "";
                    if (!IsNext)
                    {
                        NewText = GetHistoryItem.CurrentText;
                        this.Mod.P_Translator.AutoSetLink(GetHistoryItem.Key, Row.Source, new P_String(GetHistoryItem.CurrentText, 0, GetHistoryItem.RangeID));
                    }
                    else
                    {
                        NewText = GetHistoryItem.CurrentText;
                        this.Mod.P_Translator.AutoSetLink(GetHistoryItem.Key, Row.Source, new P_String(GetHistoryItem.CurrentText, 0, GetHistoryItem.RangeID));
                    }

                    Row.Translated = NewText;

                    UPDateKeys.Add(GetHistoryItem.Key);
                }

                for (int i = 0; i < this.TransListView.Rows; i++)
                {
                    if (UPDateKeys.Contains(this.TransListView.RealLines[i].Key))
                    {
                        bool IsCloud = false;
                        this.TransListView.RealLines[i].SyncData(this.Mod, ref IsCloud);
                        this.TransListView.RealLines[i].SyncUI(this.TransListView);
                    }
                }

                CurrentHistory?.RefreshData();
            }
        }

        public void CanEditTransView(bool Check)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                TransView.IsHitTestVisible = Check;
            }));
        }

        public void ShowClearToStrButton(bool Enable)
        {
            UIHelper.ShowButton(ClearToStrButton, Enable);
        }

        private void CancelTranslatedText(object sender, MouseButtonEventArgs e)
        {
            EmptyFromAndToText();

            UIHelper.ShowButton(CancelOTButton, false);
            UIHelper.ShowButton(ApplyOTButton, false);

            ShowClearToStrButton(false);
        }

        public void ApplyTranslatedText()
        {
            if (TransListView != null)
            {
                if (LastSetKey.Trim().Length > 0)
                {
                    var GetGrid = TransListView.KeyToFakeGrid(LastSetKey);

                    if (GetGrid != null)
                    {
                        bool RefCloud = false;
                        GetGrid.SyncData(Mod, ref RefCloud);

                        GetGrid.Translated = ToStr.Text;

                        try
                        {
                            if (CloudDBCache.FindCache(Mod.P_Translator.GetFileUniqueKey(), GetGrid.Key, Mod.P_Translator.To).Equals(GetGrid.Translated))
                            {
                                LocalDBCache.DeleteCache(Mod.P_Translator.GetFileUniqueKey(), GetGrid.Key, Mod.P_Translator.To);

                                var Link = Mod.P_Translator.GetLink();

                                Link[GetGrid.Key] =new P_String(GetGrid.Translated,1);


                            }
                            else
                            {
                                Mod.P_Translator.AutoSetLink(GetGrid.Key, GetGrid.Source,new P_String(GetGrid.Translated,1));
                            }
                        }
                        catch { }

                        GetGrid.SyncData(Mod, ref RefCloud);
                        GetGrid.SyncUI(TransListView);
                        //DeFine.ExtendWin.SetOriginal(GetGrid.SourceText, DeFine.WorkingWin.GlobalEspReader.StringsReader.QueryData(GetGrid.Key));
                    }

                    UIHelper.ShowButton(ApplyOTButton, false);
                }
            }
        }

        private void ApplyTranslatedText(object sender, MouseButtonEventArgs e)
        {
            ApplyTranslatedText();
            ReSetHistoryPointer();
            CurrentHistory?.RefreshData();
        }

        private void TranslateOTButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!Phoenix.CheckAvailableNodes())
            {
                MessageBoxExtend.Show(this._Parent,"Msg","Please enable at least one translation platform node.", ApplicationLayer.PreviewDialogSeverity.Information);
                return;
            }

            TranslateCurrent();
            ReSetHistoryPointer();
            CurrentHistory?.RefreshData();
        }


        public object TranslateLocker = new object();
        public Thread TranslateTrd = null;
        private CancellationTokenSource TranslateCTS = null;

        public bool SingleTrans = false;
        public void TranslateCurrent()
        {
            Mod.MakeReady();

            lock (TranslateLocker)
            {
                if (TransListView != null)
                {
                    if (P_Convert.ObjToStr(TranslateOTButtonFont.Content).Equals("Translate"))
                    {
                        FakeGrid QueryGrid = TransListView.KeyToFakeGrid(LastSetKey);

                        if (QueryGrid != null && TranslateTrd == null)
                        {
                            bool IsCloud = false;
                            QueryGrid.SyncData(Mod, ref IsCloud);

                            if (QueryGrid.Translated.Length > 0)
                            {
                                CloudDBCache.DeleteCache(Mod.P_Translator.GetFileUniqueKey(), QueryGrid.Key, Mod.P_Translator.To);
                            }

                            string Emotion = "";

                            if (this.Mod.Type == GameFileType.ESP)
                            {
                                Emotion = this.Mod.EspReader.QueryEmotion(this.Mod.EspReader.Records[QueryGrid.Key]);
                            }

                            BaseUnit SetUnit = new BaseUnit(Mod.P_Translator.GetFileUniqueKey(), QueryGrid.Key, QueryGrid.Type, QueryGrid.Source, QueryGrid.Translated, Emotion, 100);

                            CanEditTransView(false);

                            TranslateCTS = new CancellationTokenSource();
                            CancellationToken Token = TranslateCTS.Token;

                            TranslateTrd = new Thread(() =>
                            {
                                try
                                {
                                    SingleTrans = true;

                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        TranslateOTButtonFont.Content = "Translating..." + "(Click to cancel)";
                                        ThreadInFo.Visibility = Visibility.Visible;
                                    }));

                                    SetUnit.Translated = string.Empty;
                                    UnitGroup Result =
                                       Mod.P_Translator.Translate(SetUnit, Token, false);

                                    Token.ThrowIfCancellationRequested();

                                    string GetTranslated =
                                        Result.GetFrist().Translated;

                                    CanEditTransView(true);

                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        TranslateOTButtonFont.Content = "Translating...";

                                        if (Mod.TranslationStatus == StateControl.Null ||
                                            Mod.TranslationStatus == StateControl.Cancel)
                                        {
                                            ThreadInFo.Visibility = Visibility.Collapsed;
                                        }

                                        ToStr.Text = GetTranslated;
                                    }));
                                }
                                catch (OperationCanceledException)
                                {
                                }
                                finally
                                {
                                    CanEditTransView(true);

                                    this.Dispatcher.Invoke(() =>
                                    {
                                        TranslateOTButtonFont.Content = "Translate";

                                        ThreadInFo.Visibility = Visibility.Collapsed;
                                    });

                                    SingleTrans = false;

                                    TranslateCTS?.Dispose();
                                    TranslateCTS = null;

                                    TranslateTrd = null;
                                }
                            });

                            TranslateTrd.Start();
                        }
                    }
                    else
                    {
                        TranslateCTS?.Cancel();

                        InteractiveView.CloseAll();

                        TranslateOTButtonFont.Content = "Translate";

                        SingleTrans = false;
                        ThreadInFo.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        public void NextAuto()
        {
            var Lines = TransListView?.RealLines;
            if (Lines == null) return;

            int StartIndex = 0;
            if (LastUntranslatedKey != null)
            {
                int LastIndex = Lines.FindIndex(l => l.Key == LastUntranslatedKey);
                if (LastIndex >= 0)
                    StartIndex = LastIndex + 1;
            }

            int Total = Lines.Count;
            for (int Offset = 0; Offset < Total; Offset++)
            {
                int i = (StartIndex + Offset) % Total;

                bool IsCloud = false;
                var GetLine = Lines[i];
                Lines[i].SyncData(Mod, ref IsCloud);

                if ((GetLine.Source + GetLine.RealSource).Trim().Length > 0)
                {
                    if (P_Language.DetectLanguageByLine(GetLine.Source) != Mod.P_Translator.To)
                    {
                        if (GetLine.Translated.Length == 0 && (new TranslationPreprocessor().IsOnlySymbolsAndSpaces(GetLine.Source + GetLine.RealSource)) == false)
                        {
                            if (Lines[i].Score > 0)
                            {
                                LastUntranslatedKey = Lines[i].Key;
                                TransListView.Goto(Lines[i].Key);
                                return;
                            }
                        }
                    }
                }
            }

            LastUntranslatedKey = null;
        }


        private bool _IsUpdating = false;
        private void TransTargetType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_IsUpdating) return;

            string GetSelectValue = P_Convert.ObjToStr((sender as ComboBox).SelectedValue);

            if (GetSelectValue.Trim().Length > 0)
            {
                SelectSig(GetSelectValue);
            }
        }

        public void SelectSig(string Sig, Action Callback = null)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                _IsUpdating = true;

                TypeSelector.SelectedValue = Sig;

                this.CurrentSig = Sig;

                if (Callback != null)
                {
                    OnDataReloadCompleted = new Action(() =>
                    {
                        this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, Callback);
                    });
                }
                else
                {
                    OnDataReloadCompleted = null;
                }

                ReloadData();

                _IsUpdating = false;
            }));
        }

        public void EnableNormalModel()
        {
            NIMApp.SelfSetting.ViewMode = "Normal";
            NormalModel.Style = (Style)this.FindResource("ModelSelected");
            QuickModel.Style = (Style)this.FindResource("ModelUnSelected");

            NormalModelBlock.Visibility = Visibility.Visible;
            QuickModelBlock.Visibility = Visibility.Collapsed;

            ReloadData(true, true);

            double AutoHeight = NIMApp.SelfSetting.WritingAreaHeight;

            if (AutoHeight < 100)
            {
                AutoHeight = 300;
            }
            WritingArea.Height = new GridLength(AutoHeight, GridUnitType.Pixel);
            SplictLine.Height = new GridLength(3.5, GridUnitType.Pixel);
        }

        public void EnableQuickModel()
        {
            NIMApp.SelfSetting.ViewMode = "Quick";
            NormalModel.Style = (Style)this.FindResource("ModelUnSelected");
            QuickModel.Style = (Style)this.FindResource("ModelSelected");

            NormalModelBlock.Visibility = Visibility.Collapsed;
            QuickModelBlock.Visibility = Visibility.Visible;

            ReloadData(true, true);

            WritingArea.Height = new GridLength(0, GridUnitType.Pixel);
            SplictLine.Height = new GridLength(0, GridUnitType.Pixel);
        }

        private void NormalModel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            EnableNormalModel();
        }
        private void QuickModel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            EnableQuickModel();
        }

        private void ToStr_TextChanged(object sender, EventArgs e)
        {
            if (ToStr.Text.Length > 0)
            {
                ShowClearToStrButton(true);
                UIHelper.ShowButton(ApplyOTButton, true);
            }
            else
            {
                ShowClearToStrButton(false);
            }

            UIHelper.ShowButton(ApplyOTButton, true);
        }

        private void ShowDataBaseR_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            bool? GetCloudTranslationCache = CloudTranslationCache.IsChecked;
            bool? GetUserTranslationCache = UserTranslationCache.IsChecked;

            int Key = Mod.P_Translator.GetFileUniqueKey();
            if (GetCloudTranslationCache == true)
            {
                var CloudTrans = new DataBaseView();
                CloudTrans.Show();
                CloudTrans.QueryFirst($"Select * From CloudTranslation Where [FileUniqueKey] = {Key} And [To] = {(int)Mod.P_Translator.To} Limit 100000");
            }

            if (GetUserTranslationCache == true)
            {
                var UserTranslation = new DataBaseView();
                UserTranslation.Show();
                UserTranslation.QueryFirst($"Select * From LocalTranslation Where [FileUniqueKey] = {Key} And [To] = {(int)Mod.P_Translator.To} Limit 100000");
            }
        }

        public void UPDateUI()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                EmptyFromAndToText();
                Mod.P_Translator.GetLink().Clear();

                if (TransListView != null)
                {
                    if (TransListView.Rows > 0)
                    {
                        TransListView.QuickRefresh(Mod);
                    }
                }
            }));
        }

        public Thread ClearCacheTrd = null;
        private void ClearCacheR_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            CheckCanClearCache(out bool Check);
            if (Check)
            {
                if (MessageBoxExtend.Show(this._Parent, "Waring", "Are you sure you want to clear the database records? Doing so will lose all translated content. (Note: Under no circumstances should you click this button arbitrarily.)",PreviewDialogSeverity.Warning) == false)
                {
                    return;
                }

                if (TransListView != null)
                {
                    if (TransListView.Rows > 0)
                    {
                        if (P_Convert.ObjToStr(ClearCacheRButton.Content).Equals("Clear Cache"))
                        {
                            if (ClearCacheTrd == null)
                            {
                                bool? GetCloudTranslationCache = CloudTranslationCache.IsChecked;
                                bool? GetUserTranslationCache = UserTranslationCache.IsChecked;

                                ClearCacheTrd = new Thread(() =>
                                {
                                    try
                                    {
                                        ClearCacheRButton.Dispatcher.Invoke(new Action(() =>
                                        {
                                            ClearCacheRButton.Content = "Clearing Cache...";
                                        }));

                                        int CallFuncCount = 0;
                                        if (GetCloudTranslationCache == true)
                                        {
                                            Mod.P_Translator.ClearAICache();

                                            if (CloudDBCache.ClearCloudCache(Mod.P_Translator.GetFileUniqueKey()))
                                            {
                                                var GetBatchCore = Mod.P_Translator.GetBatchCore();
                                                if (GetBatchCore != null)
                                                {
                                                    GetBatchCore.TranslatedCount = 0;
                                                }
                                                Phoenix.Vacuum();
                                                CallFuncCount++;
                                            }
                                        }
                                        if (GetUserTranslationCache == true)
                                        {
                                            LocalDBCache.ClearLocalCache(Mod.P_Translator.GetFileUniqueKey());
                                            {
                                                Phoenix.Vacuum();
                                                CallFuncCount++;
                                            }

                                            ToStr.Dispatcher.Invoke(new Action(() =>
                                            {
                                                ToStr.Text = "";
                                            }));
                                        }

                                        UPDateUI();
                                    }
                                    catch
                                    {

                                    }

                                    Mod.CancelTranslateWork();

                                    while (Mod.PreparingTrd != null)
                                    {
                                        Thread.Sleep(100);
                                    }

                                    ClearCacheRButton.Dispatcher.Invoke(new Action(() =>
                                    {
                                        ClearCacheRButton.Content = "Clear Cache";
                                    }));

                                    ClearCacheTrd = null;
                                });

                                ClearCacheTrd.Start();
                            }
                        }
                    }
                }
            }
        }

        private void UserTranslationCache_Click(object sender, RoutedEventArgs e)
        {
            if (UserTranslationCache.IsChecked == true)
            {
                NIMApp.SelfSetting.CanClearUserInputTranslationCache = true;
            }
            else
            {
                NIMApp.SelfSetting.CanClearUserInputTranslationCache = false;
            }

            CheckCanClearCache(out bool Check);
        }

        private void CloudTranslationCache_Click(object sender, RoutedEventArgs e)
        {
            if (CloudTranslationCache.IsChecked == true)
            {
                NIMApp.SelfSetting.CanClearCloudTranslationCache = true;
            }
            else
            {
                NIMApp.SelfSetting.CanClearCloudTranslationCache = false;
            }

            CheckCanClearCache(out bool Check);
        }

        private void ClearCacheViewClose_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ClearCacheView.Visibility = Visibility.Collapsed;
            Mask.Visibility = Visibility.Collapsed;
            CacheViewIsShow = false;
        }

        public int GlobalTransCount = 0;
        public void GetGlobalTransCount()
        {
            if (Mod.Type == GameFileType.ESP)
            {
                if (P_Convert.ObjToStr(TypeSelector.SelectedValue).Equals("ALL"))
                {
                    if (TransListView != null)
                        GlobalTransCount = TransListView.RealLines.Count;
                }
            }
            else
            {
                if (TransListView != null)
                    GlobalTransCount = TransListView.RealLines.Count;
            }
        }

        public void SyncTranslationStatus()
        {
            this.Dispatcher.Invoke(delegate ()
            {
                this.CalcStatistics();
            });
        }
        public bool BarInit = false;
        public void CalcStatistics()
        {
            try
            {
                int ModifyCount = 0;

                bool FromCore = false;
                var GetBatchCore = Mod?.P_Translator?.GetBatchCore();
                if (GetBatchCore != null)
                {
                    if (GetBatchCore.Container != null && GetBatchCore.IsWorking && !SingleTrans)
                    {
                        ModifyCount = (GetBatchCore.BaseTranslatedCount + GetBatchCore.TranslatedCount);

                        if (ModifyCount > this.TransListView.RealLines.Count)
                        {
                            ModifyCount = this.TransListView.RealLines.Count;
                        }

                        FromCore = true;
                    }

                }

                if (!FromCore)
                {
                    ModifyCount = Mod.P_Translator.CalcTranslatedCount(0);
                }

                this.Dispatcher.Invoke(new Action(() =>
                {
                    if (Mod != null)
                        if (Mod.P_Translator.GetBatchCore() != null)
                        {
                            var BatchCore = Mod.P_Translator.GetBatchCore();
                            if (ScanAnimator != null)
                            {
                                if (ModifyCount > 0 && BatchCore.IsWorking && !BatchCore.IsStopped)
                                {
                                    if (!BarInit)
                                    {
                                        BarEffect.Dispatcher.Invoke(new Action(() =>
                                        {
                                            BarEffect.Width = 30;
                                        }));

                                        BarInit = true;
                                    }
                                    ScanAnimator.Start();
                                }
                                else
                                {
                                    ScanAnimator.Stop();
                                }
                            }

                            if ((BatchCore.IsWorking && !BatchCore.IsStopped) || SingleTrans)
                            {
                                int Current = BatchCore.GetWorkingThreadCount();

                                if (SingleTrans)
                                {
                                    ThreadInFo.Content = string.Format("Thread(Current:{0},Max:{1})", Current + 1, NIMApp.EngineSetting.MaxThreadCount + 1);
                                }
                                else
                                {
                                    ThreadInFo.Content = string.Format("Thread(Current:{0},Max:{1})", Current, NIMApp.EngineSetting.MaxThreadCount);
                                }
                            }
                            else
                            if (BatchCore.IsWorking && BatchCore.IsStopped)
                            {
                                ThreadInFo.Content = string.Format("Thread(Current:0,Max:{0})", NIMApp.EngineSetting.MaxThreadCount);
                            }
                        }
                        else
                        {
                            if (SingleTrans)
                            {
                                ThreadInFo.Content = string.Format("Thread(Current:{0},Max:{1})", 1, NIMApp.EngineSetting.MaxThreadCount + 1);
                            }
                        }

                    if (TransListView != null)
                    {
                        GetGlobalTransCount();

                        if (ReadTrdWorkState)
                        {
                            if (Mod.TranslationStatus == StateControl.Cancel || Mod.TranslationStatus == StateControl.Null)
                            {
                                TransProcess.Content = string.Format("Loading({0}/{1})", ModifyCount, GlobalTransCount);
                            }

                            TypeSelector.Opacity = 0.5;
                            TypeSelector.IsEnabled = false;

                            ViewModel.Opacity = 0.5;
                            ViewModel.IsHitTestVisible = false;
                        }
                        else
                        {
                            if (Mod.TranslationStatus == StateControl.Cancel || Mod.TranslationStatus == StateControl.Null)
                            {
                                TransProcess.Content = string.Format("STRINGS({0}/{1})", ModifyCount, GlobalTransCount);
                            }

                            TypeSelector.Opacity = 1;
                            TypeSelector.IsEnabled = true;

                            ViewModel.Opacity = 1;
                            ViewModel.IsHitTestVisible = true;
                        }

                        double GetRate = ((double)ModifyCount / (double)GlobalTransCount);

                        if (GetRate > 0)
                        {
                            try
                            {
                                if (!Double.IsInfinity(GetRate))
                                    ProcessBar.Width = ProcessBarControl.ActualWidth * GetRate;
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                ProcessBar.Width = 0;
                            }
                            catch { }
                        }
                    }
                }));
            }
            catch { }
        }

        public void CancelBatchTranslation()
        {
            try
            {
                Mod.TranslationStatus = StateControl.Cancel;
                Mod.SyncTransState(
                new Action(() =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        this.SyncTransStateUI();
                    }));
                })
                , false);
            }
            catch
            {
            }
        }

        public volatile bool CanExitSyncTrd = false;
        public Thread SyncTrd = null;
        public void DisposeAny()
        {
            CanExitSyncTrd = true;
            SyncTrd?.Abort();
            SyncTrd = null;

            GlobalTransCount = 0;

            CancelBatchTranslation();

            EmptyFromAndToText();

            Mod.P_Translator.Close();

            CurrentSearchData = new SearchData();

            this.Dispatcher.Invoke(new Action(() =>
            {
                try
                {
                    SearchBox.Text = string.Empty;

                    TransListView?.Clear();

                    Mod.Close();

                    TypeSelector.Items.Clear();
                    Mod.OriginalDictionaryReader.Close();

                    FromStringsFile.Visibility = Visibility.Collapsed;

                    ProcessBar.Width = 0;
                }
                catch { }
            }));

            new Thread(() =>
            {
                Thread.Sleep(500);

                this.Dispatcher.Invoke(new Action(() =>
                {
                    TransListView.Clear();
                }));

                GlobalTransCount = 0;
            }).Start();

            Mod.CancelTranslateWork();

            Mod.P_Translator.GetLink().Clear();

            if (CurrentHistory != null)
            {
                CurrentHistory.Close();
                CurrentHistory = null;
            }
        }

        private void Mask_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (CacheViewIsShow)
            {
                ClearCacheView.Visibility = Visibility.Collapsed;
                Mask.Visibility = Visibility.Collapsed;
                CacheViewIsShow = false;
            }
        }

        public bool Saving = false;
        private static object GlobalSaveLock = new object();
        private void SaveFile_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Saving) return;

            SaveFileBtn.Opacity = 0.2;
            SaveFileBtn.IsHitTestVisible = false;

            new Thread(() =>
            {
                Saving = true;
                lock (GlobalSaveLock)
                {
                    try
                    {
                        this.Mod.Save();
                    }
                    catch (Exception Ex)
                    {
                        MessageBoxExtend.Show(this._Parent, "Error Saving File", Ex.Message,PreviewDialogSeverity.Error);
                    }
                }

                SaveFileBtn.Dispatcher.Invoke(new Action(() =>
                {
                    SaveFileBtn.IsHitTestVisible = true;
                    SaveFileBtn.Opacity = 1;
                }));

                Saving = false;
            }).Start();
        }

        public static HistoryWindow CurrentHistory = null;
        private void ShowHistory(object sender, MouseButtonEventArgs e)
        {
            ShowHistory();
        }

        public void ShowHistory()
        {
            if (TranslateView.CurrentHistory == null)
            {
                TranslateView.CurrentHistory = new HistoryWindow(this.Mod.P_Translator.GetFileUniqueKey());

                TranslateView.CurrentHistory.Show();
                TranslateView.CurrentHistory.UpdateFollowPosition();
            }
            else
            {
                TranslateView.CurrentHistory.FileUniqueKey = this.Mod.P_Translator.GetFileUniqueKey();

                TranslateView.CurrentHistory.UpdateFollowPosition();
                TranslateView.CurrentHistory.RefreshData();
            }
        }
    }
}
