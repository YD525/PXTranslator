using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit;
using System;
using System.Runtime.InteropServices;
using NIM.UIManagement;
using NIM.SkyrimManagement;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NIM
{
    /// <summary>
    /// Interaction logic for CodeView.xaml
    /// </summary>
    public partial class CodeView : Window
    {
        private PhoenixGui _Owner;
        public ModFile ModRef = null;
        public CodeView(ModFile Mod,PhoenixGui Owner)
        {
            InitializeComponent();

            _Owner = Owner;

            this.Owner = _Owner;
            this.ModRef = Mod;

            _Owner.LocationChanged += OwnerMainWindow_LocationChanged;
            _Owner.SizeChanged += OwnerMainWindow_SizeChanged;
            _Owner.StateChanged += OwnerMainWindow_StateChanged;
            _Owner.Closed += OwnerMainWindow_Closed;
        }

        private void OwnerMainWindow_Closed(object Sender, EventArgs E)
        {
            MultiWindowController.CodeWin= null;
            this.Close();
        }
        private void UpdateFollowPosition()
        {
            double Gap = 8;

            this.Left = _Owner.Left + _Owner.ActualWidth + Gap;
            this.Top = _Owner.Top;
            this.Height = _Owner.ActualHeight;
        }
        private void RecordTracking_Loaded(object Sender, RoutedEventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_LocationChanged(object Sender, EventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_SizeChanged(object Sender, SizeChangedEventArgs E)
        {
            UpdateFollowPosition();
        }

        private void OwnerMainWindow_StateChanged(object Sender, EventArgs E)
        {
            if (_Owner.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                UpdateFollowPosition();
            }
        }
        private static class Win32
        {
            public static readonly IntPtr HWND_TOP = new IntPtr(0);

            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_SHOWWINDOW = 0x0040;

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(
                IntPtr hWnd,
                IntPtr hWndInsertAfter,
                int X, int Y, int cx, int cy,
                uint uFlags);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string GetName = "NIM" + ".IDERule.Lua.xshd";

            System.Reflection.Assembly Assembly = System.Reflection.Assembly.GetExecutingAssembly();

            using (System.IO.Stream Resource = Assembly.GetManifestResourceStream(GetName))
            {
                using (System.Xml.XmlTextReader Reader = new System.Xml.XmlTextReader(Resource))
                {
                    var Xshd = HighlightingLoader.LoadXshd(Reader);

                    TextEditor.SyntaxHighlighting = HighlightingLoader.Load(Xshd, HighlightingManager.Instance);
                }
            }

            UpdateFollowPosition();

            SetText(ModRef.PexReader.Code);
        }

        public void SyncCode(string SearchText = "")
        {
            SetText(ModRef.PexReader.Code);
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            _Owner.LocationChanged -= OwnerMainWindow_LocationChanged;
            _Owner.SizeChanged -= OwnerMainWindow_SizeChanged;
            _Owner.StateChanged -= OwnerMainWindow_StateChanged;
            _Owner.Closed -= OwnerMainWindow_Closed;
        }


        public bool IsLeftMouseDown = false;

        private void WinHead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IsLeftMouseDown = true;
            }

            if (IsLeftMouseDown)
            {
                try
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.DragMove();
                    }));

                    IsLeftMouseDown = false;
                }
                catch { }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchText NSearchText = new SearchText(TextEditor);
                NSearchText.Owner = this;
                NSearchText.Show();
            }
        }


        private void SetText(string Text)
        {
            this.Dispatcher.Invoke(() =>
            {
               TextEditor.WordWrap = false;
               TextEditor.Document = new TextDocument(Text);
            });
        }


        private CancellationTokenSource SearchCts = null;
        public async Task SelectLineFromIDEAsync(int LineId, string Value, CancellationToken CancellationToken = default, bool FocusEditor = false)
        {
            SearchCts?.Cancel();
            SearchCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var LocalCts = SearchCts;
            var Token = LocalCts.Token;

            try
            {
                string FullText = null;
                this.Dispatcher.Invoke(() =>
                {
                    FullText = TextEditor.Text; 
                });

                string SearchValue = "\"" + Value + "\"";
                int StartLine = LineId == 0 ? 1 : LineId;

                var SearchResult = await Task.Run(() =>
                {
                    int FoundLine = -1;
                    int FoundOffset = -1;
                    int FoundLength = SearchValue.Length;

                    using (var Reader = new System.IO.StringReader(FullText))
                    {
                        int CurrentLine = 1;
                        string LineText;
                        while ((LineText = Reader.ReadLine()) != null)
                        {
                            if (CurrentLine >= StartLine)
                            {
                                Token.ThrowIfCancellationRequested();
                                int Index = LineText.IndexOf(SearchValue, StringComparison.OrdinalIgnoreCase);
                                if (Index >= 0)
                                {
                                    FoundLine = CurrentLine;
                                    FoundOffset = Index;
                                    break;
                                }
                            }
                            CurrentLine++;
                        }
                    }

                    return (FoundLine, FoundOffset, FoundLength);
                }, Token);

                if (SearchResult.FoundLine != -1)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        Token.ThrowIfCancellationRequested();
                        var Editor = TextEditor;
                        var Doc = Editor.Document;
                        var Line = Doc.GetLineByNumber(SearchResult.FoundLine);
                        int Offset = Line.Offset + SearchResult.FoundOffset;
                        Editor.ScrollToLine(SearchResult.FoundLine);
                        Editor.Select(Offset, SearchResult.FoundLength);
                        Editor.CaretOffset = Offset + SearchResult.FoundLength;
                        if (FocusEditor)
                        {
                            Editor.Focus();
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
            finally
            {
                if (SearchCts == LocalCts)
                    SearchCts = null;
            }
        }
    }


}
