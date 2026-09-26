using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NIM.SkyrimManagement;
using NIM.SkyrimModManager;
using NIM.TranslateManage;
using NIM.UIManage;
using PhoenixEngine.ADO;
using PhoenixEngine.Unit;
using PhoenixEngine.Language;
using PhoenixEngine;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Common;
using NIM.ApplicationLayer;
using NIM.ModParser;

namespace NIM
{
    public class DictExportItem
    {
        public int From { get; set; }
        public int To { get; set; }
        public int ExactMatch { get; set; }
        public int IgnoreCase { get; set; }
        public string ModName { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Result { get; set; }
    }

    /// <summary>
    /// Interaction logic for LocalConfig.xaml
    /// </summary>
    public partial class TranslateConfig : Window
    {
        private PhoenixGui _Owner;
        public TranslateConfig(PhoenixGui Owner)
        {
            InitializeComponent();
            this._Owner = Owner;
        }


        #region Default Form Behavior

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

        private void RMouserEffectByEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border)
            {
                Border LockerGrid = sender as Border;
                if (LockerGrid.Child != null)
                {
                    if (LockerGrid.Child is Image)
                    {
                        Image LockerImg = LockerGrid.Child as Image;
                        LockerImg.Opacity = 0.9;
                    }
                    LockerGrid.Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
                }
            }
        }

        private void RMouserEffectByLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border)
            {
                Border LockerGrid = sender as Border;
                if (LockerGrid.Child != null)
                {
                    if (LockerGrid.Child is Image)
                    {
                        Image LockerImg = LockerGrid.Child as Image;
                        LockerImg.Opacity = 0.6;
                    }
                    LockerGrid.Background = new SolidColorBrush(Color.FromRgb(59, 59, 59));
                }
            }
        }

        public int SizeChangeState = 0;
        public void AnyHeaderButtonClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border || sender is Image)
            {
                string Tag = "";

                if (sender is Border)
                {
                    Tag = P_Convert.ObjToStr((sender as Border).Tag);
                }
                if (sender is Image)
                {
                    Tag = P_Convert.ObjToStr((sender as Image).Tag);
                }

                switch (Tag)
                {
                    case "Min":
                        {
                            this.WindowState = WindowState.Minimized;
                        }
                        break;
                    case "MaxWin":
                        {
                            if (SizeChangeState == 0)
                            {
                                this.WindowState = WindowState.Maximized;
                                SizeChangeState = 1;
                            }
                            else
                            {
                                this.WindowState = WindowState.Normal;
                                SizeChangeState = 0;
                            }

                        }
                        break;
                    case "Close":
                        {
                            this.Hide();
                        }
                        break;
                }
            }
        }
        #endregion
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TranslatorInterface.RegListener("KeyWordsListen", new List<int>() { 2 }, new Action<int, object>((Sign, Any) =>
            {
                if (Any is PreTranslateCall)
                {
                    PreTranslateCall GetPreCall = (PreTranslateCall)Any;
                    if (GetPreCall.Key.StartsWith("YD525Test_"))
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            MatchedKeywords.Items.Clear();
                        }));

                        foreach (var GetKey in GetPreCall.ReplaceTags)
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                MatchedKeywords.Items.Add(GetKey.Rowid + "," + GetKey.Key + "->" + GetKey.Value);
                            }));
                        }

                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            Output.Text = GetPreCall.ReceiveString;
                        }));
                    }
                }
            }));

            ExactMatch.IsChecked = true;
        }

        public void ChangeTab()
        {
            FileName.Content = _Owner.ActiveTab?.Mod?.FileName;
        }

        public void SetTypes()
        {
            TypeSelector.Items.Clear();
            TypeSelector.Items.Add("ALL");

            EspReader EspInstance = null;
            if (_Owner != null)
            {
                if (_Owner.ActiveTab?.Mod.Type == GameFileType.ESP)
                {
                    EspInstance = _Owner.ActiveTab?.Mod.EspReader;
                }
            }

            if(EspInstance != null)
            foreach (var Value in EspInstance.Types)
            {
                TypeSelector.Items.Add(Value);
            }

            TypeSelector.SelectedValue = TypeSelector.Items[0];
        }

        public void Init()
        {
            TypeSelector.Items.Clear();
            From.Items.Clear();
            SFrom.Items.Clear();
            To.Items.Clear();
            STo.Items.Clear();

            SetTypes();

            foreach (var Get in UILanguageHelper.GetSupportedLanguages())
            {
                if (Get != Languages.Null)
                {
                    From.Items.Add(Get.ToString());
                    SFrom.Items.Add(Get.ToString());
                    To.Items.Add(Get.ToString());
                    STo.Items.Add(Get.ToString());
                }
            }

            From.Items.Remove(Languages.Auto.ToString());
            SFrom.Items.Remove(Languages.Auto.ToString());
            To.Items.Remove(Languages.Auto.ToString());
            STo.Items.Remove(Languages.Auto.ToString());

            AutoDetect.IsChecked = true;

            if (_Owner != null)
            {
                if (_Owner.ActiveTab != null)
                {
                    SFrom.SelectedValue = _Owner.ActiveTab.Mod.P_Translator.From.ToString();
                    STo.SelectedValue = _Owner.ActiveTab.Mod.P_Translator.To.ToString();
                }
            }
           
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void FromStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            SFrom.SelectedValue = P_Language.DetectLanguageByLine(FromStr.Text).ToString();
        }

        private void SourceStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AutoDetect.IsChecked == true)
            {
                var DetectLang = P_Language.DetectLanguageByLine(SourceStr.Text);
                if (DetectLang == Languages.SimplifiedChinese || DetectLang == Languages.TraditionalChinese)
                {
                    if (NIMApp.WorkWin.ActiveTab.Mod.P_Translator.From == Languages.SimplifiedChinese || NIMApp.WorkWin.ActiveTab.Mod.P_Translator.From == Languages.TraditionalChinese)
                    {
                        DetectLang = NIMApp.WorkWin.ActiveTab.Mod.P_Translator.From;
                    }
                }

                From.SelectedValue = DetectLang.ToString();
            }
        }

        private void TargetStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AutoDetect.IsChecked == true)
            {
                var DetectLang = P_Language.DetectLanguageByLine(TargetStr.Text);
                if (DetectLang == Languages.SimplifiedChinese || DetectLang == Languages.TraditionalChinese)
                {
                    if (NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To == Languages.SimplifiedChinese || NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To == Languages.TraditionalChinese)
                    {
                        DetectLang = NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To;
                    }
                }
                To.SelectedValue = DetectLang.ToString();
            }
        }

        public void AutoReload()
        {
            if (FilterFrom != Languages.Null && FilterTo != Languages.Null)
            {
                if (FilterFrom != FilterTo)
                {
                    Pages = AdvancedDictionary.QueryByPage((int)FilterFrom, (int)FilterTo, CurrentPage);
                    KeywordList.Items.Clear();
                    foreach (var GetItem in Pages.CurrentPage)
                    {
                        KeywordList.Items.Add(new
                        {
                            TargetFileName = GetItem.TargetFileName,
                            Type = GetItem.Type,
                            Source = GetItem.Source,
                            Result = GetItem.Result,
                            ExactMatch = GetItem.ExactMatch,
                            IgnoreCase = GetItem.IgnoreCase,
                            Rowid = GetItem.Rowid
                        });
                    }

                    MaxPage = Pages.MaxPage;
                    CurrentPage = Pages.PageNo;

                    if (MaxPage > 0)
                    {
                        PageInFo.Content = string.Format("{0}/{1}", CurrentPage, MaxPage);
                    }
                    else
                    {
                        PageInFo.Content = "0/0";
                    }

                    if (KeywordList.Items.Count > 0)
                    {
                        var FristItem = KeywordList.Items[0];
                        KeywordList.ScrollIntoView(FristItem);
                    }
                }
                else
                {
                    KeywordList.Items.Clear();
                }
            }
            else
            {
                KeywordList.Items.Clear();
            }
        }

        public int CanReload = 1;

        public P_SQL_Page<List<AdvancedDictionaryItem>> Pages = null;

        public int CurrentPage = 1;
        public int MaxPage = 0;
        public Languages FilterFrom = Languages.Null;
        public Languages FilterTo = Languages.Null;

        private void SFrom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetLang = P_Convert.ObjToStr(SFrom.SelectedValue);
            if (GetLang.Trim().Length > 0)
            {
                FilterFrom = (Languages)Enum.Parse(typeof(Languages), GetLang.Trim());
            }

            if (NIMApp.WorkWin.ActiveTab != null)
            {
                NIMApp.WorkWin.ActiveTab.Mod.P_Translator.From = FilterFrom;
            }

            if (_Owner != null)
            {
                if (_Owner.ActiveTab != null)
                {
                    NIMApp.SelfSetting.SourceLanguage = FilterFrom;
                    NIMApp.SelfSetting.SaveConfig();
                }
            }    

            _Owner.ActiveTab?.ReloadStringsFile();
            _Owner.ActiveTab?.UPDateUI();

            if (CanReload > 0)
                AutoReload();

            _Owner.ActiveTab?.AutoShowTraditional();
        }

        private void STo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetLang = P_Convert.ObjToStr(STo.SelectedValue);
            if (GetLang.Trim().Length > 0)
            {
                FilterTo = (Languages)Enum.Parse(typeof(Languages), GetLang.Trim());
            }

            if (NIMApp.WorkWin.ActiveTab != null)
            {
                NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To = FilterTo;
            }

            if (_Owner != null)
            {
                if (_Owner.ActiveTab != null)
                {
                    NIMApp.SelfSetting.TargetLanguage = FilterTo;
                    NIMApp.SelfSetting.SaveConfig();
                }
            }

            _Owner.ActiveTab?.ReloadStringsFile();
            _Owner.ActiveTab?.UPDateUI();

            if (CanReload > 0)
                AutoReload();

            _Owner.ActiveTab?.AutoShowTraditional();

            _Owner.ActiveTab?.Completer?.CheckLang(FilterTo);
        }

        public void SetOutput(string Str)
        {
            try
            {
                this.Dispatcher.Invoke(new Action(() =>
                {
                    if (this.Visibility == Visibility.Visible)
                    {
                        Output.Text = Str;
                    }
                }));
            }
            catch { }
        }

        private void AddKeyWord(object sender, MouseButtonEventArgs e)
        {
            string FromStr = P_Convert.ObjToStr(From.SelectedValue);
            string ToStr = P_Convert.ObjToStr(To.SelectedValue);

            int FromID = 0;
            if (FromStr.Trim().Length > 0)
            {
                FromID = (int)(Languages)Enum.Parse(typeof(Languages), FromStr.Trim());
            }

            int ToID = 0;
            if (ToStr.Trim().Length > 0)
            {
                ToID = (int)(Languages)Enum.Parse(typeof(Languages), ToStr.Trim());
            }


            if (FromStr.Trim().Length == 0 || ToStr.Trim().Length == 0)
            {
                MessageBox.Show("Source language or target language cannot be empty.");
                return;
            }
            if (FromStr == ToStr)
            {
                MessageBox.Show("The source and target languages ​​cannot be the same.");
                return;
            }

            if (SourceStr.Text.Trim().Length == 0)
            {
                MessageBox.Show("Source text cannot be empty.");
                return;
            }

            if (TargetStr.Text.Trim().Length == 0)
            {
                MessageBox.Show("Result text cannot be empty.");
                return;
            }

            int GetExactMatch = 0;
            if (ExactMatch.IsChecked == true)
            {
                GetExactMatch = 1;
            }
            int GetIgnoreCase = 0;
            if (IgnoreCase.IsChecked == true)
            {
                GetIgnoreCase = 1;
            }

            string GetType = P_Convert.ObjToStr(TypeSelector.SelectedValue);

            if (GetType.Equals("ALL"))
            {
                GetType = string.Empty;
            }

            if (AdvancedDictionary.AddItem(new AdvancedDictionaryItem(TargetModName.Text, GetType, SourceStr.Text, TargetStr.Text, FromID, ToID, GetExactMatch, GetIgnoreCase, string.Empty)))
            {
                SFrom.SelectedValue = FromStr;
                STo.SelectedValue = ToStr;
                CanReload = 0;
                AutoReload();
                CanReload = 1;
            }
        }
        private void Previous_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                AutoReload();
            }
        }

        private void Next_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (CurrentPage < MaxPage)
            {
                CurrentPage++;
                AutoReload();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Pages != null)
            {
                foreach (var GetItem in KeywordList.SelectedItems)
                {
                    int Rowid = P_Convert.ObjToInt(P_Convert.ObjToStr(KeywordList.SelectedItem.GetType().GetProperty("Rowid").GetValue(GetItem, null)));
                    AdvancedDictionary.DeleteByRowid(Rowid);
                }

                AutoReload();
            }

        }

        private static int AutoID = 0;
        private void Execute_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string GetBtnContent = P_Convert.ObjToStr(ExecuteBtn.Content);
            string GetType = P_Convert.ObjToStr(TypeSelector.SelectedValue);
            if (GetBtnContent.Equals("Execute"))
            {
                AutoID++;
                NIMApp.WorkWin.ActiveTab.Mod.MakeReady();

                if (FilterFrom != Languages.Null && FilterTo != Languages.Null)
                {
                    FinalText.Text = string.Empty;
                    MatchedKeywords.Items.Clear();
                    Output.Text = string.Empty;

                    string GetFromStr = FromStr.Text;

                    BaseUnit NewUnit = new BaseUnit(-525, "YD525Test_"+ AutoID, GetType, GetFromStr, "","",100);

                    new Thread(() =>
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            ExecuteBtn.Content = "Executing...";
                        }));
                        bool CanSleep = false;

                        var GetResult = NIMApp.WorkWin.ActiveTab.Mod.P_Translator.Translate(NewUnit,default,false);

                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            FinalText.Text = GetResult.GetFrist().Translated;
                            ExecuteBtn.Content = "Execute";
                        }));

                        CloudDBCache.ClearCloudCache(-525);

                    }).Start();
                }
                else
                {
                    MessageBox.Show("Both source and target languages ​​must be set.");
                }
            }
        }

        private void DeleteSelectItem(object sender, MouseButtonEventArgs e)
        {
            
            List<string> Removes = new List<string>();
            foreach (var Get in MatchedKeywords.SelectedItems)
            {
                string GetStr = P_Convert.ObjToStr(Get);
                if (GetStr.Contains(","))
                {
                    int Rowid = P_Convert.ObjToInt(GetStr.Split(',')[0]);

                    if (GetStr.Contains("__P") || Rowid == 0)
                    {
                        MessageBoxExtend.Show(this,"Msg","Protective placeholders need to be disabled in EngineConfig.",PreviewDialogSeverity.Warning);
                    }
                    else
                    {
                        Removes.Add(GetStr);
                        AdvancedDictionary.DeleteByRowid(Rowid);
                    }
                }
            }

            foreach (var Get in Removes)
            {
                MatchedKeywords.Items.Remove(Get);
            }

            AutoReload();
        }

        private static string SerializeToJson(DictExportItem Item)
        {
            var NStringBuilder = new StringBuilder();
            using (var SW = new StringWriter(NStringBuilder))
            {
                var Serializer = new Newtonsoft.Json.JsonSerializer();
                Serializer.Serialize(SW, Item);
            }
            return NStringBuilder.ToString();
        }

        private static DictExportItem DeserializeFromJson(string Json)
        {
            using (var SR = new StringReader(Json))
            using (var Reader = new Newtonsoft.Json.JsonTextReader(SR))
            {
                var Serializer = new Newtonsoft.Json.JsonSerializer();
                return Serializer.Deserialize<DictExportItem>(Reader);
            }
        }

        public int ImportCount = 0;
        public string LeftOver = "";

        public bool ExitAny = false;


        private void ExportAll(object sender, MouseButtonEventArgs e)
        {
            new Thread(() =>
            {
                ProcessWin.Dispatcher.Invoke(new Action(() =>
                {
                    ProcessWin.Visibility = Visibility.Visible;
                }));

                string SetOutPutPath = NIMApp.GetFullPath(@"\Cache\Output.json");

                if (File.Exists(SetOutPutPath))
                    File.Delete(SetOutPutPath);

                int Count = 0;
                int MaxPage = 1;
                int CurrentPage = 1;   

                using (var Writer = new StreamWriter(SetOutPutPath, true, Encoding.UTF8))
                {
                    while (CurrentPage <= MaxPage)   
                    {
                        if (ExitAny) goto QuickExit;

                        var GetData = AdvancedDictionary.QueryByPage((int)FilterFrom, (int)FilterTo, CurrentPage);
                        Count += GetData.CurrentPage.Count;

                        foreach (var Get in GetData.CurrentPage)
                        {
                            if (ExitAny) goto QuickExit;

                            var ExportItem = new DictExportItem
                            {
                                From = Get.From,
                                To = Get.To,
                                ExactMatch = Get.ExactMatch,
                                IgnoreCase = Get.IgnoreCase,
                                ModName = Get.TargetFileName,
                                Type = Get.Type,
                                Source = Get.Source,
                                Result = Get.Result
                            };

                            Writer.WriteLine(SerializeToJson(ExportItem));
                        }

                        int DisplayPage = CurrentPage;
                        MaxPage = GetData.MaxPage;
                        CurrentPage++;

                        Log.Dispatcher.Invoke(new Action(() =>
                        {
                            Log.Content = string.Format("Exporting dictionary...({0}%)({1} Records)",
                                Math.Round(((double)DisplayPage / (double)MaxPage) * 100, 0),
                                Count);
                        }));
                    }
                }

                string TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                this.Dispatcher.Invoke(new Action(() =>
                {
                    var GetWritePath = DataHelper.ShowSaveFileDialog(
                        "Dict_" + TimeStamp + ".json",
                        "JSON (*.json)|*.json");

                    if (GetWritePath != null)
                    {
                        File.Copy(SetOutPutPath, GetWritePath);
                        File.Delete(SetOutPutPath);
                    }
                }));

            QuickExit:
                Thread.Sleep(100);
                ExitAny = false;
                ProcessWin.Dispatcher.Invoke(new Action(() =>
                {
                    ProcessWin.Visibility = Visibility.Hidden;
                }));

            }).Start();
        }
        public void ProcessRecord(string Combined)
        {
            var Parts = Combined.Split(',');

            for (int i = 0; i < Parts.Length - 1; i++)
            {
                string Record = Parts[i];
                if (!string.IsNullOrWhiteSpace(Record))
                {
                    string[] Params = Record.Split('|');
                    if (Params.Length == 8)
                    {
                        try
                        {
                            if (AdvancedDictionary.AddItem(new AdvancedDictionaryItem(
                                   SQLSafeCodec.Decode(Params[4]),
                                   SQLSafeCodec.Decode(Params[5]),
                                   SQLSafeCodec.Decode(Params[6]),
                                   SQLSafeCodec.Decode(Params[7]),
                                   Params[0],
                                   Params[1],
                                   Params[2],
                                   Params[3],
                                   string.Empty)))
                            {
                                ImportCount++;
                            }
                        }
                        catch (Exception Ex)
                        {

                        }
                    }
                }
            }

            LeftOver = Parts[Parts.Length - 1];
        }
        public void ProcessBuffer(char[] Buffer, int Count)
        {
            string Chunk = new string(Buffer, 0, Count);
            string Combined = LeftOver + Chunk;

            ProcessRecord(Combined);
        }

        private void ImportTable(object sender, MouseButtonEventArgs e)
        {
            ImportCount = 0;
            LeftOver = string.Empty;

            var Dialog = new System.Windows.Forms.OpenFileDialog();
            Dialog.Title = "Please select a file";
            Dialog.Filter = "Dictionary File (*.json;*.txt)|*.json;*.txt";
            Dialog.Multiselect = false;

            if (Dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string SelectedFile = Dialog.FileName;

            if (!File.Exists(SelectedFile))
                return;

            new Thread(() =>
            {
                ProcessWin.Dispatcher.Invoke(new Action(() =>
                {
                    ProcessWin.Visibility = Visibility.Visible;
                }));

                try
                {
                    bool IsJsonFormat = false;
                    using (var PeekReader = new StreamReader(SelectedFile, Encoding.UTF8))
                    {
                        string FirstLine;
                        while ((FirstLine = PeekReader.ReadLine()) != null)
                        {
                            FirstLine = FirstLine.Trim();
                            if (string.IsNullOrEmpty(FirstLine)) continue;

                            IsJsonFormat = FirstLine.StartsWith("{");
                            break;
                        }
                    }

                    if (IsJsonFormat)
                    {
                        using (var Reader = new StreamReader(SelectedFile, Encoding.UTF8))
                        {
                            string Line;
                            while ((Line = Reader.ReadLine()) != null)
                            {
                                if (ExitAny) break;

                                Line = Line.Trim();
                                if (string.IsNullOrEmpty(Line)) continue;

                                try
                                {
                                    DictExportItem Item = DeserializeFromJson(Line);

                                    if (AdvancedDictionary.AddItem(new AdvancedDictionaryItem(
                                        Item.ModName,
                                        Item.Type,
                                        Item.Source,
                                        Item.Result,
                                        Item.From,
                                        Item.To,
                                        Item.ExactMatch,
                                        Item.IgnoreCase,
                                        string.Empty)))
                                    {
                                        ImportCount++;
                                    }
                                }
                                catch 
                                { 
                                }

                                Log.Dispatcher.Invoke(new Action(() =>
                                {
                                    Log.Content = string.Format("Number of imported records: ({0})", ImportCount);
                                }));
                            }
                        }
                    }
                    else
                    {
                        int BufferSize = 1024 * 2;
                        char[] Buffer = new char[BufferSize];

                        using (var Reader = new StreamReader(SelectedFile, Encoding.UTF8))
                        {
                            while (true)
                            {
                                if (ExitAny) break;

                                int ReadCount = Reader.ReadBlock(Buffer, 0, BufferSize);
                                if (ReadCount == 0) break;

                                ProcessBuffer(Buffer, ReadCount);

                                Log.Dispatcher.Invoke(new Action(() =>
                                {
                                    Log.Content = string.Format("Number of imported records: ({0})", ImportCount);
                                }));
                            }

                            if (!string.IsNullOrWhiteSpace(LeftOver))
                                ProcessRecord(LeftOver);
                        }
                    }
                }
                catch (Exception Ex)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        MessageBox.Show("Import failed: " + Ex.Message);
                    }));
                }
                finally
                {
                    ExitAny = false;
                    Thread.Sleep(100);
                    ProcessWin.Dispatcher.Invoke(new Action(() =>
                    {
                        ProcessWin.Visibility = Visibility.Hidden;
                        AutoReload();
                    }));
                }

            }).Start();
        }
        private void CancelProcess(object sender, MouseButtonEventArgs e)
        {
            ExitAny = true;
        }

        private void OpenDataBase(object sender, MouseButtonEventArgs e)
        {
            NIMApp.CloseDataBaseView();

            //Results are capped at 100,000 rows via LIMIT to prevent memory exhaustion, as databases may scale to GB/TB levels. This tool is intended for SQL-proficient users to manually execute conditional queries for specific records or perform bulk modifications across multiple entries using custom SQL logic.
            //Select * From AdvancedDictionary Where Source Like '%[pagebreak]%' or  Source Like '%<font' (I just threw this together to match the content of all the books.) - > Compared to using regular expressions for pattern matching, utilizing the `LIKE` and `GLOB` commands in SQL operates directly at the database engine level, enabling millisecond-level query performance.
            var translator = _Owner?.ActiveTab?.Mod?.P_Translator;
            int? sourceLanguage = translator == null ? (int?)null : (int)translator.From;
            int? targetLanguage = translator == null ? (int?)null : (int)translator.To;

            NIMApp.OpenDataBaseView(
                this,
                AdvancedDictionaryQueryBuilder.Build(sourceLanguage, targetLanguage));
        }

        private void DetectFrom(object sender, MouseButtonEventArgs e)
        {
            if (_Owner != null)
            {
                if (_Owner.ActiveTab != null)
                {
                    LanguageDetector Detector = new LanguageDetector();
                    for (int i = 0; i < 700; i++)
                    {
                        if (_Owner.ActiveTab.TransListView.RealLines.Count > i)
                        {
                            P_Language.DetectLanguage(ref Detector, _Owner.ActiveTab.TransListView.RealLines[i].Source);
                        }
                    }

                    _Owner.ActiveTab.Mod.P_Translator.From = Detector.GetMaxLang();
                    SFrom.SelectedValue = _Owner.ActiveTab.Mod.P_Translator.From.ToString();
                }
            }
          
        }
    }
}
