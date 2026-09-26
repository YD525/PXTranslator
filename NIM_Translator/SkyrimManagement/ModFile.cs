using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using NIM.FileManagement;
using NIM.SkyrimManage;
using NIM.TranslateManage;
using NIM.UIManagement;
using NIMEngine.ADO;
using NIMEngine.Engine;
using NIMEngine.Engine.ADO;
using NIMEngine.Events;
using NIMEngine.Memory;
using NIMEngine.Request;
using NIMEngine.Translate;
using NIMEngine.Unit;
using NIM.ApplicationLayer;
using NIMEngine;
using NIM.ModParser;

namespace NIM.SkyrimManagement
{
    public enum GameFileType
    {
        Null = 0, ESP = 1, PEX = 2, MCM = 3, XML = 5, JSON = 6
    }
    public enum GameFileState
    {
        Null = 0, Load = 1, Save = 2
    }
    public class ModFile
    {
        public string Path = "";
        public string FileName = "";
        public GameFileType Type = GameFileType.Null;

        public RamCacheReader RamCacheReader = null;

        public StringsFileReader FromStringsFile = null;
        public StringsFileReader ToStringsFile = null;
        public EspReader EspReader = null;

        public PexReader PexReader = null;
        public MCMReader MCMReader = null;

        public XmlReader XmlReader = null;

        public Dictionary<string, ManagedDialContext> DialNodeCache = new Dictionary<string, ManagedDialContext>();

        public OriginalDictionaryReader OriginalDictionaryReader = new OriginalDictionaryReader();

        public Translator P_Translator = null;
        public YDListView ListView = null;

        public TranslateView Win = null;

        public GameFileState State = GameFileState.Null;



        public ModFile(string Path)
        {
            this.CanRestored = false;

            //Although each tag has its own independent translator, there's only one Node selection view on the interface. This means multiple instances use a single configuration file. Furthermore, the current thread count must be calculated by adding up the number of running instances, and so on. I suddenly realized, what about the thread limit in the settings interface? It limits the number of threads for a single instance. Therefore, to be on the safe side, this version will only allow one translation to run simultaneously for now.
            this.P_Translator = new Translator(Path, NIMApp.SelfSetting.SourceLanguage, NIMApp.SelfSetting.TargetLanguage, true);

            if (System.IO.File.Exists(Path))
            {
                this.Path = Path;
                this.FileName = Path.Substring(Path.LastIndexOf(@"\") + @"\".Length);

                OriginalDictionaryReader.ReadDictionary(this.FileName);

                if (Path.ToLower().EndsWith(".xml"))
                {
                    this.Type = GameFileType.XML;
                    XmlReader = new XmlReader();
                    XmlReader.Create(P_Translator.GetFileUniqueKey(), P_Translator.GetLink());
                }
                else
                if (Path.ToLower().EndsWith(".json"))
                {
                    this.Type = GameFileType.JSON;
                    RamCacheReader = new RamCacheReader(P_Translator);
                }
                else
                if (Path.ToLower().EndsWith(".pex"))
                {
                    this.Type = GameFileType.PEX;
                    PexReader = new PexReader();
                    PexReader.Create(P_Translator.GetFileUniqueKey(), P_Translator.GetLink());
                }
                else
                if (Path.ToLower().EndsWith(".txt"))
                {
                    this.Type = GameFileType.MCM;
                    MCMReader = new MCMReader();
                    MCMReader.Create(P_Translator.GetFileUniqueKey(), P_Translator.GetLink());
                }
                else
                if (Path.ToLower().EndsWith(".esp") || Path.ToLower().EndsWith(".esm") || Path.ToLower().EndsWith(".esl"))
                {
                    this.Type = GameFileType.ESP;
                    EspReader = new EspReader();
                    EspReader.Create(P_Translator.GetFileUniqueKey(), P_Translator.GetLink());
                }
            }

            this.P_Translator.GetLink().OnValueChanged += new Action<string, P_String, P_String>((Key, Previous, Current) =>
            {
                if (Current.Type == 1)
                {
                    int ID = 0;
                    if (Previous == null)
                    {
                        if (this.ListView != null)
                        {
                            bool IsCloud = false;
                            FakeGrid GetRow = this.ListView.KeyToFakeGrid(Key);
                            if (GetRow != null)
                            {
                                GetRow.SyncData(this, ref IsCloud);

                                ID = HistoryDBCache.AddHistory(
                                new HistoryItem(this.P_Translator.GetFileUniqueKey(), Key, (int)this.P_Translator.To,
                                GetRow.Translated,
                                0,
                                DateTime.Now,
                                ""
                                ));

                                HistoryDBCache.CheckPreviousHistoryItem(ID, this.P_Translator.GetFileUniqueKey(),
                                    (int)this.P_Translator.To, Key, GetRow.Translated, out int TargetID);
                                if (TargetID > 0)
                                {
                                    HistoryDBCache.DeleteHistory(this.P_Translator.GetFileUniqueKey(), TargetID);
                                }
                            }
                        }
                    }
                    else
                    {
                        //We need to prevent system-translated data from polluting the history. However, we need a way to save the previous record translated by the system.
                        ID = HistoryDBCache.AddHistory(
                          new HistoryItem(this.P_Translator.GetFileUniqueKey(), Key, (int)this.P_Translator.To,
                          Previous.String,
                          0,
                          DateTime.Now,
                          ""
                          ));

                        HistoryDBCache.CheckPreviousHistoryItem(ID, this.P_Translator.GetFileUniqueKey(), (int)this.P_Translator.To, Key, Previous.String, out int TargetID);
                        if (TargetID > 0)
                        {
                            HistoryDBCache.DeleteHistory(this.P_Translator.GetFileUniqueKey(), TargetID);
                        }          
                    }

                        ID = HistoryDBCache.AddHistory(
                        new HistoryItem(this.P_Translator.GetFileUniqueKey(), Key, (int)this.P_Translator.To,
                        Current.String,
                        0,
                        DateTime.Now,
                        Current.RangeID
                        ));
                }
            });
        }

        public void SetListView(YDListView ListView)
        {
            this.ListView = ListView;
        }

        public bool CanRestored = false;


        //private void ClearBackup()
        //{
        //    if (File.Exists(this.Path))
        //    {
        //        string BackupPath = this.Path + ".backup";

        //        if (File.Exists(BackupPath))
        //        {
        //            File.Delete(BackupPath);
        //        }
        //    }
        //}

        private string BackupManagePath = "";
        private void Backup()
        {
            List<ZipFileInfo> FileLists = new List<ZipFileInfo>();
            BackupManagePath = BackupManager.AddFile(this.Path, ref FileLists);
        }

        private void RestoreBackup()
        {
            if (File.Exists(BackupManagePath))
            {
                BackupManager.RestoreLatest(BackupManagePath);
            }
        }

        public void LoadStringsFile()
        {
            FromStringsFile = new StringsFileReader();
            ToStringsFile = new StringsFileReader();

            FromStringsFile.LoadStringsFiles(this.Path, P_Translator.From);
            ToStringsFile.LoadStringsFiles(this.Path, P_Translator.To);
        }

        private object _DataRef = null;
        public Dictionary<string,T> GetRecords<T>()
        {
            return (Dictionary<string, T>)_DataRef;
        }

        public void Load()
        {
            if (File.Exists(this.Path))
            {
                Close();

                this.P_Translator.LoadFile(this.Path);

                this.OriginalDictionaryReader.ReadDictionary(this.FileName);

                switch (this.Type)
                {
                    case GameFileType.XML:
                        {
                            _DataRef = XmlReader.Load(this.Path);
                            State = GameFileState.Load;
                        }
                        break;
                    case GameFileType.JSON:
                        {
                            RamCacheReader.Load(this.Path);
                            State = GameFileState.Load;
                        }
                        break;
                    case GameFileType.ESP:
                        {
                            _DataRef = EspReader.Load(this.Path);
                            LoadStringsFile();
                            State = GameFileState.Load;
                        }
                        break;
                    case GameFileType.PEX:
                        {
                            _DataRef = PexReader.Load(NIMApp.SelfSetting.GenCSharp?CodeGenStyle.CSharp:CodeGenStyle.Papyrus,NIMApp.SelfSetting.ShowAssembly,this.Path);
                            State = GameFileState.Load;
                        }
                        break;
                    case GameFileType.MCM:
                        {
                            _DataRef = MCMReader.Load(this.Path);
                            State = GameFileState.Load;
                        }
                        break;
                }
            }
        }

        public void SyncListView(bool CanSetSource)
        {
            if (ListView != null)
            {
                for (int i = 0; i < ListView.Rows; i++)
                {
                    bool IsCloud = false;

                    ListView.RealLines[i].SyncData(this, ref IsCloud);

                    string GetKey = ListView.RealLines[i].Key;

                    string GetTransText = ListView.RealLines[i].Translated;

                    if (CanSetSource)
                    {
                        if (string.IsNullOrEmpty(GetTransText))
                        {
                            GetTransText = ListView.RealLines[i].Source;
                        }
                    }

                    var Link = this.P_Translator.GetLink();
                    Link[GetKey] = new P_String(GetTransText, 0);
                }
            }
        }


        public void Save()
        {
            if (File.Exists(this.Path))
            {
                Backup();

                var Link = this.P_Translator.GetLink();

                if (NIMApp.SelfSetting.UseFullPunctuationJa)
                {
                    Link.CheckLinks(new Action<string, P_String, bool>((string Key, P_String Value, bool Unique) =>
                    {
                        if (Value.String.Length > 0)
                        {
                            Link[Key] = new P_String(TranslationPreprocessor.ToFullWidthSymbols(Value.String, true), 0);
                        }
                    }));
                }
                else
                if (NIMApp.SelfSetting.UseFullPunctuation)
                {
                    Link.CheckLinks(new Action<string, P_String, bool>((string Key, P_String Value, bool Unique) =>
                    {
                        if (Value.String.Length > 0)
                        {
                            Link[Key] = new P_String(TranslationPreprocessor.ToFullWidthSymbols(Value.String, false), 0);
                        }
                    }));
                }

                if (this.Type == GameFileType.JSON)
                {
                    SyncListView(false);
                }
                else
                {
                    SyncListView(true);
                }

                switch (this.Type)
                {
                    case GameFileType.JSON:
                        {
                            if (!RamCacheReader.Save(this, this.Path))
                            {
                                RestoreBackup();
                                MessageBox.Show("Build RamCache Error!");
                            }
                            State = GameFileState.Save;
                        }
                        break;
                    case GameFileType.XML:
                        {
                            int ModifyCount = 0;
                            if (!XmlReader.Save(ref ModifyCount))
                            {
                                RestoreBackup();
                                MessageBox.Show("Build Xml Error!");
                            }
                            State = GameFileState.Save;
                        }
                        break;
                    case GameFileType.ESP:
                        {
                            int ModifyCount = 0;
                            if (!EspReader.Save(ref ModifyCount))
                            {
                                RestoreBackup();
                                MessageBox.Show("Build Esp Error!");
                            }
                            State = GameFileState.Save;
                        }
                        break;
                    case GameFileType.PEX:
                        {
                            int ModifyCount = 0;
                            if (!PexReader.Save(ref ModifyCount))
                            {
                                RestoreBackup();
                                MessageBox.Show("Build Pex Error!");
                            }                        
                            State = GameFileState.Save;
                        }
                        break;
                    case GameFileType.MCM:
                        {
                            int ModifyCount = 0;
                            if (!MCMReader.Save(ref ModifyCount))
                            {
                                RestoreBackup();
                                MessageBox.Show("Build MCM Error!");
                            }
                            State = GameFileState.Save;
                        }
                        break;
                }

                if (this.ListView != null)
                {
                    OriginalDictionaryReader.WriteDictionary(this.ListView);
                }
                OriginalDictionaryReader.CreateDictionary();

                if (this.Win != null)
                {
                    this.Win.Dispatcher.Invoke(new Action(() =>
                    {
                        this.Win._Parent?.RemoveTab(this.Path);
                        MessageBoxExtend.Show(
                        this.Win._Parent,
                        "Msg",
                        "File saved successfully:\r\n" +
                        this.Path +
                        "\r\n\r\nRollback backup file created:\r\n" +
                        BackupManagePath,
                        PreviewDialogSeverity.Information
                        );
                        this.Win._Parent?.LoadFile(this.Path);
                    }));
                }
            }
        }

        public void Close()
        {
            DialNodeCache.Clear();
            OriginalDictionaryReader.Close();

            switch (this.Type)
            {
                case GameFileType.XML:
                    {
                        XmlReader.Close();
                    }
                    break;
                case GameFileType.JSON:
                    {
                        RamCacheReader.Close();
                    }
                    break;
                case GameFileType.ESP:
                    {
                        EspReader.Close();
                    }
                    break;
                case GameFileType.PEX:
                    {
                        this.PexReader.Close();
                    }
                    break;
                case GameFileType.MCM:
                    {
                        MCMReader.Close();
                    }
                    break;
            }
        }


        #region Translate Control
        public Thread PreparingTrd = null;
        public Thread InitTrd = null;
        public StateControl TranslationStatus = StateControl.Null;
        public bool SyncTransStateFreeze = false;

        public bool PreparingComplete = false;

        private int _InitGuard = 0;
        public bool FristInit
        {
            get => Interlocked.CompareExchange(ref _InitGuard, 0, 0) == 1;
            set => Interlocked.Exchange(ref _InitGuard, value ? 1 : 0);
        }

        public UnitContext<BaseUnit> BaseUnitStateChanged(BaseUnit Item, UnitTranslationState State)
        {
            if (State == UnitTranslationState.Queued)
            {
                if (ListView != null)
                {
                    FakeGrid QueryGrid = ListView.KeyToFakeGrid(Item.Key);

                    if (QueryGrid != null)
                    {
                        if (QueryGrid.Translated.Length == 0)
                        {
                            bool IsCloud = false;
                            QueryGrid.SyncData(this, ref IsCloud);
                        }
                    }
                }
            }

            return new UnitContext<BaseUnit>();
        }
        public void SetTransBarTittle(string Log)
        {
            if (Win != null)
            {
                Win.Dispatcher.Invoke(new Action(() =>
                {
                    Win.TransProcess.Content = Log;
                }));
            }
        }

        public int GetTranslateCount()
        {
            int TranslateCount = 0;
            for (int i = 0; i < ListView.Rows; i++)
            {
                var Row = ListView.RealLines[i];

                if (Row.Translated.Length > 0)
                {
                    TranslateCount++;
                }
            }

            return TranslateCount;
        }

        public void Prepare()
        {
            if (P_Translator != null)
            {
                var BatchCore = P_Translator.GetBatchCore();
                if (BatchCore != null)
                {
                    BatchCore.Close();
                }
            }

            if (!FristInit)
            {
                FristInit = true;
                PreparingComplete = false;
            }
            else
            {
                return;
            }

            if (PreparingTrd != null)
            {
                try
                {
                    PreparingTrd.Abort();
                }
                catch { }
                PreparingTrd = null;
            }

            if (InitTrd != null)
            {
                try
                {
                    InitTrd.Abort();
                }
                catch { }
                InitTrd = null;
            }

            PreparingTrd = new Thread(() =>
            {
                try
                {
                    while (Win.DataLoading == true)
                    {
                        Thread.Sleep(1000);
                    }

                    SetTransBarTittle("Preparing Translation Units...");

                    List<BaseUnit> BaseUnits = GetCanTransUnits();
                    InitTrd = new Thread(() =>
                    {
                        P_Translator.Init(BaseUnits, GetTranslateCount(),
                        new P_BucketContainer.CheckLinks((TempUnits, Unit) =>
                        {
                            return MultiWindowController.CheckLinks(this, TempUnits, Unit);
                        }));
                        InitTrd = null;
                    });


                    bool TempBool = true;

                    if (!TempBool)
                    {
                        InitTrd.Start();

                        while (InitTrd != null)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        InitTrd.Start();

                        var GetBatchCore = P_Translator.GetBatchCore();

                        while (GetBatchCore.ProcStage < 2)
                        {
                            Thread.Sleep(100);

                            SetTransBarTittle("Analyzing Words(" + GetBatchCore.Container.MarkHeadsPercent + "%)...");
                        }

                        Thread.Sleep(1000);

                        if (GetBatchCore.Container != null)
                            ListView.Parent.Dispatcher.Invoke(new Action(() =>
                            {
                                for (int i = 0; i < GetBatchCore.Container.Heads.Count; i++)
                                {
                                    string GetKey = GetBatchCore.Container.Heads.ElementAt(i).Key;

                                    for (int ir = 0; ir < ListView.VisibleRows.Count; ir++)
                                    {
                                        if (RowStyleWin.GetKey(ListView.VisibleRows[ir].View).Equals(GetKey))
                                        {
                                            RowStyleWin.MarkLeader(ListView.VisibleRows[ir].View, true);
                                            break;
                                        }
                                    }
                                }
                            }));

                        this.DialNodeCache.Clear();
                    }

                    PreparingComplete = true;

                    PreparingTrd = null;
                }
                catch
                {
                    PreparingComplete = false;
                }
            });

            PreparingTrd.Start();
        }

        public bool WaitStopSign()
        {
            while (TranslationStatus == StateControl.Stop)
            {
                Thread.Sleep(1000);

                if (TranslationStatus == StateControl.Cancel)
                {
                    if (P_Translator != null)
                    {
                        P_Translator.GetBatchCore()?.Close();
                    }

                    return true;
                }
            }

            if (TranslationStatus == StateControl.Cancel)
            {
                if (P_Translator != null)
                {
                    P_Translator.GetBatchCore()?.Close();
                }

                return true;
            }

            return false;
        }

        public List<BaseUnit> GetCanTransUnits()
        {
            List<BaseUnit> BaseUnits = new List<BaseUnit>();

            this.DialNodeCache.Clear();

            for (int i = 0; i < ListView.Rows; i++)
            {
                var Row = ListView.RealLines[i];
                bool IsCloud = false;
                Row.SyncData(this, ref IsCloud);

                bool HasAddAIMemory = false;

                if (!HasAddAIMemory)
                {
                    if (!string.IsNullOrEmpty(Row.Translated))
                    {
                        P_Translator.AddAIMemory(Row.GetSource(), Row.Translated);
                    }
                }

                bool IsEsp = false;
                if (Type == GameFileType.ESP)
                {
                    IsEsp = true;
                }

                if (NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase)
                {
                    if (IsEsp)
                    {
                        if (EspReader.Records.ContainsKey(Row.Key))
                        {
                            var GetRecord = EspReader.Records[Row.Key];

                            if (GetRecord.StringID > 0 && Row.Translated.Length > 0)
                            {
                                string AutoType = Row.Type;

                                if (AutoType == "Papyrus" || AutoType == "MCM")
                                {
                                    AutoType = string.Empty;
                                }
                                else
                                if (AutoType != "NPC_" && AutoType != "WRLD" && AutoType != "CLAS" && AutoType != "ARMO" && AutoType != "AMMO")
                                {
                                    AutoType = string.Empty;
                                }

                                AdvancedDictionaryItem NewItem = new AdvancedDictionaryItem(
                                    string.Empty,//The rule applies to all files.
                                    AutoType,//Automatically determine the type of the current term
                                    Row.GetRealSource(),//Get the source text corresponding to stringsfile id
                                    Row.Translated,//Get the translation content
                                    P_Translator.From,//Get source language
                                    P_Translator.To,//Get target language
                                    1,//Use full-word matching
                                    0,//Case sensitivity is not ignored
                                    string.Empty
                                    );
                                if (!AdvancedDictionary.CheckSame(NewItem))
                                {
                                    AdvancedDictionary.AddItem(NewItem);
                                }
                            }

                        }
                    }
                }

                if (Row.Translated.Trim().Length == 0)
                {
                    bool CanSet = true;

                    if (Row.Type.Equals("BOOK"))
                    {
                        if (Row.Key.EndsWith("DESC") && !NIMApp.SelfSetting.CanTranslateBook)
                        {
                            if (EngineEvents.SetDataCall != null)
                            {
                                EngineEvents.SetDataCall(0, "Skip Book fields:" + Row.Key);
                            }

                            CanSet = false;
                        }
                    }
                    else
                    if (Row.Score <= 0)
                    {
                        if (EngineEvents.SetDataCall != null)
                        {
                            EngineEvents.SetDataCall(0, "Skip Dangerous fields:" + Row.Key);
                        }

                        CanSet = false;
                    }

                    if (IsEsp)
                    {
                        var GetTrans = this.ToStringsFile?.QueryData(Row.Key);

                        if (GetTrans != null)
                        {
                            //Added to context memory. Helps AI improve accuracy.
                            P_Translator.AddAIMemory(Row.GetSource(), GetTrans.Value);
                            HasAddAIMemory = true;

                            var Link = P_Translator.GetLink();
                            Link[Row.Key] = new P_String(GetTrans.Value, 0);

                            var GetFakeGrid = ListView.KeyToFakeGrid(Row.Key);
                            if (GetFakeGrid != null)
                            {
                                Row.Translated = GetTrans.Value;

                                Row.SyncUI(ListView);
                            }

                            if (EngineEvents.SetDataCall != null)
                            {
                                EngineEvents.SetDataCall(0, "Skip StringsFile(" + GetTrans.Type.ToString() + ") fields:" + Row.Key);
                            }

                            CanSet = false;
                        }
                        else
                        {
                            if (EspReader.Records.ContainsKey(Row.Key))
                            {
                                if (EspReader.Records[Row.Key].StringID > 0)
                                {
                                    if (EngineEvents.SetDataCall != null)
                                    {
                                        EngineEvents.SetDataCall(0, "Skip StringsFile(" + EspReader.Records[Row.Key].String + ") fields:" + Row.Key);
                                    }

                                    CanSet = false;
                                }
                            }
                        }
                    }

                    if (CanSet)
                    {
                        var Link = P_Translator.GetLink();
                        if (Link[Row.Key] != null)
                        {
                            CanSet = false;
                        }

                        if (CanSet)
                        {
                            string Emotion = "";

                            if (this.Type == GameFileType.ESP)
                            {
                                var GetRecord = this.EspReader.Records[Row.Key];

                                if (GetRecord.ParentSig == "INFO" || GetRecord.ParentSig == "DIAL")
                                {
                                    var LinkData = this.EspReader.GetDialContext(GetRecord);

                                    if (LinkData != null)
                                    {
                                        this.DialNodeCache[GetRecord.UniqueKey] = LinkData;

                                        foreach (var Get in LinkData.Links)
                                        {
                                            if (Get.RecordOffset == GetRecord.ParentIndex && Get.SubOffset == GetRecord.SubIndex)
                                            {
                                                Emotion = EmotionTypeHelper.FromRaw(Get.EmotionType).ToString();
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            BaseUnits.Add(new BaseUnit(P_Translator.GetFileUniqueKey(),
                            Row.Key, Row.Type, Row.Source, Row.Translated, Emotion, Row.Score));
                        }
                    }
                }
            }

            return BaseUnits;
        }


        public void MakeReady()
        {
            NIMApp.EngineSetting.ProtectedPatterns.Clear();

            foreach (var GetStr in NIMApp.SelfSetting.P_Placeholders.Split(','))
            {
                if (GetStr.Trim().Length > 0)
                {
                    NIMApp.EngineSetting.ProtectedPatterns.Add(GetStr);
                }
            }

            ProxyCenter.UsingProxy();
        }

        public void SyncTransState(Action EndAction, bool IsKeep = false)
        {
            if (SyncTransStateFreeze)
            {
                EndAction.Invoke();
                return;
            }

            if (Win == null)
            {
                TranslationStatus = StateControl.Cancel;
                EndAction.Invoke();
                return;
            }

            new Thread(() =>
            {
                var GetBatchCore = P_Translator.GetBatchCore();

                if (TranslationStatus == StateControl.Run && !IsKeep)
                {
                    CancelTranslateWork();

                    Win?.UPDateUI();

                    FristInit = false;

                    bool NeedPrepare = false;

                    if (GetBatchCore.Container == null)
                    {
                        NeedPrepare = true;
                    }
                    else
                    {
                        if (GetBatchCore.Container.GetCount() == 0)
                        {
                            NeedPrepare = true;
                        }
                    }

                    if (NeedPrepare)
                    {
                        Prepare();

                        while (PreparingTrd != null)
                        {
                            Thread.Sleep(100);
                        }
                    }

                    if ((GetBatchCore.GetCount()) == 0)
                    {
                        TranslationStatus = StateControl.Cancel;
                        EndAction.Invoke();
                        return;
                    }

                    var BaseUnits = GetCanTransUnits();

                    if (BaseUnits.Count == 0)
                    {
                        TranslationStatus = StateControl.Cancel;
                        EndAction.Invoke();
                        return;
                    }

                    if (NIMApp.EngineSetting.AutoSetThreadLimit)
                    {
                        NIM_Engine.SyncTrdCount();
                    }

                    if (ListView != null)
                    {
                        SyncTransStateFreeze = true;

                        MakeReady();

                        SetTransBarTittle("Preparing Consistency...");

                        for (int i = 0; i < ListView.Rows; i++)
                        {
                            var Row = ListView.RealLines[i];
                            bool IsCloud = false;
                            Row.SyncData(this, ref IsCloud);

                            if (!string.IsNullOrEmpty(Row.Translated))
                            {
                                P_Translator.AddAIMemory(Row.GetSource(), Row.Translated);
                            }
                        }

                        int ModifyCount = 0;

                        ModifyCount = GetBatchCore.BaseTranslatedCount + GetBatchCore.TranslatedCount;

                        GetBatchCore.Start();

                        SyncTransStateFreeze = false;

                        EndAction.Invoke();

                        SetTransBarTittle(string.Format("STRINGS({0}/{1})", ModifyCount, ListView.Rows));

                        Thread.Sleep(1000);

                        if (WaitStopSign())
                        {
                            EndAction.Invoke();
                            return;
                        }

                        bool IsEnd = false;
                        int TotalCount = 0;

                        DateTime StartTime = DateTime.Now;

                        while (!IsEnd)
                        {
                            try
                            {
                                if (TranslationStatus == StateControl.Cancel)
                                {
                                    break;
                                }

                                if (!GetBatchCore.IsWorking && GetBatchCore.ProcStage != 10)
                                {
                                    if ((DateTime.Now - StartTime).TotalSeconds > 30)
                                        break;
                                }
                                else
                                {
                                    StartTime = DateTime.Now;
                                }

                                var GetUnit = GetBatchCore.DequeueTranslated(out IsEnd);

                                if (GetUnit != null)
                                {
                                    TotalCount++;
                                    P_Translator.SetLink(GetUnit.Key, new P_String(GetUnit.Translated, 0));
                                    SetTransBarTittle(string.Format("STRINGS({0}/{1})",
                                          GetBatchCore.BaseTranslatedCount + GetBatchCore.TranslatedCount, ListView.Rows));

                                    ListView.MainCanvas.Dispatcher.Invoke(new Action(() =>
                                    {
                                        bool IsCloud = false;
                                        ListView.KeyToFakeGrid(GetUnit.Key).SyncData(this, ref IsCloud);
                                    }));

                                }
                                else
                                if (!IsEnd)
                                {
                                    Thread.Sleep(10);
                                }

                                if (WaitStopSign())
                                {
                                    return;
                                }
                            }
                            catch
                            {
                                Thread.Sleep(10);
                            }
                        }

                        while (!GetBatchCore.TranslatedQueue.IsEmpty)
                        {
                            if (GetBatchCore.TranslatedQueue.TryDequeue(out var TailUnit))
                            {
                                //I feel that system-translated records should not be saved in the rollback.
                                //It's sufficient to only save the records translated and modified by users line by line. Otherwise, it would be too long to read.
                                P_Translator.SetLink(TailUnit.Key, new P_String(TailUnit.Translated, 0));
                            }
                        }

                        //DeFine.WorkWin.TransViewList.QuickRefresh();

                        var BatchCore = P_Translator.GetBatchCore();

                        if (BatchCore != null)
                        {
                            Thread.Sleep(500);
                            BatchCore.Close();
                        }

                        TranslationStatus = StateControl.Cancel;
                        EndAction.Invoke();

                        this.Win.Dispatcher.Invoke(new Action(() =>
                        {
                            this.ListView?.HotReload();
                        }));
                    }
                }
                else if (TranslationStatus == StateControl.Stop)
                {
                    SyncTransStateFreeze = true;

                    if (GetBatchCore != null)
                    {
                        GetBatchCore.Stop();
                    }

                    EndAction.Invoke();

                    SyncTransStateFreeze = false;
                }
                else if (TranslationStatus == StateControl.Cancel || TranslationStatus == StateControl.Null)
                {
                    SyncTransStateFreeze = true;

                    if (GetBatchCore != null)
                    {
                        try
                        {
                            GetBatchCore.Close();
                        }
                        catch { }
                    }

                    EndAction.Invoke();

                    SyncTransStateFreeze = false;

                    InteractiveView.CloseAll();
                }
                else
                {
                    SyncTransStateFreeze = true;

                    if (GetBatchCore != null)
                    {
                        GetBatchCore.Keep();
                    }

                    EndAction.Invoke();

                    SyncTransStateFreeze = false;

                    InteractiveView.CloseAll();
                }
            }).Start();
        }

        public void CancelTranslateWork()
        {
            RowStyleWin.RecordModifyStates.Clear();

            FristInit = false;

            var GetBatchCore = P_Translator.GetBatchCore();
            if (GetBatchCore != null)
            {
                GetBatchCore.Close();
                SetTransBarTittle(string.Format("STRINGS({0}/{1})", 0, 0));
            }

            if (PreparingTrd != null)
            {
                try
                {
                    PreparingTrd.Abort();
                }
                catch { }
                PreparingTrd = null;
            }

            if (InitTrd != null)
            {
                try
                {
                    InitTrd.Abort();
                }
                catch { }
                InitTrd = null;
            }

            InteractiveView.CloseAll();
        }

        #endregion
    }
}
