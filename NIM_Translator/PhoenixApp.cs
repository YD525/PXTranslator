using System.IO;
using System.Text;
using System.Windows.Media;
using NIM.SkyrimModManager;
using System.Windows;
using NIM.UIManagement;
using System;
using Newtonsoft.Json;
using NIM.UIManage;
using PhoenixEngine;
using PhoenixEngine.Language;
using PhoenixEngine.ADO;
using PhoenixEngine.Engine.ADO;
using NIM.ApplicationLayer;
using NIM.UIManagement.Preview;

namespace NIM
{
    public enum GameNames
    {
        Skyrim = 0
    }
    public static class MessageBoxExtend
    {
        static MessageBoxExtend()
        { 
        
        }
        internal static bool Show(Window Win,string Tittle,string Message, PreviewDialogSeverity Severity, bool RequiresConfirmation = false)
        {
            bool Result = false;
            Win.Dispatcher.Invoke(new Action(() => {
                WpfPreviewDialogService DialogService = new WpfPreviewDialogService(() => Win);
                Result = DialogService.Show(new PreviewDialogRequest(Tittle, Message, Severity, RequiresConfirmation));
            }));
            return Result;
        }
    }
    public class NIMApp
    {
        public static bool CanUpdateChart = false;
        public static int GlobalRequestTimeOut = 5000;
        public static int ViewMode = 0;

        public static SolidColorBrush DefBackGround = new SolidColorBrush(Color.FromRgb(11, 116, 209));
        public static SolidColorBrush SelectBackGround = new SolidColorBrush(Color.FromRgb(7, 82, 149));

        public static string PapyrusCompilerPath = "";

        public static int DefPageSize = 100;

        public static bool AutoTranslate = true;

        public static string BackupPath = @"\BackUpData\";

        public static string CurrentVersion = typeof(NIMApp).Assembly.GetName().Version.ToString();
        public static LocalSetting SelfSetting = new LocalSetting();

        public static EngineConfigJson EngineSetting
        {
            get 
            {
                return Phoenix.Config;
            }
            set
            { 
               Phoenix.Config = value;
            }
        }

        public static RowStyleWin RowStyleWin = new RowStyleWin();
        public static NodeStyleWin NodeStyleWin = new NodeStyleWin();

        public static PlatformConfigStyleWin PlatformConfigStyleWin = null;

        public static DataBaseView DataBaseView = null;

        public static NIMGui WorkWin = null;
        public static Window CurrentLayout = null;

        public static CGView CG = null;

        public static ChartData ChartDataRef = null;

        public static WordAutoComplete WordCompleter = null;

        public static void OpenDataBaseView(Window Parent,string SqlOrder = "")
        {
            if (DataBaseView == null)
            {
                DataBaseView = new DataBaseView();
                DataBaseView.Owner = Parent;
                DataBaseView.Show();
                if (SqlOrder.Length > 0)
                {
                    DataBaseView.QueryFirst(SqlOrder);
                }
            }
        }
        public static void CloseDataBaseView()
        {
            if (DataBaseView != null)
            {
                DataBaseView.Close();
                DataBaseView = null;
            }
        }

        public static void CloseAny()
        {
            Phoenix.SaveConfig();
            NIMApp.SelfSetting.SaveConfig();
            Environment.Exit(0);
        }

        public static string GetFullPath(string Path)
        {
            if (Path.Length > 0)
            {
                if (!Path.Trim().StartsWith(@"\"))
                {
                    Path = @"\" + Path;
                }
            }
            string GetShellPath = System.Windows.Forms.Application.StartupPath;
            if (GetShellPath.EndsWith(@"\"))
            {
                if (Path.StartsWith(@"\"))
                {
                    Path = Path.Substring(1);
                }
            }
            return GetShellPath + Path;
        }

        public static void PrepareFileDirectory()
        {
            if (!Directory.Exists(NIMApp.GetFullPath(@"\Library")))
            {
                Directory.CreateDirectory(NIMApp.GetFullPath(@"\Library"));
            }
            if (!Directory.Exists(NIMApp.GetFullPath(@"\Cache")))
            {
                Directory.CreateDirectory(NIMApp.GetFullPath(@"\Cache"));
            }
            if (!File.Exists(NIMApp.GetFullPath(@"\setting.config")))
            {
                var CreatNewLocalSetting = new LocalSetting();
                CreatNewLocalSetting.SaveConfig();
            }
            if (!Directory.Exists(NIMApp.GetFullPath(@"\CorePlugins")))
            {
                Directory.CreateDirectory(NIMApp.GetFullPath(@"\CorePlugins"));
            }
        }

        private static object ErrorReportLocker = new object();
        public static void SetSQLErrorReport()
        {
            P_SQLite.OnError += new Action<string>((ErrorMsg) =>
            {
                lock (ErrorReportLocker)
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (NIMApp.DataBaseView != null)
                        {
                            MessageBoxExtend.Show(NIMApp.DataBaseView, "SQL", ErrorMsg, PreviewDialogSeverity.Error);
                        }
                        else
                        if (NIMApp.WorkWin != null)
                        {
                            MessageBoxExtend.Show(NIMApp.WorkWin, "SQL", ErrorMsg, PreviewDialogSeverity.Error);
                        }
                    }));
            });
        }
        public static void Init(NIMGui Win)
        {
            if (Win != null)
            {
                NIMApp.WorkWin = Win;

                PlatformConfigStyleWin = new PlatformConfigStyleWin(NIMApp.WorkWin);

                ChartDataRef = new ChartData();

                RowStyleWin.Hide();
                SetSQLErrorReport();
            }
        }
    }

    public enum PhoenixLayout
    {
       Null = 0, Modern = 1, Classic = 2
    }

    public class LocalSetting
    {
        /// <summary>Provides the explicitly selected translation configuration preset.</summary>
        /// <remarks>Settings created before presets existed migrate safely to <c>Custom</c>.</remarks>
        public TranslationPreset Preset { get; set; } = TranslationPreset.Balanced;
        public int Style { get; set; } = 1;
        public double FormHeight { get; set; } = 850;
        public double FormWidth { get; set; } = 1200;
        public Languages CurrentUILanguage { get; set; } = Languages.English;
        public string SkyrimPath { get; set; } = "";

        public string LastSetModFolder { get; set; } = "";

        public GameNames GameType { get; set; } = GameNames.Skyrim;
        public double WritingAreaHeight { get; set; } = 0;
        public string ViewMode { get; set; } = "Normal";

        /// <summary>Gets or sets the preview control density preference.</summary>
        public string UiDensity { get; set; } = "Compact";

        public Languages SourceLanguage { get; set; } = Languages.English;
        public Languages TargetLanguage { get; set; } = Languages.English;

        public bool CanClearCloudTranslationCache { get; set; } = false;
        public bool CanClearUserInputTranslationCache { get; set; } = false;

        public bool AutoSpeak { get; set; } = false;

        public int ChatGPTTokenUsage { get; set; } = 0;
        public int GeminiTokenUsage { get; set; } = 0;
        public int CohereTokenUsage { get; set; } = 0;
        public int DeepSeekTokenUsage { get; set; } = 0;
        public int LocalAITokenUsage { get; set; } = 0;

        public bool AutoUpdateStringsFileToDatabase { get; set; } = false;

        public bool EnableLanguageDetect { get; set; } = true;
        public string P_Placeholders { get; set; } = "<(.*?)>,";

        public bool CanTranslateBook { get; set; } = true;
        public TextLayout TextDisplay { get; set; } = TextLayout.LTR;

        public bool ShowAssembly { get; set; } = false;
        public bool GenCSharp { get; set; } = true;
        public bool UseFullPunctuation { get; set; } = false;

        public bool UseFullPunctuationJa { get; set; } = false;


        public bool TableAuto { get; set; } = false;

        public bool WordCompletion { get; set; } = true;

        public string CustomFilterStr { get; set; } = "";

        public PhoenixLayout Layout { get; set; } = PhoenixLayout.Null;

        public void ReadConfig()
        {
            try
            {
                if (File.Exists(NIMApp.GetFullPath(@"\setting.config")))
                {
                    var GetStr = Encoding.UTF8.GetString(DataHelper.ReadFile(NIMApp.GetFullPath(@"\setting.config")));
                    if (GetStr.Trim().Length > 0)
                    {
                        var GetSetting = JsonConvert.DeserializeObject<LocalSetting>(GetStr);
                        if (GetSetting != null)
                        {
                            this.Preset = GetSetting.Preset;
                            this.Style = GetSetting.Style;
                            this.FormHeight = GetSetting.FormHeight;
                            this.FormWidth = GetSetting.FormWidth;
                            this.CurrentUILanguage = GetSetting.CurrentUILanguage;
                            this.SkyrimPath = GetSetting.SkyrimPath;
                            this.LastSetModFolder = GetSetting.LastSetModFolder;
                            this.GameType = GetSetting.GameType;
                            this.WritingAreaHeight = GetSetting.WritingAreaHeight;
                            this.ViewMode = GetSetting.ViewMode;
                            this.UiDensity = string.IsNullOrWhiteSpace(GetSetting.UiDensity)
                                ? "Compact"
                                : GetSetting.UiDensity;
                            this.SourceLanguage = GetSetting.SourceLanguage;
                            this.TargetLanguage = GetSetting.TargetLanguage;
                            this.CanClearCloudTranslationCache = GetSetting.CanClearCloudTranslationCache;
                            this.CanClearUserInputTranslationCache = GetSetting.CanClearUserInputTranslationCache;
                            this.AutoSpeak = GetSetting.AutoSpeak;

                            this.ChatGPTTokenUsage = GetSetting.ChatGPTTokenUsage;
                            this.GeminiTokenUsage = GetSetting.GeminiTokenUsage;
                            this.CohereTokenUsage = GetSetting.CohereTokenUsage;
                            this.DeepSeekTokenUsage = GetSetting.DeepSeekTokenUsage;
                            this.LocalAITokenUsage = GetSetting.LocalAITokenUsage;

                            this.AutoUpdateStringsFileToDatabase = GetSetting.AutoUpdateStringsFileToDatabase;

                            this.EnableLanguageDetect = GetSetting.EnableLanguageDetect;
                            this.P_Placeholders = GetSetting.P_Placeholders;
                            this.CanTranslateBook = GetSetting.CanTranslateBook;

                            this.TextDisplay = GetSetting.TextDisplay;

                            this.ShowAssembly = GetSetting.ShowAssembly;
                            this.GenCSharp = GetSetting.GenCSharp;

                            this.UseFullPunctuation = GetSetting.UseFullPunctuation;
                            this.UseFullPunctuationJa = GetSetting.UseFullPunctuationJa;

                            this.TableAuto = GetSetting.TableAuto;
                            this.WordCompletion = GetSetting.WordCompletion;

                            this.CustomFilterStr = GetSetting.CustomFilterStr;

                            this.Layout = GetSetting.Layout;
                        }
                    }
                    else
                    {
                        LocalSetting CopySetting = this;
                        var GetSettingContent = JsonConvert.SerializeObject(CopySetting);
                        DataHelper.WriteFile(NIMApp.GetFullPath(@"\setting.config"), Encoding.UTF8.GetBytes(GetSettingContent));
                    }
                }
            }
            catch { }
        }

        public void SaveConfig()
        {
            LocalSetting CopySetting = this;
            var GetSettingContent = JsonConvert.SerializeObject(CopySetting, Formatting.Indented);

            DataHelper.WriteFile(NIMApp.GetFullPath(@"\setting.config"), Encoding.UTF8.GetBytes(GetSettingContent));

            Phoenix.SaveConfig();
        }
    }
}
