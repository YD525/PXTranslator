using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NIM.ModParser;
using PhoenixEngine.Language;
using PhoenixEngine.Translate;

namespace NIM.SkyrimManagement
{
    public enum SkyrimEntryType
    {
        Null,
        Folder,
        Mod
    }

    public class SkyrimEntry
    {
        public SkyrimEntryType Type = SkyrimEntryType.Null;

        public string Name = "";
        public string Path = "";

        public DateTime CreateTime = DateTime.MinValue;

        public SkyrimMod Mod = null;

        public SkyrimEntry()
        {
        }

        public SkyrimEntry(DateTime CreateTime,SkyrimMod Mod)
        {
            this.Name = Mod.ModName;
            this.Path = Mod.ModPath;
            this.Type = SkyrimEntryType.Mod;

            this.CreateTime = CreateTime;
            this.Mod = Mod;
        }

        public SkyrimEntry(DateTime CreateTime,string Path)
        {
            this.Name = System.IO.Path.GetFileName(Path);
            this.Path = Path;
            this.Type = SkyrimEntryType.Folder;

            this.CreateTime = CreateTime;
        }
    }

    public class SkyrimMod
    {
        public string ModPath = "";
        public string ModName = "";
        public long ModID = 0;
       
        public List<string> AvailableFiles = new List<string>();
        public SkyrimMod()
        { 
        
        }
        public SkyrimMod(string ModPath, string ModName,  List<string> AvailableFiles, long ModID)
        {
            this.ModPath = ModPath;
            this.ModName = ModName;
            this.AvailableFiles = AvailableFiles;

            this.ModID = ModID;
        }
        public string GetUrl()
        {
            return string.Format("https://www.nexusmods.com/skyrimspecialedition/mods/{0}", this.ModID);
        }
    }

    public class ModReader
    {
        private Translator _Instance;

        public EspReader Esp = null;
        public PexReader Pex = null;
        public MCMReader MCM = null;

        public void Init()
        {
            Close();
            _Instance = new Translator("ModSearch", Languages.English, Languages.English, true);

            Esp = new EspReader();
            Esp.Create(0,new PhoenixEngine.Memory.P_Dict<string, PhoenixEngine.Memory.P_String>());
            Pex = new PexReader();
            Pex.Create(0, new PhoenixEngine.Memory.P_Dict<string, PhoenixEngine.Memory.P_String>());
            MCM = new MCMReader();
            MCM.Create(0, new PhoenixEngine.Memory.P_Dict<string, PhoenixEngine.Memory.P_String>());
        }

        public void ChangeUniqueKey(int i)
        { 
            Esp.FileUniqueKey = i;
            Pex.FileUniqueKey = i;
            MCM.FileUniqueKey = i;
        }

        public bool Contains(string Path, string Str)
        {
            if (Path.EndsWith(".esp") || Path.EndsWith(".esm") || Path.EndsWith(".esl"))
            {
                foreach (var Get in Esp.Load(Path))
                {
                    if (Get.Value.String.Contains(Str))
                    {
                        Esp.Close();
                        return true;
                    }
                }
            }
            else
            if (Path.EndsWith(".pex"))
            {
                Pex.Load(CodeGenStyle.Papyrus, true, Path);
                if (Pex.Code.Contains(Str))
                {
                    return true;
                }
            }
            else
            if (Path.EndsWith(".txt"))
            {
                foreach (var Get in MCM.Load(Path))
                {
                    if (Get.Value.Source.Contains(Str))
                    {
                        MCM.Close();
                        return true;
                    }
                }
            }

            return false;
        }

        public void Close()
        {
            _Instance?.Close();
            _Instance = null;

            Esp?.Close();
            Pex?.Close();
            MCM?.Close();
        }
    }

    public class ModStringSearcher
    {
        public Dictionary<SkyrimEntry, List<string>> SearchStr(List<SkyrimEntry> Entries, string Str)
        {
            Dictionary<SkyrimEntry, List<string>> NModRetrievalInfo = new Dictionary<SkyrimEntry, List<string>>();
            ModReader NReader = new ModReader();
            NReader.Init();

            int i = 0;

            foreach (SkyrimEntry Entry in Entries)
            {
                if (Entry.Type == SkyrimEntryType.Mod)
                {
                    i++;

                    NReader.ChangeUniqueKey(i);

                    foreach (var GetFile in Entry.Mod.AvailableFiles)
                    {
                        if (NReader.Contains(GetFile, Str))
                        {
                            if (!NModRetrievalInfo.ContainsKey(Entry))
                            {
                                NModRetrievalInfo.Add(Entry, new List<string>());
                            }

                            NModRetrievalInfo[Entry].Add(GetFile);
                        }
                    }
                }
            }

            NReader.Close();
            return NModRetrievalInfo;
        }
        public List<SkyrimEntry> ScanMods(string TargetPath)
        {
            List<SkyrimEntry> Entries = new List<SkyrimEntry>();

            if (Directory.Exists(TargetPath))
            {
                if (!IsMod(TargetPath, out string CModName, out List<string> CAvailableFiles,out long CModID))
                {
                    foreach (var GetChildPath in Directory.GetDirectories(TargetPath))
                    {
                        DateTime FolderCreateTime = Directory.GetCreationTime(GetChildPath);

                        //To ensure performance, only one level of the directory is scanned.
                        if (IsMod(GetChildPath, out string ModName, out List<string> AvailableFiles, out long ModID))
                        {
                            Entries.Add(new SkyrimEntry(FolderCreateTime,new SkyrimMod(GetChildPath, ModName, AvailableFiles, ModID)));
                        }
                        else
                        {
                            //Paths where no Mod was found are still added to the array, making it easier for the user to select a path at the next level.
                            Entries.Add(new SkyrimEntry(FolderCreateTime,GetChildPath));
                        }
                    }
                }
                else
                {
                    DateTime TargetCreateTime = Directory.GetCreationTime(TargetPath);

                    Entries.Add(new SkyrimEntry(TargetCreateTime,new SkyrimMod(TargetPath,CModName,CAvailableFiles,CModID)));
                }
            }

            Entries.Sort((x, y) => y.CreateTime.CompareTo(x.CreateTime));

            return Entries;
        }

        public bool IsMod(string ModPath, out string ModName, out List<string> AvailableFiles,out long ModID)
        {
            AvailableFiles = new List<string>();
            ModName = string.Empty;

            bool IsMod = false;

            foreach (var GetFile in Directory.GetFiles(ModPath))
            {
                if (GetFile.EndsWith(".esp") || GetFile.EndsWith(".esm") || GetFile.EndsWith(".esl"))
                {
                    //Esp
                    AvailableFiles.Add(GetFile);
                    IsMod = true;
                }
            }

            string ScriptPath = Path.Combine(ModPath, "scripts");

            if (Directory.Exists(ScriptPath))
            {
                foreach (var GetFile in Directory.GetFiles(ScriptPath))
                {
                    if (GetFile.EndsWith(".pex"))
                    {
                        //Script
                        AvailableFiles.Add(GetFile);
                        IsMod = true;
                    }
                }
            }

            string SKSEPluginPath = Path.Combine(ModPath, "SKSE", "Plugins");

            if (Directory.Exists(SKSEPluginPath))
            {
                foreach (var GetFile in Directory.GetFiles(SKSEPluginPath))
                {
                    if (GetFile.EndsWith(".dll"))
                    {
                        //DLL 
                        IsMod = true;
                    }
                }
            }

            string InterfacePath = Path.Combine(ModPath, "Interface", "Translations");

            if (Directory.Exists(InterfacePath))
            {
                foreach (var GetFile in Directory.GetFiles(InterfacePath))
                {
                    if (GetFile.EndsWith(".txt"))
                    {
                        //Script MCM
                        AvailableFiles.Add(GetFile);

                        IsMod = true;
                    }
                }
            }

            if (IsMod)
            {
                ModName = Path.GetFileName(ModPath);

                string MetaPath = Path.Combine(ModPath, "meta.ini");

                if (File.Exists(MetaPath))
                {
                    string Content = File.ReadAllText(MetaPath);

                    Match Match = Regex.Match(Content, @"(?m)^\s*modid\s*=\s*(\d+)\s*$");

                    if (Match.Success)
                    {
                        ModID = int.Parse(Match.Groups[1].Value);
                    }
                    else
                    {
                        ModID = -1;
                    }
                }
                else
                {
                    ModID = -2;
                }
            }
            else
            {
                ModID = 0;
            }

            return IsMod;
        }
    }
}
