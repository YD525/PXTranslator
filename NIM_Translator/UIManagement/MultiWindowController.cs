using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NIM.ModParser;
using NIM.SkyrimManagement;
using PhoenixEngine.Unit;

namespace NIM.UIManagement
{
    public class MultiWindowController
    {
        public static RecordTracking TrackingWin = null;
        public static CodeView CodeWin = null;
        private static void CloseCodeWin()
        {
            if (CodeWin != null)
            {
                CodeWin.Close();
                CodeWin = null;
            }
        }

        private static void OpenCodeWin(ModFile Mod, PhoenixGui Win)
        {
            if (CodeWin == null)
            {
                CodeWin = new CodeView(Mod, Win);
                CodeWin.Show();
            }
            else
            {
                CodeWin.ModRef = Mod;
                CodeWin.Owner = Win;
            }
        }

        private static void CloseTrackingWin()
        {
            if (TrackingWin != null)
            {
                TrackingWin.Close();
                TrackingWin = null;
            }
        }

        private static void OpenTrackingWin(ModFile Mod, PhoenixGui Win)
        {
            if (TrackingWin == null)
            {
                TrackingWin = new RecordTracking(Mod, Win);
                TrackingWin.Show();
            }
            else
            {
                TrackingWin.ModRef = Mod;
                TrackingWin.Owner = Win;
            }
        }

        //If null is returned, grouping is performed based on default similarity.
        public static List<BaseUnit> CheckLinks(ModFile Mod, List<BaseUnit> TempUnits, BaseUnit Unit)
        {
            try
            {
                if (Unit.Type == "INFO" || Unit.Type == "DIAL" || Unit.Type == "BOOK")
                {
                    var Key = Unit.Key;

                    if (Mod.Type != GameFileType.ESP) return null;

                    RecordItem GetRecord = null;

                    if (Mod.EspReader.Records.ContainsKey(Key))
                    {
                        GetRecord = Mod.EspReader.Records[Key];
                    }

                    List<string> FindKeys = new List<string>();

                    if (GetRecord != null)
                    {
                        List<BaseUnit> Links = new List<BaseUnit>();

                        if (GetRecord.ParentSig == "INFO" || GetRecord.ParentSig == "DIAL")
                        {
                            ManagedDialContext DialogueLink = null;

                            if (Mod.DialNodeCache.ContainsKey(GetRecord.UniqueKey))
                            {
                                DialogueLink = Mod.DialNodeCache[GetRecord.UniqueKey];
                            }

                            List<ManagedDialNode> TempLinks = new List<ManagedDialNode>();

                            if (DialogueLink != null)
                            {
                                if (DialogueLink.Head != null) TempLinks.Add(DialogueLink.Head);
                                if (DialogueLink.Links != null) TempLinks.AddRange(DialogueLink.Links);
                            }

                            if (TempLinks.Count > 0)
                            {
                                foreach (var GetLink in TempLinks)
                                {
                                    if (GetLink.RecordOffset >= 0)
                                    {
                                        var GetLinkRecord = Mod.EspReader.GetRecordItemByOffsets(false, GetLink.RecordOffset, GetLink.SubOffset);
                                        if (GetLinkRecord != null)
                                            FindKeys.Add(GetLinkRecord.UniqueKey);
                                    }
                                }
                            }
                        }
                        else
                        if (GetRecord.ParentSig == "BOOK")
                        {
                            var GetBookInFo = Mod.EspReader.GetBookInFo(GetRecord);
                            if (GetBookInFo != null)
                            {
                                RecordItem Tittle = null;
                                RecordItem Content = null;

                                if (GetBookInFo.TittleSubOffset != -1)
                                    Tittle = Mod.EspReader.GetRecordItemByOffsets(false, GetBookInFo.RecordOffset, GetBookInFo.TittleSubOffset);

                                if (GetBookInFo.ContentSubOffset != -1)
                                    Content = Mod.EspReader.GetRecordItemByOffsets(false, GetBookInFo.RecordOffset, GetBookInFo.ContentSubOffset);

                                List<RecordItem> BookLinks = new List<RecordItem>();

                                if (Tittle != null) BookLinks.Add(Tittle);
                                if (Content != null) BookLinks.Add(Content);

                                foreach (var GetLink in BookLinks)
                                {
                                    FindKeys.Add(GetLink.UniqueKey);
                                }
                            }
                        }
                    }


                    var UnitDict = TempUnits.ToDictionary(U => U.Key);
                    List<BaseUnit> Units = new List<BaseUnit>();

                    foreach (var GetKey in FindKeys)
                    {
                        if (UnitDict.TryGetValue(GetKey, out var FoundUnit))
                        {
                            Units.Add(FoundUnit);
                        }
                    }

                    if (Units.Count > 0)
                        return Units;
                }

                return null;
            }
            catch 
            { 
                return null; 
            }
        }
        public static string LastSetAttachKey = "";

        public static object AttachLock = new object();

        private static CancellationTokenSource _DebounceTokenSource;
        public static void AttachMod(string SelectKey, PhoenixGui CurrentWin, ModFile Mod)
        {
            if (SelectKey == null || SelectKey == string.Empty)
            {
                return;
            }
            string AttachKey = SelectKey + "_" + Mod.FileName;

            if (LastSetAttachKey != AttachKey)
            {
                LastSetAttachKey = AttachKey;

                CancellationToken Token;
                lock (AttachLock)
                {
                    _DebounceTokenSource?.Cancel();
                    _DebounceTokenSource = new CancellationTokenSource();
                    Token = _DebounceTokenSource.Token;
                }

                CurrentWin.UI(() =>
                {
                    TrackingWin?.NpcListPanel.Children.Clear();
                    TrackingWin?.RelatedTextListPanel.Children.Clear();
                    TrackingWin?.DialogueListPanel.Children.Clear();
                });

                Task.Delay(200, Token).ContinueWith(T =>
                {
                    if (T.IsCanceled) return;

                    CurrentWin.UI(() =>
                    {
                        if (Token.IsCancellationRequested) return;

                        switch (Mod.Type)
                        {
                            case GameFileType.ESP:
                                {
                                    CloseCodeWin();
                                    OpenTrackingWin(Mod, CurrentWin);

                                    try
                                    {
                                        RecordItem GetRecord = null;

                                        if (Mod.EspReader.Records.ContainsKey(SelectKey))
                                        {
                                            GetRecord = Mod.EspReader.Records[SelectKey];
                                        }

                                        if (GetRecord != null)
                                        {
                                            if (Mod.EspReader.GameCharacters.ContainsKey(SelectKey))
                                            {
                                                if (Mod.EspReader.GameCharacters[SelectKey].Count > 0)
                                                    TrackingWin.LoadNpcRecord(GetRecord,
                                                        Mod.EspReader.GameCharacters[SelectKey][0].Name,
                                                        Mod.EspReader.GameCharacters[SelectKey][0].Gender.ToString());
                                            }
                                            else
                                            {
                                                TrackingWin.NpcListPanel.Children.Clear();
                                            }

                                            if (GetRecord.ParentSig == "INFO" || GetRecord.ParentSig == "DIAL")
                                            {
                                                var DialogueLink = Mod.EspReader.GetDialContext(GetRecord);
                                                List<ManagedDialNode> TempLinks = new List<ManagedDialNode>();

                                                if (DialogueLink != null)
                                                {
                                                    if (DialogueLink.Head != null) TempLinks.Add(DialogueLink.Head);
                                                    if (DialogueLink.Links != null) TempLinks.AddRange(DialogueLink.Links);
                                                }

                                                if (TempLinks.Count > 0)
                                                {
                                                    TrackingWin.LoadDialogueRecords(SelectKey, Mod, TempLinks, Token);
                                                }
                                                else
                                                {
                                                    TrackingWin.DialogueListPanel.Children.Clear();
                                                }
                                            }
                                            else if (GetRecord.ParentSig == "BOOK")
                                            {
                                                var GetBookInFo = Mod.EspReader.GetBookInFo(GetRecord);

                                                if (GetBookInFo != null)
                                                {
                                                    RecordItem Tittle = null;
                                                    RecordItem Content = null;

                                                    if (GetBookInFo.TittleSubOffset != -1)
                                                    {
                                                        Tittle = Mod.EspReader.GetRecordItemByOffsets(false, GetBookInFo.RecordOffset, GetBookInFo.TittleSubOffset);
                                                    }

                                                    if (GetBookInFo.ContentSubOffset != -1)
                                                    {
                                                        Content = Mod.EspReader.GetRecordItemByOffsets(false, GetBookInFo.RecordOffset, GetBookInFo.ContentSubOffset);
                                                    }

                                                    List<RecordItem> BookLinks = new List<RecordItem>();
                                                    if (Tittle != null) BookLinks.Add(Tittle);
                                                    if (Content != null) BookLinks.Add(Content);

                                                    if (BookLinks.Count > 0)
                                                    {
                                                        TrackingWin.LoadBookRecords(SelectKey, Mod, BookLinks, Token);
                                                    }
                                                    else
                                                    {
                                                        TrackingWin.DialogueListPanel.Children.Clear();
                                                    }
                                                }
                                            }

                                            List<RecordItem> Records = new List<RecordItem> { GetRecord };
                                            string Keyword = GetRecord.String;

                                            for (int i = 0; i < Mod.ListView.RealLines.Count; i++)
                                            {
                                                var Line = Mod.ListView.RealLines[i];
                                                if (Line.Key == SelectKey) continue;
                                                if (Line.RealSource.Length < Keyword.Length && Line.Source.Length < Keyword.Length) continue;

                                                if (Line.RealSource.Contains(Keyword) || Line.Source.Contains(Keyword))
                                                {
                                                    Records.Add(Mod.EspReader.Records[Line.Key]);
                                                }
                                            }

                                            if (Records.Count > 1)
                                            {
                                                TrackingWin.LoadRelatedTextRecords(SelectKey, Records, Token);
                                            }
                                            else
                                            {
                                                TrackingWin.RelatedTextListPanel.Children.Clear();
                                            }

                                            TrackingWin.UpdateAllSectionHeights();
                                            CurrentWin.Focus();
                                        }
                                    }
                                    catch { }
                                    break;
                                }
                            case GameFileType.PEX:
                                {
                                    CloseTrackingWin();
                                    OpenCodeWin(Mod, CurrentWin);

                                    var GetGrid = Mod.ListView.KeyToFakeGrid(SelectKey);
                                    string Text = GetGrid.RealSource;
                                    if (Mod.PexReader.PexLinks.ContainsKey(SelectKey) && Text == "")
                                        Text = GetGrid.Source;

                                    if (CodeWin.TextEditor.Text != Mod.PexReader.Code)
                                        CodeWin.TextEditor.Text = Mod.PexReader.Code;

                                    _ = CodeWin.SelectLineFromIDEAsync(Mod.PexReader.PexLinks[SelectKey], Text);

                                    CurrentWin.Focus();
                                    break;
                                }
                        }
                    });
                }, TaskScheduler.Default);
            }
        }

        public static void CloseMod(ModFile Mod)
        {
            if (TrackingWin != null)
            {
                if (TrackingWin.ModRef.Path == Mod.Path)
                {
                    TrackingWin.ModRef = null;
                    TrackingWin.Close();

                    TrackingWin = null;
                }
            }
            if (CodeWin != null)
            {
                if (CodeWin.ModRef.Path == Mod.Path)
                {
                    CodeWin.ModRef = null;
                    CodeWin.Close();

                    CodeWin = null;
                }
            }
        }

        public static void HideAll()
        {
            if (TrackingWin != null)
            {
                TrackingWin.Hide();
            }
            if (CodeWin != null)
            {
                CodeWin.Hide();
            }
        }

        public static void ShowAll()
        {
            if (TrackingWin != null)
            {
                TrackingWin.Show();
            }
            if (CodeWin != null)
            {
                CodeWin.Show();
            }
        }
    }
}
