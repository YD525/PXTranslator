using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PhoenixEngine.Memory;

namespace ModFileParser
{
    public struct XmlStrings
    {
        public string EDID;
        public string REC;
        public string RECID;
        public string Source;
        public string Translated;
        public string Dest;
    }
    public class XmlReader: IModReader<Dictionary<string, XmlStrings>>
    {
        /// <summary>
        /// Gets or sets whether invalid XML is reported to the caller instead of the legacy modal boundary.
        /// </summary>
        public bool ThrowOnInvalidFormat { get; set; }

        private XDocument _Document = null;

        public int FileUniqueKey = 0;

        public string CurrentPath = string.Empty;

        private P_Dict<string, P_String> _LinkRef = null;
        public void Create(int FileUniqueKey, P_Dict<string, P_String> Link)
        {
            this.FileUniqueKey = FileUniqueKey;
            this._LinkRef = Link;
        }

        public Dictionary<string,XmlStrings> Load(string Path)
        {
            Close();

            CurrentPath = Path;

            Dictionary<string, XmlStrings> Data = null;
           
            XDocument Doc = XDocument.Load(Path);
            _Document = Doc;
            try
            {
                Data = Doc.Descendants("String").ToDictionary(
                x => $"{FileUniqueKey}_{(string)x.Element("REC")?.Attribute("id")}_{(string)x.Element("EDID")}_{(string)x.Element("REC")}",
                x => new XmlStrings
                {
                    EDID = (string)x.Element("EDID"),
                    REC = (string)x.Element("REC"),
                    RECID = x.Element("REC")?.Attribute("id")?.Value,
                    Source = (string)x.Element("Source"),
                    Translated = _LinkRef[$"{FileUniqueKey}_{(string)x.Element("REC")?.Attribute("id")}_{(string)x.Element("EDID")}_{(string)x.Element("REC")}"].String,
                    Dest = (string)x.Element("Dest")
                }
                );
            }
            catch (System.Exception exception)
            {
                if (ThrowOnInvalidFormat)
                {
                    throw new System.IO.InvalidDataException(
                        "The XML translation format is unsupported.",
                        exception);
                }
            }

            return Data;
        }

        public XDocument GetData()
        {
            return _Document;
        }

        public void Close()
        {
            _Document = null;
        }

        public bool Save(ref int ModifyCount)
        {
            foreach (var StringNode in _Document.Descendants("String"))
            {
                var EditorID = (string)StringNode.Element("EDID");
                var Rec = (string)StringNode.Element("REC");
                var DestNode = StringNode.Element("Dest");
                int RECID = 0;

                if ((StringNode.Element("REC")?.Attribute("id")) != null)
                {
                    RECID = int.TryParse(StringNode.Element("REC")?.Attribute("id")?.Value,out var ID) ? ID : -1;
                }

                if (EditorID != null && Rec != null)
                {
                    string Key = this.FileUniqueKey + "_" + RECID + "_" + EditorID + "_" + Rec;
                    
                    var Link = _LinkRef[Key];

                    if (Link != null)
                    {
                        DestNode.Value = Link.String;
                        ModifyCount++;
                    }
                }
            }

            bool NoError = true;

            try 
            { 
                _Document.Save(CurrentPath);
            }
            catch
            {
                NoError = false;
            }

            Close();

            return NoError;
        }
    }
}
