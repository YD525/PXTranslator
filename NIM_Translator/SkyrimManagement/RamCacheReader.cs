using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NIM.SkyrimModManager;
using NIM.TranslateManage;
using NIMEngine.Translate;
using NIMEngine.Memory;

namespace NIM.SkyrimManagement
{
    public class RamCacheReader
    {
        public Translator TranslatorRef = null;
        public RamCacheReader(Translator TranslatorRef)
        {
            this.TranslatorRef = TranslatorRef;
        }

        public List<FakeGrid> RamLines = new List<FakeGrid>();
        public void Load(string FilePath)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string GetRamCache = Encoding.UTF8.GetString(DataHelper.ReadFile(FilePath));
                    this.RamLines = JsonConvert.DeserializeObject<List<FakeGrid>>(GetRamCache);

                    if (RamLines != null)
                    {
                        foreach (var Get in RamLines)
                        {
                            TranslatorRef.GetLink().Add(Get.Key,new P_String(Get.Translated,0));
                        }
                    }
                }
            }
            catch { }
        }
        public void Close()
        {
            RamLines?.Clear();
        }

        public bool Save(ModFile Mod,string OutPutPath)
        {
            try
            {
                if (RamLines != null)
                {
                    for (int i = 0; i < RamLines.Count; i++)
                    {
                        bool IsCloud = false;
                        RamLines[i].SyncData(Mod, ref IsCloud);
                    }

                    string GetJson = JsonConvert.SerializeObject(RamLines, Formatting.Indented);

                    if (OutPutPath != null)
                    {
                        if (OutPutPath.Trim().Length > 0)
                        {
                            if (File.Exists(OutPutPath))
                            {
                                File.Delete(OutPutPath);
                            }
                            DataHelper.WriteFile(OutPutPath, Encoding.UTF8.GetBytes(GetJson));
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
