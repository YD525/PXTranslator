using System.Collections.Generic;
using System.IO;
using System.Text;
using PhoenixEngine.Memory;

namespace NIM.ModParser
{
    public struct MCMStrings
    {
        public string EditID;
        public string Source;
        public string Translated;
    }
    public class MCMReader : IModReader<Dictionary<string, MCMStrings>>
    {
        public Dictionary<string, MCMStrings> _Document = null;

        public int FileUniqueKey = 0;

        public string CurrentPath = string.Empty;

        private P_Dict<string, P_String> _LinkRef = null;

        public void Create(int FileUniqueKey, P_Dict<string, P_String> Link)
        {
            this.FileUniqueKey = FileUniqueKey;
            this._LinkRef = Link;
        }

        private bool CheckIsMCM(List<string> Lines)
        {
            int MaxCheckCount = 2;
            foreach (var Get in Lines)
            {
                if (MaxCheckCount > 0)
                {
                    if (Get.Trim().Length > 0)
                    {
                        MaxCheckCount--;
                        if ((Get.StartsWith("$") || Get.StartsWith("#")) && (Get.Contains("\t") || Get.Contains(" ")))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        public void Close()
        {
            _Document?.Clear();
        }

        private static byte[] ReadFile(string Path)
        {
            byte[] Data = null;

            if (File.Exists(Path))
            {
                using (FileStream FS =
                          new FileStream(Path, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader BR = new BinaryReader(FS))
                    {
                        Data = BR.ReadBytes((int)FS.Length);
                    }
                }
            }
            else
            {
                return new byte[0];
            }

            return Data;
        }

        public Dictionary<string, MCMStrings> Load(string Path)
        {
            Close();

            var GetData = ReadFile(Path);

            this.CurrentPath = Path;

            Encoding Encoder = FileEncodeParser.GetFileEncodeType(Path);
            var FileStr = Encoder.GetString(GetData);

            List<string> Lines = new List<string>();

            foreach (var GetLine in FileStr.Split(new char[2] { '\r', '\n' }))
            {
                if (GetLine.Trim().Length > 0)
                {
                    //Remove invisible BOM character (U+FEFF) from the beginning of the line.
                    string Line = GetLine.TrimStart('\uFEFF').Trim();
                    // Convert escaped newline characters (\n) to real newline characters for display.
                    Line = Line.Replace("\\n", "\n");

                    Lines.Add(Line);
                }
            }

            if (CheckIsMCM(Lines))
            {
                _Document = ReadMCMConfig(Lines);
                return _Document;
            }

            return new Dictionary<string, MCMStrings>();
        }
        //SystemDataWriter.PreFormatStr

        public Dictionary<string, MCMStrings> GetData()
        {
            return _Document;
        }

        private Dictionary<string, MCMStrings> ReadMCMConfig(List<string> Lines)
        {
            Dictionary<string, MCMStrings> Data = new Dictionary<string, MCMStrings>();

            for (int i = 0; i < Lines.Count; i++)
            {
                string GetLine = Lines[i];

                if (GetLine.StartsWith("$") && (GetLine.Contains("\t") || GetLine.Contains(" ")))
                {
                    string AutoSplictChar = "";

                    if (GetLine.Contains("\t"))
                    {
                        AutoSplictChar = "\t";
                    }
                    else
                    if (GetLine.Contains(" "))
                    {
                        AutoSplictChar = " ";
                    }

                    string GetEditorID = GetLine.Substring(0, GetLine.IndexOf(AutoSplictChar));
                    string GetSourceValue = GetLine.Substring(GetEditorID.Length);
                    GetSourceValue = GetSourceValue.Trim();

                    string Key = this.FileUniqueKey + "_" + GetEditorID;

                    string AutoFindTranslated = "";

                    if (_LinkRef.ContainsKey(Key))
                    {
                        AutoFindTranslated = _LinkRef[Key].String;
                    }

                    Data[Key] = new MCMStrings()
                    {
                        EditID = GetEditorID,
                        Source = GetSourceValue,
                        Translated = AutoFindTranslated
                    };
                }
            }

            return Data;
        }

        public bool Save(ref int ModifyCount)
        {
            if (File.Exists(CurrentPath))
            {
                File.Delete(CurrentPath);
            }

            StringBuilder RichText = new StringBuilder();

            foreach (var GetMCMItem in _Document)
            {
                var Link = _LinkRef[GetMCMItem.Key];

                if (Link != null)
                {
                    string NewStr = Link.String;
                    // Convert actual newline characters to escaped \n format used in MCM TXT files.
                    NewStr = NewStr.Replace("\r\n", "\\n").Replace("\n", "\\n");

                    RichText.Append('$')
                            .Append(GetMCMItem.Value.EditID)
                            .Append('\t')
                            .Append(NewStr)
                            .Append("\r\n");

                    ModifyCount++;
                }


            }

            try
            {
                // Save as UTF-8 with BOM.
                // Compatible with Papyrus Script MCM TXT files and ensures correct encoding detection by text editors.
                File.WriteAllText(CurrentPath, RichText.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
