using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using PhoenixEngine.Common;
using PhoenixEngine.Memory;

namespace ModFileParser
{
    // ============================================================
    //  Data transfer objects  (unchanged from original)
    // ============================================================
    public class SubRecordData
    {
        public string Sig { get; set; }
        public byte[] Data { get; set; }
        public bool IsLocalized { get; set; }
        public uint StringID { get; set; }
        public string Content { get; set; }
        public int OccurrenceIndex { get; set; }
        public int Index { get; set; }
        public int? DSDIndex { get; set; }
    }

    public class EspRecordInfo
    {
        public IntPtr Handle { get; set; }
        public string Sig { get; set; }
        public uint FormID { get; set; }
        public uint Flags { get; set; }
        public int Index { get; set; }
        public string EditorID { get; set; }
        public List<SubRecordData> SubRecords { get; set; } = new List<SubRecordData>();

        public string GetUniqueKey() => $"{Sig}:{FormID}";
        public string GetFormIDHex() => $"{FormID:X8}";
        public string GetEditorID() => SubRecords.Find(s => s.Sig == "EDID")?.Content ?? "";
        public string GetDisplayName()
        {
            var full = SubRecords.Find(s => s.Sig == "FULL");
            return (!string.IsNullOrEmpty(full?.Content)) ? full.Content : GetEditorID();
        }
    }

    public class CharacterRecordInfo
    {
        public uint NpcFormID { get; set; }
        public string Name { get; set; } = "";
        public string EditorID { get; set; } = "";
        public string VoiceType { get; set; } = "";
        public int Gender { get; set; }   // 0=Unknown 1=Male 2=Female

        public List<uint> LinkedInfos { get; set; } = new List<uint>();
        public List<uint> LinkedFactions { get; set; } = new List<uint>();
        public List<uint> LinkedRaces { get; set; } = new List<uint>();
        public List<uint> LinkedVoiceTypes { get; set; } = new List<uint>();

        public string GenderString => Gender == 1 ? "Male" : Gender == 2 ? "Female" : "Unknown";
        public string FormIDHex => $"{NpcFormID:X8}";
    }

    // ============================================================
    //  Raw P/Invoke  –  every DLL function now takes handle first
    // ============================================================
    internal static class EspNative
    {
        private const string DllName = "EspReader.dll";

        // Lifecycle
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_CreateInstance();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void C_DestroyInstance(IntPtr handle);

        // Version  (no handle – global)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_GetVersion();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetVersionLength();

        // Filter
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void C_InitFilter(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SetSkyrimFilter(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetFilter(IntPtr handle, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SetFilter(IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string parentSig,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] childSigs,
            int childCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void C_ClearFilter(IntPtr handle);

        // IO
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int C_ReadEsp(IntPtr handle, [MarshalAs(UnmanagedType.LPWStr)] string espPath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool C_SaveEsp(IntPtr handle, IntPtr utf8Path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void C_Clear(IntPtr handle);

        // Field report
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_GetFieldReport(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetFieldReportLength(IntPtr handle);

        // Search
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_SearchBySig(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string parentSig,
            [MarshalAs(UnmanagedType.LPStr)] string childSig,
            out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeSearchResults(IntPtr arr, int count);

        // Record accessors  (record pointer only – no instance)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetRecordSig(IntPtr record, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetRecordFormID(IntPtr record);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_GetRecordEditorID(IntPtr record);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetRecordFlags(IntPtr record);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetRecordIndex(IntPtr record);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetSubRecordCount(IntPtr record);

        // SubRecord accessors
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr C_GetSubRecordData_Ptr(IntPtr record, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetOccurrenceIndex(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetIndex(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetDSDIndex(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetStringUtf8(IntPtr sub, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetSigUtf8(IntPtr sub, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool C_SubRecordData_IsLocalized(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_SubRecordData_GetStringID(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_SubRecordData_GetDataSize(IntPtr sub);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool C_SubRecordData_GetData(IntPtr sub, byte[] buffer, int bufferSize);

        // Modify
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern int C_ModifySubRecordByOffset(IntPtr handle, int isCell, int recordOffset, int subOffset, IntPtr newUtf8Data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern int C_ModifySubRecord(IntPtr handle, uint formID, IntPtr recordSig, IntPtr subSig, int occurrenceIndex, int globalIndex, IntPtr newUtf8Data);

        // Character tracker
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void C_ClearCharacterTracker(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterCount(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetCharacterFormID(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterGender(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterName(IntPtr handle, int index, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterEditorID(IntPtr handle, int index, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterVoiceType(IntPtr handle, int index, byte[] buffer, int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterLinkedInfoCount(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetCharacterLinkedInfo(IntPtr handle, int index, int linkIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterLinkedFactionCount(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetCharacterLinkedFaction(IntPtr handle, int index, int linkIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterLinkedRaceCount(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetCharacterLinkedRace(IntPtr handle, int index, int linkIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetCharacterLinkedVoiceTypeCount(IntPtr handle, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint C_GetCharacterLinkedVoiceType(IntPtr handle, int index, int linkIndex);

        // ============================================================
        //  Dialogue Context  –  NEW: based on offsets, not FormID
        // ============================================================
        [StructLayout(LayoutKind.Sequential)]
        public struct C_DialResponseNode
        {
            public uint ResponseID;
            public uint EmotionType;
            public int RecordOffset;
            public int SubOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct C_LinkDIAL
        {
            public int HasData;
            public C_DialResponseNode Head;
            public IntPtr Links;      // pointer to array of C_DialResponseNode
            public uint LinkCount;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern C_LinkDIAL C_GetDialContext(IntPtr handle, int RecordOffset, int SubOffset);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern C_LinkDIAL C_GetDialContextByDial(IntPtr handle, int RecordOffset);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void C_FreeDialContext(ref C_LinkDIAL context);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetTitleIndexByBookDesc(IntPtr Handle, int RecordOffset, int DescSubOffset);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int C_GetDescIndexByBookTitle(IntPtr Handle, int RecordOffset, int DescSubOffset);

        // ---- Helper methods for character records ----

        private static string GetCharacterUtf8(IntPtr Instance, int Index, Func<IntPtr, int, byte[], int, int> Getter)
        {
            int Len = Getter(Instance, Index, null, 0);
            if (Len <= 0) return string.Empty;

            byte[] Buffer = new byte[Len + 1];
            int ActualLen = Getter(Instance, Index, Buffer, Buffer.Length);

            int NullIndex = Array.IndexOf(Buffer, (byte)0, 0, ActualLen);
            if (NullIndex >= 0) ActualLen = NullIndex;

            return Encoding.UTF8.GetString(Buffer, 0, ActualLen);
        }

        public static List<CharacterRecordInfo> GetAllCharacters(IntPtr Instance)
        {
            int Count = C_GetCharacterCount(Instance);
            var Results = new List<CharacterRecordInfo>(Count);

            for (int i = 0; i < Count; i++)
            {
                var Record = new CharacterRecordInfo();
                Record.NpcFormID = C_GetCharacterFormID(Instance, i);
                Record.Name = GetCharacterUtf8(Instance, i, C_GetCharacterName);
                Record.EditorID = GetCharacterUtf8(Instance, i, C_GetCharacterEditorID);
                Record.VoiceType = GetCharacterUtf8(Instance, i, C_GetCharacterVoiceType);
                Record.Gender = C_GetCharacterGender(Instance, i);   // 0=Unknown 1=Male 2=Female

                int InfoCount = C_GetCharacterLinkedInfoCount(Instance, i);
                for (int j = 0; j < InfoCount; j++)
                    Record.LinkedInfos.Add(C_GetCharacterLinkedInfo(Instance, i, j));

                int FactionCount = C_GetCharacterLinkedFactionCount(Instance, i);
                for (int j = 0; j < FactionCount; j++)
                    Record.LinkedFactions.Add(C_GetCharacterLinkedFaction(Instance, i, j));

                int RaceCount = C_GetCharacterLinkedRaceCount(Instance, i);
                for (int j = 0; j < RaceCount; j++)
                    Record.LinkedRaces.Add(C_GetCharacterLinkedRace(Instance, i, j));

                int VoiceCount = C_GetCharacterLinkedVoiceTypeCount(Instance, i);
                for (int j = 0; j < VoiceCount; j++)
                    Record.LinkedVoiceTypes.Add(C_GetCharacterLinkedVoiceType(Instance, i, j));

                Results.Add(Record);
            }

            EspNative.C_ClearCharacterTracker(Instance);

            return Results;
        }

        private static string GetRecordSigUtf8(IntPtr RecordPtr)
        {
            byte[] Buffer = new byte[8];
            int Len = C_GetRecordSig(RecordPtr, Buffer, Buffer.Length);
            if (Len <= 0) return string.Empty;

            int NullIndex = Array.IndexOf(Buffer, (byte)0, 0, Len);
            if (NullIndex >= 0)
                Len = NullIndex;

            return Encoding.UTF8.GetString(Buffer, 0, Len);
        }

        public static string GetRecordEditorID(IntPtr RecordPtr)
        {
            if (RecordPtr == IntPtr.Zero)
                return string.Empty;

            IntPtr EditorIDPtr = C_GetRecordEditorID(RecordPtr);

            if (EditorIDPtr == IntPtr.Zero)
                return string.Empty;

            string Result = Marshal.PtrToStringAnsi(EditorIDPtr) ?? string.Empty;

            int NullIndex = Result.IndexOf('\0');
            if (NullIndex >= 0)
                Result = Result.Substring(0, NullIndex);

            return Result;
        }

        private static string GetSubRecordSigUtf8(IntPtr SubRecordPtr)
        {
            byte[] Buffer = new byte[8];
            int Len = C_SubRecordData_GetSigUtf8(SubRecordPtr, Buffer, Buffer.Length);
            if (Len <= 0) return string.Empty;

            int NullIndex = Array.IndexOf(Buffer, (byte)0, 0, Len);
            if (NullIndex >= 0)
                Len = NullIndex;

            return Encoding.UTF8.GetString(Buffer, 0, Len);
        }


        private static string GetSubRecordStringUtf8(IntPtr SubRecordPtr)
        {
            int Len = C_SubRecordData_GetStringUtf8(SubRecordPtr, null, 0);
            if (Len <= 0) return string.Empty;

            byte[] Buffer = new byte[Len + 1];
            int ActualLen = C_SubRecordData_GetStringUtf8(SubRecordPtr, Buffer, Buffer.Length);

            int NullIndex = Array.IndexOf(Buffer, (byte)0, 0, ActualLen);
            if (NullIndex >= 0)
                ActualLen = NullIndex;

            string NewStr = Encoding.UTF8.GetString(Buffer, 0, ActualLen);

            return string.Copy(NewStr);
        }

        public static List<EspRecordInfo> SearchBySig(IntPtr Instance, string ParentSig = "ALL", string ChildSig = "")
        {
            int Count;
            IntPtr ResultsPtr = C_SearchBySig(Instance, ParentSig, ChildSig, out Count);

            var Results = new List<EspRecordInfo>();

            if (ResultsPtr == IntPtr.Zero || Count == 0)
            {
                return Results;
            }

            try
            {
                for (int i = 0; i < Count; i++)
                {
                    IntPtr RecordPtr = Marshal.ReadIntPtr(ResultsPtr, i * IntPtr.Size);

                    if (RecordPtr == IntPtr.Zero)
                        continue;

                    var Record = new EspRecordInfo();
                    Record.Handle = RecordPtr;

                    Record.Sig = GetRecordSigUtf8(RecordPtr);

                    Record.FormID = C_GetRecordFormID(RecordPtr);
                    Record.EditorID = GetRecordEditorID(RecordPtr);
                    Record.Flags = C_GetRecordFlags(RecordPtr);
                    Record.Index = C_GetRecordIndex(RecordPtr);

                    int SubRecordCount = C_GetSubRecordCount(RecordPtr);

                    for (int j = 0; j < SubRecordCount; j++)
                    {
                        IntPtr SubRecordPtr = C_GetSubRecordData_Ptr(RecordPtr, j);
                        if (SubRecordPtr == IntPtr.Zero)
                            continue;

                        var SubRecord = new SubRecordData();

                        SubRecord.Sig = GetSubRecordSigUtf8(SubRecordPtr);
                        SubRecord.Content = GetSubRecordStringUtf8(SubRecordPtr);
                        SubRecord.IsLocalized = C_SubRecordData_IsLocalized(SubRecordPtr);
                        SubRecord.StringID = C_SubRecordData_GetStringID(SubRecordPtr);

                        SubRecord.OccurrenceIndex = C_SubRecordData_GetOccurrenceIndex(SubRecordPtr);
                        SubRecord.Index = C_SubRecordData_GetIndex(SubRecordPtr);

                        var GetDSDIndex = C_SubRecordData_GetDSDIndex(SubRecordPtr);

                        if (GetDSDIndex >= 0)
                        {
                            SubRecord.DSDIndex = GetDSDIndex;
                        }
                        else
                        {
                            SubRecord.DSDIndex = null;
                        }

                        int DataSize = C_SubRecordData_GetDataSize(SubRecordPtr);
                        if (DataSize > 0)
                        {
                            SubRecord.Data = new byte[DataSize];
                            C_SubRecordData_GetData(SubRecordPtr, SubRecord.Data, DataSize);
                        }
                        else
                        {
                            SubRecord.Data = new byte[0];
                        }

                        Record.SubRecords.Add(SubRecord);
                    }

                    Results.Add(Record);
                }

                return Results;
            }
            finally
            {
                FreeSearchResults(ResultsPtr, Count);
            }
        }

        public static IntPtr StringToUtf8Ptr(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }


        public static bool ModifySubRecordByOffset(IntPtr Instance, bool IsCell, int ParentIndex, int SubIndex, string NewUtf8Data)
        {
            IntPtr PtrNewData = IntPtr.Zero;
            try
            {
                PtrNewData = StringToUtf8Ptr(NewUtf8Data ?? "");
                if (IsCell)
                {
                    if (C_ModifySubRecordByOffset(Instance, 1, ParentIndex, SubIndex, PtrNewData)>0)
                    {
                        return true;
                    }
                }
                else
                {
                    if (C_ModifySubRecordByOffset(Instance, 0, ParentIndex, SubIndex, PtrNewData) > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (PtrNewData != IntPtr.Zero) Marshal.FreeHGlobal(PtrNewData);
            }
        }


        public static bool SaveEsp(IntPtr Instance, string OutputPath)
        {
            IntPtr Ptr = IntPtr.Zero;
            try
            {
                Ptr = StringToUtf8Ptr(OutputPath);
                return EspNative.C_SaveEsp(Instance, Ptr);
            }
            finally
            {
                if (Ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(Ptr);
            }
        }
        public static string GetFilterByStr(IntPtr instance)
        {
            int len = C_GetFilter(instance, null, 0);
            if (len <= 0) return string.Empty;

            byte[] buffer = new byte[len + 1];
            C_GetFilter(instance, buffer, buffer.Length);
            string raw = Encoding.UTF8.GetString(buffer, 0, len);

            return raw;
        }
        public static Dictionary<string, string[]> GetFilter(IntPtr instance)
        {
            int len = C_GetFilter(instance, null, 0);
            if (len <= 0) return new Dictionary<string, string[]>();

            byte[] buffer = new byte[len + 1];
            C_GetFilter(instance, buffer, buffer.Length);
            string raw = Encoding.UTF8.GetString(buffer, 0, len);

            var result = new Dictionary<string, string[]>();
            foreach (var entry in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = entry.IndexOf(':');
                if (colon < 0) continue;
                result[entry.Substring(0, colon)] = entry.Substring(colon + 1)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return result;
        }
    }


    public enum CharacterGender
    {
        Unknown,
        Male,
        Female
    }

    // ============================================================
    //  Managed Dialogue Context  –  updated to match new C++ API
    // ============================================================
    public class ManagedDialNode
    {
        public uint ResponseID { get; set; }
        public uint EmotionType { get; set; }
        public int RecordOffset { get; set; }
        public int SubOffset { get; set; }
    }

    public class ManagedDialContext
    {
        public ManagedDialNode Head { get; set; }
        public List<ManagedDialNode> Links { get; set; } = new List<ManagedDialNode>();
    }

    public class Character
    {
        public string Name { get; set; } = "";
        public CharacterGender Gender { get; set; }// 0=Unknown 1=Male 2=Female
        public string VoiceType { get; set; } = "";
    }

    public class RecordItem
    {
        public uint StringID = 0;      // StringsFile id
        public uint RealFormID = 0;
        public string FormID = "";
        public string EditorID = "";
        public string ParentSig = "";
        public string ChildSig = "";
        public string UniqueKey = "";
        public string String = "";
        public int? DSDIndex = null;

        public string Source
        {
            get 
            {
                return this.String;
            }
            set
            { 
                
            }
        }

        public int ParentIndex = 0;
        public int SubIndex = 0;
        public int OccurrenceIndex = 0;
        public bool IsModify = false;
    }

    public enum EmotionType : uint
    {
        Neutral = 0,
        Anger = 1,
        Disgust = 2,
        Fear = 3,
        Sad = 4,
        Happy = 5,
        Surprise = 6,
        Puzzled = 7,
        Unknown = 255
    }

    public static class EmotionTypeHelper
    {
        public static EmotionType FromRaw(uint RawValue)
        {
            if (RawValue <= (uint)EmotionType.Puzzled)
                return (EmotionType)RawValue;

            return EmotionType.Unknown;
        }

        public static string ToDisplayName(EmotionType Emotion)
        {
            switch (Emotion)
            {
                case EmotionType.Neutral: return "Neutral";
                case EmotionType.Anger: return "Anger";
                case EmotionType.Disgust: return "Disgust";
                case EmotionType.Fear: return "Fear";
                case EmotionType.Sad: return "Sad";
                case EmotionType.Happy: return "Happy";
                case EmotionType.Surprise: return "Surprise";
                case EmotionType.Puzzled: return "Puzzled";
                default: return "Unknown";
            }
        }

        public static string ToDisplayName(uint RawValue)
        {
            return ToDisplayName(FromRaw(RawValue));
        }
    }

    // ============================================================
    //  EspReader  –  managed wrapper, one instance per object
    //  Use pattern identical to PexReader.
    // ============================================================
    public class EspReader : IModReader<Dictionary<string, RecordItem>>, IDisposable
    {
        private IntPtr _Instance;
        private bool _Disposed = false;

        public static string Version { get; } = ReadDllVersion();

        public Dictionary<string, List<Character>> GameCharacters = new Dictionary<string, List<Character>>();

        public Dictionary<string, RecordItem> Records = new Dictionary<string, RecordItem>();

        public List<string> Types = new List<string>();


        public int FileUniqueKey = 0;
        public string CurrentPath = string.Empty;

        private P_Dict<string, P_String> _LinkRef = null; 

        // ── Constructor / destructor ──────────────────────────
        public void Create(int FileUniqueKey, P_Dict<string, P_String> Link)
        {
            this.FileUniqueKey = FileUniqueKey;
            _Instance = EspNative.C_CreateInstance();
            if (_Instance == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create EspInstance in native DLL.");

            this._LinkRef = Link;

            SetDefaultFilter();
        }

        public Dictionary<string, string[]> GetFilter()
        {
            EnsureNotDisposed();
            return EspNative.GetFilter(_Instance);
        }

        public string GetFilterByStr()
        {
            EnsureNotDisposed();
            string RichText = EspNative.GetFilterByStr(_Instance);
            RichText = RichText.Replace(";", ";\r\n");
            if (RichText.EndsWith("\r\n"))
            {
                RichText = RichText.Substring(0, RichText.Length - "\r\n".Length);
            }
            return RichText;

        }

        public Dictionary<string, string[]> ParseFilterString(string FilterStr)
        {
            var Result = new Dictionary<string, string[]>();
            if (string.IsNullOrEmpty(FilterStr)) return Result;

            foreach (var Entry in FilterStr.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Entry.Trim().Length > 0)
                {
                    int Column = Entry.IndexOf(':');

                    string Parent = Entry.Substring(0, Column).Trim();
                    string[] Children = Entry.Substring(Column + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    if (Parent.Length > 0 && Children.Length > 0)
                    {
                        Result[Parent] = Children;
                    }
                }
            }
            return Result;
        }

        public void ResetToSkyrimFilter()
        {
            EnsureNotDisposed();
            EspNative.C_InitFilter(_Instance);
            EspNative.C_SetSkyrimFilter(_Instance);
        }

        public void Dispose()
        {
            if (!_Disposed)
            {
                if (_Instance != IntPtr.Zero)
                {
                    EspNative.C_DestroyInstance(_Instance);
                    _Instance = IntPtr.Zero;
                }
                _Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~EspReader() { Dispose(); }

        private void EnsureNotDisposed()
        {
            if (_Disposed || _Instance == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(EspReader));
        }

        // ── Version ──────────────────────────────────────────
        private static string ReadDllVersion()
        {
            try
            {
                int len = EspNative.C_GetVersionLength();
                if (len <= 0) return "Unknown";
                IntPtr ptr = EspNative.C_GetVersion();
                return ptr == IntPtr.Zero ? "Unknown" : Marshal.PtrToStringAnsi(ptr, len);
            }
            catch { return "Error"; }
        }

        // ── Filter helpers ───────────────────────────────────
        public void SetDefaultFilter()
        {
            EnsureNotDisposed();
            EspNative.C_ClearFilter(_Instance);
            EspNative.C_SetSkyrimFilter(_Instance);
        }

        public void SetFilter(Dictionary<string, string[]> filterConfig)
        {
            EnsureNotDisposed();
            EspNative.C_ClearFilter(_Instance);
            foreach (var kvp in filterConfig)
                EspNative.C_SetFilter(_Instance, kvp.Key, kvp.Value, kvp.Value.Length);
        }

        public void ClearFilter()
        {
            EnsureNotDisposed();
            EspNative.C_ClearFilter(_Instance);
        }

        // ── IO ───────────────────────────────────────────────
        /// <summary>
        /// Load an ESP/ESM file.  Returns true on success.
        /// </summary>
        public Dictionary<string, RecordItem> Load(string Path)
        {
            Dictionary<string, RecordItem> Data = new Dictionary<string, RecordItem>();

            EnsureNotDisposed();

            if (File.Exists(Path))
            {
                Types.Clear();

                int State = EspNative.C_ReadEsp(_Instance, Path);

                if (State > 0)
                {
                    CurrentPath = Path;

                    foreach (var GetRecord in EspNative.SearchBySig(_Instance, "ALL"))
                    {
                        string ParentFormID = GetRecord.GetFormIDHex();
                        string ParentSig = GetRecord.Sig;

                        if (!Types.Contains(ParentSig))
                        {
                            Types.Add(ParentSig);
                        }
                    }

                    InitOnce = false;

                    Query();

                    return SelectSig("ALL");
                }
            }

            return new Dictionary<string, RecordItem>();
        }

        public bool Save(ref int ModifyCount)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                var Record = Records[Records.ElementAt(i).Key];

                var GetTransData = _LinkRef[Record.UniqueKey];
                if (GetTransData != null)
                {
                    if (GetTransData.String.Length > 0 && GetTransData.String != Record.String)
                    {
                        bool IsCell = false;

                        if (Record.ParentSig == "CELL")
                        {
                            IsCell = true;
                        }

                        if (P_Convert.ObjToLong(GetTransData.String) > 0)
                        {
                            continue;
                        }

                        if (EspNative.ModifySubRecordByOffset(_Instance, IsCell, Record.ParentIndex, Record.SubIndex, GetTransData.String))
                        {
                            ModifyCount++;
                        }
                    }
                }
            }

            string OutPutPath = CurrentPath + ".Temp";

            var State = EspNative.SaveEsp(_Instance, OutPutPath);

            if (File.Exists(CurrentPath + ".Temp") && File.Exists(CurrentPath))
            {
                File.Delete(CurrentPath);
                File.Move(CurrentPath + ".Temp", CurrentPath);
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Release parsed data without destroying the instance or its filter.
        /// </summary>
        public void Clear()
        {
            InitOnce = false;
            Types.Clear();
            OffsetRecordMap.Clear();
            Records.Clear();
            GameCharacters.Clear();
            EnsureNotDisposed();
            CurrentPath = string.Empty;
            EspNative.C_Clear(_Instance);
        }

        // ── Field report ─────────────────────────────────────
        public string GetFieldReport()
        {
            EnsureNotDisposed();
            int Len = EspNative.C_GetFieldReportLength(_Instance);
            if (Len <= 0) return "Validator not initialized";
            IntPtr Ptr = EspNative.C_GetFieldReport(_Instance);
            if (Ptr == IntPtr.Zero) return "Validator not initialized";
            byte[] Buffer = new byte[Len];
            Marshal.Copy(Ptr, Buffer, 0, Len);
            return Encoding.UTF8.GetString(Buffer);
        }

        public class BookInFoItem
        {
            public int RecordOffset = 0;

            public int TittleSubOffset = 0;
            public int ContentSubOffset = 0;

            public BookInFoItem(int RecordOffset, int TittleSubOffset, int ContentSubOffset)
            {
                this.RecordOffset = RecordOffset;
                this.TittleSubOffset = TittleSubOffset;
                this.ContentSubOffset = ContentSubOffset;
            }
        }
        public BookInFoItem GetBookInFo(RecordItem Item)
        {
            if (Item.ParentSig == "BOOK")
            {
                if (Item.ParentSig == "BOOK" && Item.ChildSig == "DESC")//Content
                {
                    return new BookInFoItem(
                        Item.ParentIndex,
                        EspNative.C_GetTitleIndexByBookDesc(_Instance, Item.ParentIndex, Item.SubIndex),
                        Item.SubIndex
                        );
                }
                else
                if (Item.ParentSig == "BOOK" && Item.ChildSig == "FULL")//Tittle
                {
                    return new BookInFoItem(
                       Item.ParentIndex,
                       Item.SubIndex,
                       EspNative.C_GetDescIndexByBookTitle(_Instance, Item.ParentIndex, Item.SubIndex)
                       );
                }
            }
            return null;
        }

        // ── Dialogue Context ──────────────────────────────────
        /// <summary>
        /// Get dialogue context for a specific dialogue node using record offset and sub-offset.
        /// </summary>
        /// <param name="IsCell">0 for main records, 1 for CELL records (CELL records have no dialogue context)</param>
        /// <param name="RecordOffset">Index of the record in the records array</param>
        /// <param name="SubOffset">Index of the sub-record (NAM1) within the record</param>
        /// <returns>ManagedDialContext or null if not found</returns>
        private ManagedDialContext GetDialContext(int RecordOffset, int SubOffset)
        {
            EnsureNotDisposed();
            if (_Instance == IntPtr.Zero) return null;

            EspNative.C_LinkDIAL rawContext = EspNative.C_GetDialContext(_Instance, RecordOffset, SubOffset);

            if (rawContext.HasData == 0) return null;

            try
            {
                var managedContext = new ManagedDialContext();

                managedContext.Head = ConvertNode(rawContext.Head);

                if (rawContext.LinkCount > 0 && rawContext.Links != IntPtr.Zero)
                {
                    int structSize = Marshal.SizeOf(typeof(EspNative.C_DialResponseNode));
                    for (int i = 0; i < rawContext.LinkCount; i++)
                    {
                        IntPtr elementPtr = new IntPtr(rawContext.Links.ToInt64() + (i * structSize));

                        var rawNode = (EspNative.C_DialResponseNode)Marshal.PtrToStructure(elementPtr, typeof(EspNative.C_DialResponseNode));

                        managedContext.Links.Add(ConvertNode(rawNode));
                    }
                }

                return managedContext;
            }
            finally
            {
                EspNative.C_FreeDialContext(ref rawContext);
            }
        }

        private ManagedDialContext GetDialContextByDial(int RecordOffset)
        {
            EnsureNotDisposed();
            if (_Instance == IntPtr.Zero) return null;

            EspNative.C_LinkDIAL rawContext = EspNative.C_GetDialContextByDial(_Instance, RecordOffset);

            if (rawContext.HasData == 0) return null;

            try
            {
                var managedContext = new ManagedDialContext();

                managedContext.Head = ConvertNode(rawContext.Head);

                if (rawContext.LinkCount > 0 && rawContext.Links != IntPtr.Zero)
                {
                    int structSize = Marshal.SizeOf(typeof(EspNative.C_DialResponseNode));
                    for (int i = 0; i < rawContext.LinkCount; i++)
                    {
                        IntPtr elementPtr = new IntPtr(rawContext.Links.ToInt64() + (i * structSize));

                        var rawNode = (EspNative.C_DialResponseNode)Marshal.PtrToStructure(elementPtr, typeof(EspNative.C_DialResponseNode));

                        managedContext.Links.Add(ConvertNode(rawNode));
                    }
                }

                return managedContext;
            }
            finally
            {
                EspNative.C_FreeDialContext(ref rawContext);
            }
        }

        /// <summary>
        /// Get dialogue context for a specific RecordItem.
        /// </summary>
        public ManagedDialContext GetDialContext(RecordItem RecordItem)
        {
            if (RecordItem == null) return null;

            if ((RecordItem.ParentSig == "INFO"))
            {
                return GetDialContext(RecordItem.ParentIndex, RecordItem.SubIndex);
            }
            else
            if ((RecordItem.ParentSig == "DIAL"))
            {
                return GetDialContextByDial(RecordItem.ParentIndex);
            }

            return null;
        }

        public string QueryEmotion(RecordItem Item)
        {
            if (Item.ParentSig == "INFO" || Item.ParentSig == "DIAL")
            {
                var GetInFo = this.GetDialContext(Item);
                if (GetInFo != null)
                {
                    foreach (var Get in GetInFo.Links)
                    {
                        if (Get.RecordOffset == Item.ParentIndex && Get.SubOffset == Item.SubIndex)
                        {
                            return EmotionTypeHelper.FromRaw(Get.EmotionType).ToString();
                        }
                    }
                }
            }

            return string.Empty;
        }

        private ManagedDialNode ConvertNode(EspNative.C_DialResponseNode rawNode)
        {
            return new ManagedDialNode
            {
                ResponseID = rawNode.ResponseID,
                EmotionType = rawNode.EmotionType,
                RecordOffset = rawNode.RecordOffset,
                SubOffset = rawNode.SubOffset
            };
        }
        public static string EncodeBase26(int Value)
        {
            if (Value < 0)
                throw new ArgumentOutOfRangeException(nameof(Value), "Value must be non-negative.");

            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            if (Value == 0)
                return "A";

            var NStringBuilder = new StringBuilder();
            while (Value > 0)
            {
                NStringBuilder.Insert(0, Chars[Value % 26]);
                Value /= 26;
            }
            return NStringBuilder.ToString();
        }

        public static int DecodeBase26(string Encoded)
        {
            if (string.IsNullOrEmpty(Encoded))
                throw new ArgumentException("Encoded string cannot be empty.");

            int Result = 0;
            foreach (char C in Encoded)
            {
                if (C < 'A' || C > 'Z')
                    throw new FormatException("Invalid character in encoded string.");
                Result = Result * 26 + (C - 'A');
            }
            return Result;
        }
        private long MakeOffsetKey(bool IsCell, int ParentIndex, int SubIndex)
        {
            long CellFlag = IsCell ? 1L : 0L;
            return (CellFlag << 62) | ((long)ParentIndex << 32) | (uint)SubIndex;
        }

        private bool InitOnce = false;

        private Dictionary<long, RecordItem> OffsetRecordMap = new Dictionary<long, RecordItem>();

        private object LockQuery = new object();

        public void Query()
        {
            lock (LockQuery)
            {
                if (!InitOnce)
                {
                    EnsureNotDisposed();

                    OffsetRecordMap.Clear();
                    Records.Clear();

                    Dictionary<uint, Character> InfoToCharacter = null;
                    List<CharacterRecordInfo> Characters = new List<CharacterRecordInfo>();

                    Characters.AddRange(EspNative.GetAllCharacters(_Instance));

                    InfoToCharacter = new Dictionary<uint, Character>(Characters.Sum(c => c.LinkedInfos.Count));

                    foreach (var Character in Characters)
                    {
                        var NCH = new Character
                        {
                            Name = Character.Name,
                            Gender = (CharacterGender)Character.Gender,
                            VoiceType = Character.VoiceType
                        };
                        foreach (var InfoFID in Character.LinkedInfos)
                        {
                            if (!InfoToCharacter.ContainsKey(InfoFID))
                                InfoToCharacter[InfoFID] = NCH;
                        }
                    }

                    Characters.Clear();

                    foreach (var GetRecord in EspNative.SearchBySig(_Instance, "ALL"))
                    {
                        uint RealFormID = GetRecord.FormID;
                        string ParentFormID = GetRecord.GetFormIDHex();
                        string ParentSig = GetRecord.Sig;

                        bool IsCell = false;

                        if (ParentSig == "CELL")
                        {
                            IsCell = true;
                        }

                        string ParentEditorID = GetRecord.EditorID;

                        foreach (var Sub in GetRecord.SubRecords)
                        {
                            var CalcKey = (IsCell ? "C|" : "") + EncodeBase26(this.FileUniqueKey) + "1" + EncodeBase26(GetRecord.Index) + "0" + EncodeBase26(Sub.Index);

                            string UniqueKey = CalcKey;

                            RecordItem NRecordItem = new RecordItem
                            {
                                RealFormID = RealFormID,
                                StringID = Sub.StringID,
                                FormID = ParentFormID,
                                EditorID = ParentEditorID,
                                ParentSig = ParentSig,
                                ChildSig = Sub.Sig,
                                UniqueKey = UniqueKey,
                                String = Sub.Content,
                                ParentIndex = GetRecord.Index,
                                SubIndex = Sub.Index,
                                OccurrenceIndex = Sub.OccurrenceIndex,
                                DSDIndex = Sub.DSDIndex
                            };

                            if (InfoToCharacter != null && InfoToCharacter.TryGetValue(RealFormID, out var MatchedChar))
                            {
                                if (GameCharacters.TryGetValue(UniqueKey, out var List))
                                    List.Add(MatchedChar);
                                else
                                    GameCharacters[UniqueKey] = new List<Character> { MatchedChar };
                            }

                            if (NRecordItem.String.Length > 0)
                            {
                                if (!Records.ContainsKey(NRecordItem.UniqueKey))
                                {
                                    Records.Add(NRecordItem.UniqueKey, NRecordItem);

                                    long OffsetKey = MakeOffsetKey(IsCell, NRecordItem.ParentIndex, NRecordItem.SubIndex);

                                    OffsetRecordMap[OffsetKey] = NRecordItem;
                                }
                                else
                                {
                                    var Get = NRecordItem.UniqueKey;
                                    throw new Exception($"Warning: Duplicate key detected: {NRecordItem.UniqueKey}");
                                }
                            }
                            else
                            {

                            }
                        }
                    }

                    InitOnce = true;
                }
            }
        }

        public Dictionary<string, RecordItem> SelectSig(string ParentSig)
        {
            if (ParentSig == "ALL") return this.Records;

            return Records.Where(Item => Item.Value.ParentSig == ParentSig)
                          .ToDictionary(Item => Item.Key, Item => Item.Value);
        }

        public RecordItem GetRecordItemByOffsets(bool IsCell, int ParentIndex, int SubIndex)
        {
            long OffsetKey = MakeOffsetKey(IsCell, ParentIndex, SubIndex);

            try
            {
                return OffsetRecordMap[OffsetKey];
            }
            catch { }

            return null;
        }

        public List<RecordItem> GetRecordsByParentIndex(int ParentIndex)
        {
            EnsureNotDisposed();
            Query();

            var Results = new List<RecordItem>();

            foreach (var Record in Records.Values)
            {
                if (Record.ParentIndex == ParentIndex)
                {
                    Results.Add(Record);
                }
            }

            return Results;
        }

        public void Close()
        {
            Clear();
        }
    }
}
