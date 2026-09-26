using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhoenixEngine.Language;

namespace NIM.SkyrimManagement
{
    public enum StringsFileType
    {
        Null = 0,
        Strings = 1,
        IL = 2,
        DL = 3
    }

    public class StringItem
    {
        public uint ID = 0;
        public StringsFileType Type;
        public string Value = "";
    }

    public class StringsFileReader
    {
        public Dictionary<uint, StringItem> Strings { get; set; }

        public Languages CurrentLanguage { get; private set; }

        private static readonly string[] StringFileTypes = { "STRINGS", "DLSTRINGS", "ILSTRINGS" };

        public StringItem QueryData(string Key)
        {
            return null;
        }

        public void Close()
        {
            Strings.Clear();
            CurrentLanguage = Languages.English;
        }

        public StringsFileReader()
        {
            Strings = new Dictionary<uint, StringItem>();
            CurrentLanguage = Languages.English;
        }

        public bool LoadStringsFiles(string EspPath, Languages Language = Languages.English)
        {
            if (EspPath.Length == 0)
            {
                return false;
            }

            CurrentLanguage = Language;
            Strings.Clear();

            bool LoadedAny = false;

            for (int I = 0; I < StringFileTypes.Length; I++)
            {
                string FileType = StringFileTypes[I];
                StringsFileType Type = GetFileTypeEnum(FileType);
                string StringsPath = BuildStringsFilePath(EspPath, Language, FileType);

                if (File.Exists(StringsPath))
                {
                    Console.WriteLine($"Found strings file: {StringsPath}");
                    if (LoadSingleStringsFile(StringsPath, Type))
                    {
                        LoadedAny = true;
                    }
                }
                else
                {
                    Console.WriteLine($"Strings file not found: {StringsPath}");
                }
            }

            Console.WriteLine($"Total strings loaded: {Strings.Count}");
            return LoadedAny;
        }

        private bool LoadSingleStringsFile(string FilePath, StringsFileType FileType)
        {
            try
            {
                using (BinaryReader Reader = new BinaryReader(File.OpenRead(FilePath)))
                {
                    uint Count = Reader.ReadUInt32();
                    uint DataSize = Reader.ReadUInt32();

                    if (Count == 0 || DataSize == 0)
                    {
                        Console.WriteLine($"Invalid strings file header: {FilePath}");
                        return false;
                    }

                    var Directory = new List<(uint StringID, uint Offset)>();
                    for (uint I = 0; I < Count; I++)
                    {
                        uint StringID = Reader.ReadUInt32();
                        uint Offset = Reader.ReadUInt32();
                        Directory.Add((StringID, Offset));
                    }

                    byte[] DataBuffer = Reader.ReadBytes((int)DataSize);

                    int LoadedCount = 0;

                    bool HasLengthPrefix = (FileType == StringsFileType.DL || FileType == StringsFileType.IL);

                    foreach (var Entry in Directory)
                    {
                        uint StringID = Entry.StringID;
                        uint Offset = Entry.Offset;

                        if (Offset >= DataSize)
                            continue;

                        string Text;

                        if (HasLengthPrefix)
                        {
                            if (Offset + 4 > DataSize)
                                continue;

                            uint Length = BitConverter.ToUInt32(DataBuffer, (int)Offset);

                            if (Offset + 4 + Length > DataSize)
                                continue;

                            byte[] StringBytes = new byte[Length];
                            Array.Copy(DataBuffer, Offset + 4, StringBytes, 0, Length);

                            Text = Encoding.UTF8.GetString(StringBytes).TrimEnd('\0');
                        }
                        else
                        {
                            int EndPos = (int)Offset;
                            while (EndPos < DataSize && DataBuffer[EndPos] != 0)
                            {
                                EndPos++;
                            }

                            int Length = EndPos - (int)Offset;
                            byte[] StringBytes = new byte[Length];
                            Array.Copy(DataBuffer, Offset, StringBytes, 0, Length);

                            Text = Encoding.UTF8.GetString(StringBytes);
                        }

                        var Item = new StringItem
                        {
                            ID = StringID,
                            Type = FileType,
                            Value = Text
                        };

                        if (Strings.ContainsKey(StringID))
                        {
                            Console.WriteLine($"⚠ Warning: Duplicate StringID {StringID} (0x{StringID:X8})");
                        }

                        Strings[StringID] = Item;
                        LoadedCount++;
                    }

                    Console.WriteLine($"Loaded {LoadedCount} strings from: {FilePath}");
                    return true;
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine($"Error loading strings file {FilePath}: {Ex.Message}");
                return false;
            }
        }

        //We'll leave this for now. If the ESP file is bound to StringsFile, the modified data needs to be written to StringsFile instead of ESP. For now, just displaying an "not supported" message will suffice.

        //public bool SaveStringsFile(string FilePath, IEnumerable<StringItem> StringsToSave)
        //{
        //    try
        //    {
        //        var SortedStrings = StringsToSave.OrderBy(Item => Item.ID).ToList();

        //        using (BinaryWriter Writer = new BinaryWriter(File.Create(FilePath)))
        //        {
        //            uint Count = (uint)SortedStrings.Count;
        //            var Directory = new List<(uint StringID, uint Offset)>();
        //            var DataStream = new MemoryStream();

        //            foreach (var Item in SortedStrings)
        //            {
        //                uint StringID = Item.ID;
        //                uint CurrentOffset = (uint)DataStream.Position;

        //                byte[] StringBytes = Encoding.UTF8.GetBytes(Item.Value);
        //                byte[] StringBytesWithNull = new byte[StringBytes.Length + 1];
        //                Array.Copy(StringBytes, StringBytesWithNull, StringBytes.Length);
        //                StringBytesWithNull[StringBytes.Length] = 0;

        //                uint Length = (uint)StringBytesWithNull.Length;
        //                DataStream.Write(BitConverter.GetBytes(Length), 0, 4);
        //                DataStream.Write(StringBytesWithNull, 0, StringBytesWithNull.Length);

        //                Directory.Add((StringID, CurrentOffset));
        //            }

        //            byte[] DataBuffer = DataStream.ToArray();
        //            uint DataSize = (uint)DataBuffer.Length;

        //            Writer.Write(Count);
        //            Writer.Write(DataSize);

        //            foreach (var Entry in Directory)
        //            {
        //                Writer.Write(Entry.StringID);
        //                Writer.Write(Entry.Offset);
        //            }

        //            Writer.Write(DataBuffer);
        //        }

        //        return true;
        //    }
        //    catch (Exception Ex)
        //    {
        //        Console.WriteLine($"Error saving strings file {FilePath}: {Ex.Message}");
        //        return false;
        //    }
        //}

        //public bool SaveAllStringsFiles(string EspPath, Languages Language = Languages.English)
        //{
        //    bool AllSuccess = true;

        //    var StringsGroup = Strings.Values.Where(Item => Item.Type == StringsFileType.Strings);
        //    var ILStringsGroup = Strings.Values.Where(Item => Item.Type == StringsFileType.IL);
        //    var DLStringsGroup = Strings.Values.Where(Item => Item.Type == StringsFileType.DL);

        //    if (StringsGroup.Any())
        //    {
        //        string StringsPath = BuildStringsFilePath(EspPath, Language, "STRINGS");
        //        if (!SaveStringsFile(StringsPath, StringsGroup))
        //            AllSuccess = false;
        //    }

        //    if (ILStringsGroup.Any())
        //    {
        //        string ILStringsPath = BuildStringsFilePath(EspPath, Language, "ILSTRINGS");
        //        if (!SaveStringsFile(ILStringsPath, ILStringsGroup))
        //            AllSuccess = false;
        //    }

        //    if (DLStringsGroup.Any())
        //    {
        //        string DLStringsPath = BuildStringsFilePath(EspPath, Language, "DLSTRINGS");
        //        if (!SaveStringsFile(DLStringsPath, DLStringsGroup))
        //            AllSuccess = false;
        //    }

        //    return AllSuccess;
        //}

        public string GetString(uint StringID)
        {
            if (Strings.TryGetValue(StringID, out var Item))
                return Item.Value;
            return string.Empty;
        }

        public StringItem GetStringItem(uint StringID)
        {
            Strings.TryGetValue(StringID, out var Item);
            return Item;
        }

        public bool HasString(uint StringID)
        {
            return Strings.ContainsKey(StringID);
        }

        public void SetString(uint StringID, string Text, StringsFileType Type = StringsFileType.Strings)
        {
            if (Strings.TryGetValue(StringID, out var ExistingItem))
            {
                ExistingItem.Value = Text;
                ExistingItem.Type = Type;
            }
            else
            {
                Strings[StringID] = new StringItem
                {
                    ID = StringID,
                    Type = Type,
                    Value = Text
                };
            }
        }

        public bool RemoveString(uint StringID)
        {
            return Strings.Remove(StringID);
        }

        public int GetStringCount()
        {
            return Strings.Count;
        }

        public void Clear()
        {
            Strings.Clear();
        }

        public List<StringItem> GetAllStrings()
        {
            return Strings.Values.ToList();
        }

        public List<StringItem> GetStringsByType(StringsFileType Type)
        {
            return Strings.Values.Where(Item => Item.Type == Type).ToList();
        }

        private StringsFileType GetFileTypeEnum(string FileType)
        {
            switch (FileType.ToUpper())
            {
                case "STRINGS":
                    return StringsFileType.Strings;
                case "ILSTRINGS":
                    return StringsFileType.IL;
                case "DLSTRINGS":
                    return StringsFileType.DL;
                default:
                    return StringsFileType.Null;
            }
        }

        private string BuildStringsFilePath(string EspPath, Languages Language, string FileType)
        {
            string LangName = Enum.GetName(typeof(Languages), Language);
            string Directory = Path.GetDirectoryName(EspPath);
            string BaseName = Path.GetFileNameWithoutExtension(EspPath);
            string CapitalizedLanguage = CapitalizeFirstLetter(LangName);

            string StringsDirectory = Path.Combine(Directory, "Strings");
            string FileName = $"{BaseName}_{CapitalizedLanguage}.{FileType}";

            return Path.Combine(StringsDirectory, FileName);
        }

        private string CapitalizeFirstLetter(string Text)
        {
            if (string.IsNullOrEmpty(Text))
                return Text;

            return char.ToUpper(Text[0]) + Text.Substring(1).ToLower();
        }
    }
}