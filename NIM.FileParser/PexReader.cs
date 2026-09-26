using System.Collections.Generic;
using System.Linq;
using PexInterface;
using PhoenixEngine.Memory;
using static PexInterface.PexHeuristicAnalysis;

namespace ModFileParser
{
    public struct PexStrings
    {
        public FunctionBlock FunctionRef;

        public PexStringExtend PexStringItemRef;

        public int StringTableID;
        public int Score;
        public string Source;
        public string Translated;

        public bool IsVar;
        public int VarOffset;
    }
    public class PexReader: IScriptReader<Dictionary<string, PexStrings>>
    {
        //This section primarily records information to facilitate quick navigation to specific locations within the decompiled Pex documents, as identical strings may appear in the Pex code.
        public Dictionary<string, int> PexLinks = new Dictionary<string, int>();

        public string Code = "";

        private PexHeuristicAnalysis _Document = null;


        public int FileUniqueKey = 0;

        public string CurrentPath = string.Empty;

        private P_Dict<string, P_String> _LinkRef = null;

        public void Create(int FileUniqueKey, P_Dict<string, P_String> Link)
        {
            _Document = new PexHeuristicAnalysis();
            this.FileUniqueKey = FileUniqueKey;

            this._LinkRef = Link;
        }

        /// <summary>
        /// Load Pex File
        /// </summary>
        /// <param name="Style">Decide on the style for displaying the decompiled scripting language.</param>
        /// <param name="ShowAssembly">Decide whether to append assembly language comments after the decompiled code for cross-reference purposes.</param>
        /// <param name="Path"></param>
        public Dictionary<string, PexStrings> Load(CodeGenStyle Style,bool ShowAssembly, string Path)
        {
            Close();

            CurrentPath = Path;

            Dictionary<string, PexStrings> Data = null;

            _Document.Core.LoadPex(Path).ReadStrings().GetPsc(out this.Code,ShowAssembly,Style).AnalysisStrings();

            _Document.Core.GetStrings(out List<PexStringItem> Strings);

            Data = Strings.ToDictionary(
              x => x.UniqueKey,
              x => new PexStrings()
              {
                  FunctionRef = x.FunctionRef,
                  PexStringItemRef = x.PexStringItemRef,
                  StringTableID = x.StringTableID,
                  Score = x.Score,
                  Source = x.Original,
                  Translated = x.Translated,
                  IsVar = x.IsVar,
                  VarOffset = x.VarOffset
              }
              );

            return Data;
        }

        public bool Save(ref int ModifyCount)
        {
            _Document.Core.GetStrings(out List<PexStringItem> Strings);

            int TranslateCount = 0;

            for (int i = 0; i < Strings.Count; i++)
            {
                var GetTransData = _LinkRef[Strings[i].UniqueKey];

                if (GetTransData != null)
                {
                    Strings[i].Translated = GetTransData.String;

                    TranslateCount++;
                }
            }

            ModifyCount = TranslateCount;

            if (TranslateCount > 0)
            {
                try 
                {
                    _Document.Core.SavePex(this.CurrentPath, out int SaveState).Close();
                }
                catch 
                {
                    return false;
                }
            }

            return true;
        }

        public PexHeuristicAnalysis GetData()
        {
            return _Document;
        }

     
        public void Close()
        {
            _Document.Core.Close();
            this.Code = string.Empty;
            this.PexLinks.Clear();
        }
    }
}
