using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System;
using NIMEngine.Language;

namespace NIM.UIManage
{

    public class UILanguageHelper
    {
        public static class VisualTreeHelperExtensions
        {
            public static List<T> GetAllChildren<T>(DependencyObject parent) where T : DependencyObject
            {
                List<T> result = new List<T>();

                if (parent == null)
                    return result;

                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T tChild)
                    {
                        result.Add(tChild);
                    }
                    result.AddRange(GetAllChildren<T>(child));
                }

                return result;
            }
        }

        public static List<Languages> GetSupportedLanguages()
        {
            List<Languages> LanguageList = new List<Languages>();

            foreach (var Language in Enum.GetValues(typeof(Languages)))
            {
                LanguageList.Add((Languages)Language); 
            }

            return LanguageList.OrderBy(lang => lang == Languages.Auto ? 0 : 1).ToList();
        }

        public static void UPDateUI(string Key, string Value)
        {
            //switch (Key)
            //{
            //    case "﻿UINormal":
            //        {
            //            DeFine.WorkingWin.UINormal.Content = Value;
            //        }
            //    break;
            //    case "UIQuick":
            //        {
            //            DeFine.WorkingWin.UIQuick.Content = Value;
            //        }
            //    break;
            //    case "LoadFileButton":
            //        {
            //            DeFine.WorkingWin.LoadFileButton.Content = Value;
            //        }
            //    break;
            //    case "RefreshButton":
            //        {
            //            DeFine.WorkingWin.RefreshButton.Content = Value;
            //        }
            //    break;
            //    case "UIClone":
            //        { 
            //           DeFine.WorkingWin.UIClone.Content = Value;
            //        }
            //    break;
            //    case "UIClear":
            //        {
            //            DeFine.WorkingWin.UIClear.Content = Value;
            //        }
            //    break;
            //    case "UIReplace":
            //        {
            //            DeFine.WorkingWin.UIReplace.Content = Value;
            //        }
            //    break;
            //    case "HistoryButtonFont":
            //        {
            //            DeFine.WorkingWin.HistoryButtonFont.Content = Value;
            //        }
            //    break;
            //    case "UIType":
            //        {
            //            DeFine.WorkingWin.UIType.Content = Value;
            //        }
            //    break;
            //    case "UIKey":
            //        {
            //            DeFine.WorkingWin.UIKey.Content = Value;
            //        }
            //    break;
            //    case "UIOriginal":
            //        {
            //            DeFine.WorkingWin.UIOriginal.Content = Value;
            //        }
            //    break;
            //    case "UITranslated":
            //        {
            //            DeFine.WorkingWin.UITranslated.Content = Value;
            //        }
            //    break;
            //    case "UIOriginalCap":
            //        {
            //            DeFine.WorkingWin.UIOriginalCap.Content = Value;
            //        }
            //    break;
            //    case "UITranslatedCap":
            //        {
            //            DeFine.WorkingWin.UITranslatedCap.Content = Value;
            //        }
            //    break;
            //    //HistoryList
            //    case "ChangeTime":
            //        {
            //            ((GridView)DeFine.WorkingWin.HistoryList.View).Columns[0].Header = Value;
            //        }
            //    break;
            //    case "Translated":
            //        {
            //            ((GridView)DeFine.WorkingWin.HistoryList.View).Columns[1].Header = Value;
            //        }
            //    break;
            //    //Footer
            //    case "UIAutoSpeak":
            //        { 
            //            DeFine.WorkingWin.UIAutoSpeak.Content = Value;
            //        }
            //    break;
            //    case "UICancel":
            //        {
            //            DeFine.WorkingWin.UICancel.Content = Value;
            //        }
            //    break;
            //    case "TranslateOTButtonFont":
            //        {
            //            DeFine.WorkingWin.TranslateOTButtonFont.Content = Value;
            //        }
            //    break;
            //    case "UIApply":
            //        {
            //            DeFine.WorkingWin.UIApply.Content = Value;
            //        }
            //    break;
            //}
        }
        //public static Dictionary<string, string> UICache = new Dictionary<string, string>();
        public static void ChangeLanguage(Languages SetLanguage)
        {
            //UICache.Clear();
            //string SetPath = DeFine.GetFullPath(@"\Interface\Translations\Lex_" + SetLanguage.ToString().ToUpper() + ".txt");
            //MCMReader NewReader = new MCMReader(TranslatorInterface.Instance);
            //if (File.Exists(SetPath))
            //{
            //    NewReader.LoadMCM(SetPath);

            //    foreach (var GetMCMItem in NewReader.MCMItems)
            //    {
            //        UPDateUI(GetMCMItem.EditorID, GetMCMItem.SourceText);
            //        if (!UICache.ContainsKey(GetMCMItem.EditorID))
            //        {
            //            UICache.Add(GetMCMItem.EditorID, GetMCMItem.SourceText);
            //        }
            //    }
            //}
            //else
            //{
            //    MessageBox.Show("The interface translation file was not found.");

            //    DeFine.GlobalLocalSetting.CurrentUILanguage = Languages.English;

            //    DeFine.CloseAny();
            //}
        }
    }
}
