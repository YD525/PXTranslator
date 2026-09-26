using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using PhoenixEngine;
using PhoenixEngine.ADO;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Linq;
using NIM.IDEManagement;
using NIM.ApplicationLayer;

namespace NIM
{
    public partial class DataBaseView : Window
    {
        private bool _isReadOnlyMode;

        /// <summary>Restricts the database tool to read-only statements and disables mutation controls.</summary>
        /// <param name="isReadOnly">Whether database mutations must be blocked.</param>
        public void SetReadOnlyMode(bool isReadOnly)
        {
            _isReadOnlyMode = isReadOnly;
            DeleteBtn.IsEnabled = !isReadOnly;
            DeleteBtn.Visibility = isReadOnly ? Visibility.Collapsed : Visibility.Visible;
            MainGrid.IsReadOnly = isReadOnly;
            Title = isReadOnly ? "Database Viewer (read-only)" : "Database Viewer";
        }
        private string _TableName;

        private Dictionary<int, long> _RowIds = new Dictionary<int, long>();

        public void QueryFirst(string Sql)
        {
            SqlOrder.Text = Sql;
            RunQuery(SqlOrder.Text);
        }
        public DataBaseView()
        {
            InitializeComponent();
        }

        // ── Update table name display ─────────────────────────────
        private void SetTableName(string Name)
        {
            _TableName = Name;
            TableNameBlock.Text = Name;
            TableNameRun.Text = Name;

            Title = $"Database Viewer - {Name}";
        }

        // ── Query button click ────────────────────────────────────
        private void QueryDataBase(object Sender, RoutedEventArgs E)
        {
            string Sql = SqlOrder.Text?.Trim();
            if (string.IsNullOrEmpty(Sql)) return;

            string Parsed = ParseTableName(Sql);
            if (!string.IsNullOrEmpty(Parsed))
                SetTableName(Parsed);

            RunQuery(Sql);
        }

        public string ExtractTableName(string Sql)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Sql))
                    return string.Empty;

                Sql = Sql.Trim();

                string Pattern = @"(?i)\b(?:FROM|INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+[`""\[]?(?<TableName>\w+)[`""\]]?";

                var Matches = Regex.Matches(Sql, Pattern, RegexOptions.Singleline);

                if (Matches.Count > 0)
                {
                    string TableName = Matches[Matches.Count - 1].Groups["TableName"].Value;
                    SetTableName(TableName);
                    return TableName;
                }

                SetTableName(string.Empty);
                return string.Empty;
            }
            catch
            {
                SetTableName(string.Empty);
                return string.Empty;
            }
        }

        private string LastQuery = "";
        private void RunQuery(string UserSql)
        {
            if (_isReadOnlyMode && !ApplicationLayer.PreviewDatabaseStatementGuard.IsReadOnly(UserSql))
            {
                SetStatus("Read-only mode accepts SELECT statements only.", false);
                return;
            }
            try
            {
                LastQuery = UserSql;
                ExtractTableName(UserSql);
                SetStatus("Querying...", true);
                _RowIds.Clear();

                string RewrittenSql = InjectRowid(UserSql);
                string SafeSQL = SQLSafeCodec.EncodeSQLValues(RewrittenSql);
                List<Dictionary<string, object>> Rows =
                    Phoenix.LocalDB.P_ExecuteQuery(SafeSQL);

                DataTable Table = ToDataTable(Rows);

                for (int I = 0; I < Table.Rows.Count; I++)
                {
                    if (Table.Columns.Contains("Rowid") &&
                        long.TryParse(Table.Rows[I]["Rowid"]?.ToString(), out long Rid))
                        _RowIds[I] = Rid;
                }

                if (!ColumnsMatch(Table))
                {
                    BuildColumns(Table);
                    MainGrid.BeginningEdit -= OnBeginningEdit;
                    MainGrid.CellEditEnding -= OnCellEditEnding;
                    MainGrid.BeginningEdit += OnBeginningEdit;
                    MainGrid.CellEditEnding += OnCellEditEnding;
                }

                MainGrid.ItemsSource = Table.DefaultView;

                RowCountRun.Text = Table.Rows.Count.ToString();
                ColCountRun.Text = (Table.Columns.Count - 1).ToString();
                SetStatus($"OK · {Table.Rows.Count} rows", true);
            }
            catch (Exception Ex)
            {
                SetStatus($"Error: {Ex.Message}", false);
            }
        }

        private bool ColumnsMatch(DataTable Table)
        {
            if (MainGrid.Columns.Count != Table.Columns.Count)
                return false;

            for (int I = 0; I < Table.Columns.Count; I++)
            {
                if (MainGrid.Columns[I].Header?.ToString() != Table.Columns[I].ColumnName)
                    return false;
            }

            return true;
        }

        private void RunQuery()
        {
            if (LastQuery.Length > 0)
            {
                RunQuery(LastQuery);
            }
        }

        private string InjectRowid(string Sql)
        {
            if (Regex.IsMatch(Sql, @"\browid\b", RegexOptions.IgnoreCase))
                return Sql;

            var Match = Regex.Match(Sql,
                @"^(\s*SELECT\s+)(.*?)(\s+FROM\s+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!Match.Success) return Sql;

            string Keyword = Match.Groups[1].Value;
            string Columns = Match.Groups[2].Value;
            string FromClause = Match.Groups[3].Value;
            string Rest = Sql.Substring(Match.Length);

            return $"{Keyword}Rowid, {Columns.Trim()}{FromClause}{Rest}";
        }

        private Dictionary<(int, string), string> _EditSnapshots
        = new Dictionary<(int, string), string>();

        private void OnBeginningEdit(object Sender, DataGridBeginningEditEventArgs E)
        {
            string ColName = E.Column.Header?.ToString();
            if (string.IsNullOrEmpty(ColName) || ColName == "Rowid") return;

            int RowIndex = E.Row.GetIndex();
            DataRowView Drv = E.Row.Item as DataRowView;
            if (Drv == null) return;

            string OldValue = Drv.Row[ColName]?.ToString() ?? "";
            _EditSnapshots[(RowIndex, ColName)] = OldValue;
        }
        private TextBox FindChildTextBox(DependencyObject Parent)
        {
            if (Parent == null) return null;
            int Count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(Parent);
            for (int I = 0; I < Count; I++)
            {
                var Child = System.Windows.Media.VisualTreeHelper.GetChild(Parent, I);
                if (Child is TextBox Tb) return Tb;
                var Found = FindChildTextBox(Child);
                if (Found != null) return Found;
            }
            return null;
        }

        private void OnCellEditEnding(object Sender, DataGridCellEditEndingEventArgs E)
        {
            if (E.EditAction != DataGridEditAction.Commit) return;

            string EditedColumn = E.Column.Header?.ToString();
            if (string.IsNullOrEmpty(EditedColumn) || EditedColumn == "Rowid") return;

            int RowIndex = E.Row.GetIndex();
            if (!_RowIds.TryGetValue(RowIndex, out long Rowid))
            {
                SetStatus("UPDATE skipped: rowid not found for this row", false);
                return;
            }

            string NewValue = "";
            if (E.EditingElement is TextBox Tb)
                NewValue = Tb.Text;
            else
                NewValue = FindChildTextBox(E.EditingElement)?.Text ?? "";

            // Use pre-cached snapshot, not DataRowView (which is already updated)
            string OldValue = "";
            _EditSnapshots.TryGetValue((RowIndex, EditedColumn), out OldValue);
            _EditSnapshots.Remove((RowIndex, EditedColumn));

            if (NewValue == OldValue) return;

            if (EditedColumn == "Source" || EditedColumn == "Result")
            {
                NewValue = SQLSafeCodec.Encode(NewValue);
            }

            string UpdateSql =
                $"UPDATE {Quote(_TableName)} " +
                $"SET {Quote(EditedColumn)} = {SqlVal(NewValue)} " +
                $"WHERE rowid = {Rowid};";

            try
            {
                Phoenix.LocalDB.P_ExecuteQuery(UpdateSql);
                SetStatus($"Updated [{EditedColumn}] = \"{NewValue}\"  (rowid={Rowid})", true);
            }
            catch (Exception Ex)
            {
                SetStatus($"UPDATE failed: {Ex.Message}", false);
            }
        }

        // ── Wrap identifier in double quotes ──────────────────────
        private string Quote(string Name) => $"\"{Name}\"";

        // ── Escape and quote a SQL string value ───────────────────
        private string SqlVal(string Value)
        {
            if (Value == null || Value == "(null)") return "NULL";
            return "'" + Value.Replace("'", "''") + "'";
        }

        // ── List<Dictionary<string,object>> → DataTable ───────────
        private DataTable ToDataTable(List<Dictionary<string, object>> Rows)
        {
            var Table = new DataTable();
            if (Rows == null || Rows.Count == 0) return Table;

            foreach (var Key in Rows[0].Keys)
                Table.Columns.Add(Key, typeof(string));

            foreach (var Row in Rows)
            {
                DataRow Dr = Table.NewRow();
                foreach (var Key in Row.Keys)
                {
                    var Value = Row[Key];

                    if (Value != null && (Key.Equals("Source")
                        ||
                        Key.Equals("Result")))
                    {
                        // Auto Decode
                        Value = SQLSafeCodec.Decode(Value.ToString());
                    }

                    Dr[Key] = Value == null ? "(null)" : Value.ToString();
                }

                Table.Rows.Add(Dr);
            }

            return Table;
        }

        // ── Build DataGrid columns dynamically ────────────────────
        private void BuildColumns(DataTable Table)
        {
            MainGrid.Columns.Clear();

            foreach (DataColumn Col in Table.Columns)
            {
                bool IsMultiLine = Col.ColumnName == "Source" || Col.ColumnName == "Result";

                DataGridColumn GridCol;

                if (IsMultiLine)
                {
                    // Template column — display TextBlock, edit multiline TextBox
                    var DisplayTemplate = new DataTemplate();
                    var DisplayFactory = new FrameworkElementFactory(typeof(TextBlock));
                    DisplayFactory.SetBinding(TextBlock.TextProperty, new Binding($"[{Col.ColumnName}]"));
                    DisplayFactory.SetValue(TextBlock.PaddingProperty, new Thickness(10, 0, 10, 0));
                    DisplayFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                    DisplayFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
                    DisplayFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(212, 212, 212)));
                    DisplayFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                    DisplayTemplate.VisualTree = DisplayFactory;

                    var EditTemplate = new DataTemplate();
                    var EditFactory = new FrameworkElementFactory(typeof(TextBox));
                    EditFactory.SetBinding(TextBox.TextProperty, new Binding($"[{Col.ColumnName}]")
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    });
                    EditFactory.SetValue(TextBox.AcceptsReturnProperty, true);
                    EditFactory.SetValue(TextBox.TextWrappingProperty, TextWrapping.Wrap);
                    EditFactory.SetValue(TextBox.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                    EditFactory.SetValue(TextBox.MinHeightProperty, 80d);
                    EditFactory.SetValue(TextBox.MaxHeightProperty, 200d);
                    EditFactory.SetValue(TextBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51)));
                    EditFactory.SetValue(TextBox.ForegroundProperty, new SolidColorBrush(Colors.White));
                    EditFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
                    EditFactory.SetValue(TextBox.CaretBrushProperty, new SolidColorBrush(Color.FromRgb(250, 227, 6)));
                    EditFactory.SetValue(TextBox.FontFamilyProperty, new FontFamily("Consolas"));
                    EditFactory.SetValue(TextBox.FontSizeProperty, 12d);
                    EditFactory.SetValue(TextBox.PaddingProperty, new Thickness(10, 6, 10, 6));
                    EditFactory.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top);
                    EditTemplate.VisualTree = EditFactory;

                    var TemplateCol = new DataGridTemplateColumn
                    {
                        Header = Col.ColumnName,
                        CellTemplate = DisplayTemplate,
                        CellEditingTemplate = EditTemplate,
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                        CanUserSort = false
                    };
                    GridCol = TemplateCol;
                }
                else
                {
                    DataGridLength ColWidth;

                    switch (Col.ColumnName)
                    {
                        case "Rowid":
                            ColWidth = new DataGridLength(60);
                            break;
                        case "To":
                            ColWidth = new DataGridLength(50);
                            break;
                        case "From":
                            ColWidth = new DataGridLength(50);
                            break;
                        case "Type":
                            ColWidth = new DataGridLength(60);
                            break;
                        case "Regex":
                            ColWidth = new DataGridLength(5);
                            break;
                        case "IgnoreCase":
                        case "ExactMatch":
                            ColWidth = new DataGridLength(50);
                            break;
                        case "TargetFileName":
                            ColWidth = new DataGridLength(60);
                            break;
                        case "Source":
                        case "Result":
                            ColWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
                            break;
                        default:
                            ColWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
                            break;
                    }

                    var TextCol = new DataGridTextColumn
                    {
                        Header = Col.ColumnName,
                        Binding = new Binding($"[{Col.ColumnName}]"),
                        Width = ColWidth,
                        ElementStyle = MakeTextBlockStyle(Col.ColumnName),
                        EditingElementStyle = MakeEditBoxStyle(),
                        CanUserSort = false
                    };
                    GridCol = TextCol;
                }

                MainGrid.Columns.Add(GridCol);
            }
        }

        // ── Cell display style (color by column name convention) ──
        private Style MakeTextBlockStyle(string ColumnName)
        {
            var Style = new Style(typeof(TextBlock));
            var Name = ColumnName.ToLower();

            SolidColorBrush Fg;
            //if (Name == "id" || Name.EndsWith("id") || Name.EndsWith("_id"))
            //    Fg = new SolidColorBrush(Color.FromRgb(250, 227, 6));   
            //else 
            if (Name.Contains("time") || Name.Contains("date") || Name.EndsWith("at"))
                Fg = new SolidColorBrush(Color.FromRgb(102, 102, 102));  // Gray — timestamp
            else
                Fg = new SolidColorBrush(Color.FromRgb(212, 212, 212));  // Default

            Style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Fg));
            Style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10, 0, 10, 0)));
            Style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            Style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));

            var NullTrigger = new DataTrigger { Binding = new Binding("."), Value = "(null)" };
            NullTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(85, 85, 85))));
            NullTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
            Style.Triggers.Add(NullTrigger);

            return Style;
        }

        // ── Cell editing TextBox style ────────────────────────────
        private Style MakeEditBoxStyle()
        {
            var Style = new Style(typeof(TextBox));
            Style.Setters.Add(new Setter(TextBox.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(51, 51, 51))));
            Style.Setters.Add(new Setter(TextBox.ForegroundProperty,
                new SolidColorBrush(Colors.White)));
            Style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
            Style.Setters.Add(new Setter(TextBox.CaretBrushProperty,
                new SolidColorBrush(Color.FromRgb(250, 227, 6))));
            Style.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily("Consolas")));
            Style.Setters.Add(new Setter(TextBox.FontSizeProperty, 12d));
            Style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(10, 0, 10, 0)));
            return Style;
        }

        // ── Extract table name from SQL (FROM keyword) ────────────
        private string ParseTableName(string Sql)
        {
            try
            {
                var Match = Regex.Match(Sql,
                    @"\bFROM\s+([`""\[]?[\w]+[`""\]]?)",
                    RegexOptions.IgnoreCase);
                if (!Match.Success) return null;

                // Strip any quoting characters
                return Match.Groups[1].Value.Trim('`', '"', '[', ']');
            }
            catch { return null; }
        }

        // ── Update status bar message ─────────────────────────────
        private void SetStatus(string Msg, bool Ok)
        {
            StatusMsg.Text = Msg;
            StatusMsg.Foreground = Ok
                ? new SolidColorBrush(Color.FromRgb(126, 200, 160))
                : new SolidColorBrush(Color.FromRgb(226, 75, 74));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _TableName = string.Empty;
            CurrentRowid = 0;
            _RowIds.Clear();
        }

        private void DeleteSelect(object sender, RoutedEventArgs e)
        {
            if (CurrentRowid != 0)
            {
                if (MessageBoxExtend.Show(this, "Msg", "The currently selected row will be deleted. Are you sure you want to continue?", PreviewDialogSeverity.Information, true))
                {
                    Phoenix.LocalDB.P_ExecuteQuery($"Delete From {_TableName} Where Rowid = {CurrentRowid}");
                    RunQuery();
                }
            }
        }
        public long CurrentRowid = 0;
        private void MainGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int Index = MainGrid.SelectedIndex;
            if (Index >= 0 && _RowIds.ContainsKey(Index))
                CurrentRowid = _RowIds[Index];
            else
                CurrentRowid = 0;
        }


        private void SelectNextRow()
        {
            if (MainGrid.Items.Count == 0)
                return;

            int CurrentIndex = MainGrid.SelectedIndex;

            if (CurrentIndex < 0)
                CurrentIndex = -1;

            int NextIndex = CurrentIndex + 1;
            if (NextIndex >= MainGrid.Items.Count)
                NextIndex = 0;

            MainGrid.SelectedIndex = NextIndex;

            MainGrid.ScrollIntoView(MainGrid.SelectedItem);
        }

        private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                var Cell = MainGrid.CurrentCell;
                if (Cell != null)
                {
                    if (MainGrid.IsEditing())
                        return;
                }

                SelectNextRow();
                e.Handled = true;
            }
        }

        private void ShowSqlOrder(object sender, MouseButtonEventArgs e)
        {
            SqlSetView.Visibility = Visibility.Visible;
            SqlIDE.Text = SqlOrder.Text;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string GetName = "NIM" + ".IDERule.SQL.xshd";

            System.Reflection.Assembly Assembly = System.Reflection.Assembly.GetExecutingAssembly();

            using (System.IO.Stream Resource = Assembly.GetManifestResourceStream(GetName))
            {
                using (System.Xml.XmlTextReader Reader = new System.Xml.XmlTextReader(Resource))
                {
                    var Xshd = HighlightingLoader.LoadXshd(Reader);

                    SqlIDE.SyntaxHighlighting = HighlightingLoader.Load(Xshd, HighlightingManager.Instance);
                }
            }

            SqlIDE.TextArea.TextEntering += SqlEditor_TextEntering;
            SqlIDE.TextArea.TextEntered += SqlEditor_TextEntered;
        }

        #region Completion

        public CompletionWindow Completion;

        private static readonly string[] SqlKeywords =
        {
            "Select", "From", "Where", "Insert", "Into", "Values",
            "Update", "Set", "Delete", "Join", "Left Join", "Right Join",
            "Inner Join", "On", "Group By", "Order By", "Having",
            "Distinct", "As", "And", "Or", "Not", "Null", "Is Null",
            "Is Not Null", "Like", "In", "Between", "Exists", "Union",
            "Create", "Table", "Drop", "Alter", "Index", "View",
            "Begin", "Commit", "Rollback", "Transaction",
            "Count", "Sum", "Avg", "Min", "Max", "Coalesce",
            "Case", "When", "Then", "End", "Limit", "Offset"
        };

        private static readonly string[] BracketKeywords =
        {
            "[Rowid]","[From]", "[To]","[Type]", "[Source]", "[Result]","[TargetFileName]","[ExactMatch]","[IgnoreCase]","[FileUniqueKey]","[Key]"
        };

        private static readonly Dictionary<string, string[]> TableColumns = new Dictionary<string, string[]>
        {
            ["AdvancedDictionary"] = new[] { "Rowid", "TargetFileName", "Type", "Source", "Result", "ExactMatch", "IgnoreCase" }
        };

        private static readonly string[] TableNames =
        {
            "AdvancedDictionary", "CloudTranslation", "LocalTranslation"
        };

        private ImageSource GetBracketIcon() => CreateCircleIcon(Colors.Orange);
        private ImageSource GetValueIcon() => CreateCircleIcon(Color.FromRgb(155, 89, 182));
        private ImageSource GetTableNameIcon() => CreateCircleIcon(Color.FromRgb(52, 152, 219));


        private void ShowBracketCompletion()
        {
            Completion?.Close();
            Completion = new CompletionWindow(SqlIDE.TextArea);

            int Offset = SqlIDE.TextArea.Caret.Offset;
            var Doc = SqlIDE.Document;
            int Start = Offset - 1;
            Completion.StartOffset = (Start >= 0 && Doc.GetCharAt(Start) == '[') ? Start : Offset;

            StyleCompletionWindow(Completion);
            AttachCompletionBehavior(Completion);

            foreach (string Kw in BracketKeywords)
                Completion.CompletionList.CompletionData.Add(
                    new MyCompletionData(Kw, $"Escaped Identifier: {Kw}", GetBracketIcon()));

            Completion.Show();
            Completion.Closed += (S, E) => Completion = null;
        }

        private void ShowValueCompletion()
        {
            var Doc = SqlIDE.Document;
            int Offset = SqlIDE.TextArea.Caret.Offset;

            int Pos = Offset - 1;
            while (Pos > 0 && Doc.GetCharAt(Pos) != '=') Pos--;
            if (Pos <= 0) return;

            int EqPos = Pos - 1;
            while (EqPos > 0 && Doc.GetCharAt(EqPos) == ' ') EqPos--;

            if (EqPos < 0 || Doc.GetCharAt(EqPos) != ']') return;
            int BracketEnd = EqPos;
            int BracketStart = BracketEnd - 1;
            while (BracketStart > 0 && Doc.GetCharAt(BracketStart) != '[') BracketStart--;
            if (BracketStart < 0) return;

            string FieldName = Doc.GetText(BracketStart + 1, BracketEnd - BracketStart - 1);
            if (!BracketDefaults.TryGetValue(FieldName, out Func<string> GetDefault)) return;

            string DefaultValue = GetDefault();

            Completion?.Close();
            Completion = new CompletionWindow(SqlIDE.TextArea);
            Completion.StartOffset = Offset;

            StyleCompletionWindow(Completion);
            AttachCompletionBehavior(Completion);

            Completion.CompletionList.CompletionData.Add(
                new MyCompletionData(DefaultValue, $"Current value for [{FieldName}]", GetValueIcon()));

            Completion.Show();
            Completion.Closed += (S, E) => Completion = null;
        }


        private static readonly Dictionary<string, Func<string>> BracketDefaults = new Dictionary<string, Func<string>>
        {
            ["From"] = () => ((int)NIMApp.WorkWin.ActiveTab.Mod.P_Translator.From).ToString(),
            ["To"] = () => ((int)NIMApp.WorkWin.ActiveTab.Mod.P_Translator.To).ToString(),
        };

        private string[] ResolveColumns(string TableOrAlias)
        {
            string Key = TableColumns.Keys.FirstOrDefault(K =>
                K.Equals(TableOrAlias, StringComparison.OrdinalIgnoreCase));
            return Key != null ? TableColumns[Key] : null;
        }

        private string GetWordBefore(TextArea TextArea, int Skip = 0)
        {
            var Doc = TextArea.Document;
            int Pos = TextArea.Caret.Offset - Skip;
            int Start = Pos;

            while (Start > 0 && (char.IsLetterOrDigit(Doc.GetCharAt(Start - 1)) || Doc.GetCharAt(Start - 1) == '_'))
                Start--;

            return Start < Pos ? Doc.GetText(Start, Pos - Start) : string.Empty;
        }

        private void ShowKeywordCompletion()
        {
            Completion = new CompletionWindow(SqlIDE.TextArea);

            int Offset = SqlIDE.TextArea.Caret.Offset;
            var Doc = SqlIDE.Document;
            int Start = Offset;
            while (Start > 0 && (char.IsLetterOrDigit(Doc.GetCharAt(Start - 1)) || Doc.GetCharAt(Start - 1) == '_'))
                Start--;
            Completion.StartOffset = Start;

            StyleCompletionWindow(Completion);
            AttachCompletionBehavior(Completion); 

            var Data = Completion.CompletionList.CompletionData;
            foreach (string Kw in SqlKeywords)
                Data.Add(new MyCompletionData(Kw, $"SQL Keyword: {Kw}", GetKeywordIcon()));
            foreach (string Tbl in TableNames)
                Data.Add(new MyCompletionData(Tbl, $"Table: {Tbl}", GetTableIcon()));

            Completion.Show();
            Completion.Closed += (S, E) => Completion = null;
        }
        private void ShowMemberCompletion()
        {
            string Word = GetWordBefore(SqlIDE.TextArea, 1);
            string[] Columns = ResolveColumns(Word);

            if (Columns == null || Columns.Length == 0) return;

            Completion?.Close();
            Completion = new CompletionWindow(SqlIDE.TextArea);
            StyleCompletionWindow(Completion);

            foreach (string Col in Columns)
                Completion.CompletionList.CompletionData.Add(
                    new MyCompletionData(Col, $"Column: {Col}\nTable: {Word}", GetColumnIcon()));

            Completion.Show();
            Completion.Closed += (S, E) => Completion = null;
        }

        private void SqlEditor_TextEntering(object Sender, TextCompositionEventArgs E)
        {
            if (Completion == null || E.Text.Length == 0) return;
            char C = E.Text[0];

            if (E.Text == "\n" || E.Text == "\r")
            {
                Completion.Close();
            }

            if (char.IsWhiteSpace(C) || IsSqlSeparator(C))
            {
                Completion?.Close();
            }
        }

        private static bool IsSqlSeparator(char C) =>
    char.IsWhiteSpace(C) ||
    (char.IsPunctuation(C) && C != '_') ||
    "()[]{}=,;".Contains(C);


        private void AttachCompletionBehavior(CompletionWindow Win)
        {
            Win.CloseAutomatically = false;
            Win.CloseWhenCaretAtBeginning = false;
            Win.PreviewKeyDown += (S, E) =>
            {
                if (E.Key == Key.Space || E.Key == Key.Enter)
                {
                    Win.Close();
                    E.Handled = false;
                }
                else if (E.Key == Key.Tab)
                {
                    var ListBox = Win.CompletionList.ListBox;
                    if (ListBox.SelectedItem is ICompletionData Item)
                    {
                        Item.Complete(
                            SqlIDE.TextArea,
                            new AnchorSegment(
                                SqlIDE.Document,
                                Win.StartOffset,
                                SqlIDE.TextArea.Caret.Offset - Win.StartOffset),
                            E);
                    }
                    Win.Close();
                    E.Handled = true;
                }
            };
        }
        private void SqlEditor_TextEntered(object Sender, TextCompositionEventArgs E)
        {
            //if (E.Text == ".")
            //{
            //    ShowMemberCompletion();
            //    return;
            //}

            if (E.Text == "[")
            {
                ShowBracketCompletion();
                return;
            }

            if (E.Text == "=")
            {
                ShowValueCompletion();
                return;
            }

            if (E.Text.Length == 1 && (char.IsLetter(E.Text[0]) || E.Text[0] == '_'))
            {
                if (Completion == null)
                    ShowKeywordCompletion();
            }
        }

        #endregion

        private void LayerGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SqlSetView.Visibility = Visibility.Collapsed;
        }

        private void Apply(object sender, RoutedEventArgs e)
        {
            SqlOrder.Text = SqlIDE.Text;
            SqlSetView.Visibility = Visibility.Collapsed;
            SqlOrderCopy.Content = SqlIDE.Text.Replace("\r\n", " ");
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            SqlSetView.Visibility = Visibility.Collapsed;
        }



        private void StyleCompletionWindow(CompletionWindow win)
        {
            win.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            win.Foreground = Brushes.White;
            win.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            win.BorderThickness = new Thickness(1);
            win.MaxHeight = 220;
            win.CloseWhenCaretAtBeginning = true;
        }
        private ImageSource GetKeywordIcon() => CreateCircleIcon(Colors.CornflowerBlue);
        private ImageSource GetTableIcon() => CreateCircleIcon(Colors.MediumSeaGreen);
        private ImageSource GetColumnIcon() => CreateCircleIcon(Colors.Gold);

        private ImageSource CreateCircleIcon(Color color)
        {
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(
                new SolidColorBrush(color),
                null,
                new EllipseGeometry(new Point(8, 8), 6, 6)));
            return new DrawingImage(dg);
        }

        private void Clear(object sender, RoutedEventArgs e)
        {
            SqlIDE.Text = "";
            SqlOrderCopy.Content = "";
            SqlOrder.Text = "";
        }
    }

    public static class DataGridExtensions
    {
        public static bool IsEditing(this DataGrid Grid)
        {
            var Row = (DataGridRow)Grid.ItemContainerGenerator.ContainerFromItem(Grid.SelectedItem);
            if (Row != null && Row.IsEditing)
            {
                return true;
            }
            return false;
        }
    }
}
