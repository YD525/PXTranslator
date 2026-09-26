using System.Collections.Generic;
using System.IO;
using System.Text;
using NIM.SkyrimModManager;
using Newtonsoft.Json;

namespace NIM.SkyrimManage
{
    public class OriginalDictionary
    {
        public string ModName { get; set; } = "";
        public List<OriginalTextItem> Dictionary { get; set; } = new List<OriginalTextItem>();
    }
    public class OriginalTextItem
    {
        public string Key { get; set; } = "";
        public string OriginalText { get; set; } = "";

        public OriginalTextItem()
        { 
        
        }

        public OriginalTextItem(string Key, string OriginalText)
        { 
           this.Key = Key;
           this.OriginalText = OriginalText;
        }

        public OriginalTextItem(OriginalTextItem Item)
        {
            this.Key = Item.Key;
            this.OriginalText = Item.OriginalText;
        }
    }
    public class OriginalDictionaryReader
    {
        public OriginalDictionary CurrentFile = null;
        public Dictionary<string, OriginalTextItem> Dictionary = new Dictionary<string, OriginalTextItem>();

        public void Close()
        {
            CurrentFile = new OriginalDictionary();
            Dictionary.Clear();
            CurrentModName = string.Empty;
        }

        public bool CheckDictionary()
        {
            string ModName = CurrentModName;
            string SetPath = NIMApp.GetFullPath(@"\Library\" + ModName + ".Json");
            if (File.Exists(SetPath))
            {
                return true;
            }
            return false;
        }

        public int WriteDictionary(YDListView View)
        {
            int ReplaceCount = 0;

            View.MainCanvas.Dispatcher.Invoke(new System.Action(() => {
               
                for (int i = 0; i < View.Rows; i++)
                {
                    FakeGrid GetFakeGrid = View.RealLines[i];

                    string GetKey = GetFakeGrid.Key;
                    string GetSourceText = GetFakeGrid.Source;
                    var TargetText = GetFakeGrid.Translated;

                    this.UPDateTransText(GetKey, GetSourceText);

                    ReplaceCount++;
                }
            }));

            return ReplaceCount;
        }
        public void CreateDictionary()
        {
            string ModName = CurrentModName;
            string SetPath = NIMApp.GetFullPath(@"\Library\" + ModName) + ".Json";

            CurrentFile = new OriginalDictionary();

            foreach (var Get in Dictionary)
            {
                CurrentFile.ModName = ModName;
                CurrentFile.Dictionary.Add(Get.Value);
            }

            if (File.Exists(SetPath))
            {
                File.Delete(SetPath);
            }

            string GetJson = JsonConvert.SerializeObject(CurrentFile, Formatting.Indented);

            DataHelper.WriteFile(SetPath,Encoding.UTF8.GetBytes(GetJson));
        }

        public string CurrentModName = string.Empty;
        public void ReadDictionary(string ModName)
        {
            CurrentModName = ModName;
            Dictionary.Clear();

            string SetPath = NIMApp.GetFullPath(@"\Library\" + ModName) + ".Json";
            if (File.Exists(SetPath))
            {
                string GetData = Encoding.UTF8.GetString(DataHelper.ReadFile(SetPath));
                var GetClass = JsonConvert.DeserializeObject<OriginalDictionary>(GetData);
                if (GetClass != null)
                {
                    CurrentFile = GetClass;

                    foreach (var Get in CurrentFile.Dictionary)
                    {
                        Dictionary.Add(Get.Key,new OriginalTextItem(Get));
                    }
                }
            }
        }

        public OriginalTextItem CheckDictionary(string Key)
        {
            if (Dictionary.ContainsKey(Key))
            { 
               return Dictionary[Key];
            }

            return null;
        }

        public int UPDateTransText(string Key,string OriginalText)
        {   
            if (Dictionary.ContainsKey(Key))
            {
                Dictionary[Key].OriginalText = OriginalText;
                return 1;
            }
            else
            {
                Dictionary.Add(Key,new OriginalTextItem(Key,OriginalText));
                return 2;
            }
        }
    }
}
