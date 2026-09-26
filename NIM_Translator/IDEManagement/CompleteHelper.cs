using System;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Media;
using PhoenixEngine.Engine.ADO;
using PhoenixEngine.Language;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using System.Linq;
using NIM.UIManagement;

namespace NIM.IDEManagement
{
    public class WordCompletionManager : IDisposable
    {
        private readonly TextEditor _Editor;
        private CompletionWindow _Completion;
        private string _LastPrefix = "";

        public bool IsCompletionActive => _Completion != null && _Completion.IsVisible;

        public WordCompletionManager(TextEditor Editor)
        {
            _Editor = Editor ?? throw new ArgumentNullException(nameof(Editor));
            _Editor.TextArea.TextEntered += OnTextEntered;
            _Editor.TextArea.TextEntering += OnTextEntering;
            _Editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object Sender, KeyEventArgs E)
        {
            if (!PhoenixApp.SelfSetting.WordCompletion) return;
            if (E.Key == Key.Back)
            {
                _LastPrefix = null;
                _Editor.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(TryShowOrUpdateCompletion));
            }
        }

        private void CommitCompletion(KeyEventArgs E)
        {
            if (_Completion == null) return;
            var ListBox = _Completion.CompletionList.ListBox;
            if (ListBox.SelectedItem is ICompletionData Item)
            {
                Item.Complete(
                    _Editor.TextArea,
                    new AnchorSegment(
                        _Editor.Document,
                        _Completion.StartOffset,
                        _Editor.TextArea.Caret.Offset - _Completion.StartOffset),
                    E);
            }
            _Completion.Close();
        }

        private void OnTextEntered(object Sender, TextCompositionEventArgs E)
        {
            if (!PhoenixApp.SelfSetting.WordCompletion) return;
            if (string.IsNullOrEmpty(E.Text)) return;
            char C = E.Text[0];

            if (IsSeparator(C))
            {
                _Completion?.Close();
                return;
            }

            TryShowOrUpdateCompletion();
        }

        private void OnTextEntering(object Sender, TextCompositionEventArgs E)
        {
            if (!PhoenixApp.SelfSetting.WordCompletion) return;
            if (_Completion == null || E.Text.Length == 0) return;
            char C = E.Text[0];
            if (C == ' ' || C == '\t')
                _Completion.CompletionList.RequestInsertion(E);

            if (E.Text == "\n" || E.Text == "\r")
            {
                 _Completion.Close(); 
            }

            if (char.IsWhiteSpace(C) || IsSeparator(C))
            {
                _Completion?.Close();
            }
        }

        private static bool IsSeparator(char C) =>
       char.IsWhiteSpace(C) ||
       (char.IsPunctuation(C) && C != '\'') || 
       "()[]{}!?,.。！?；;：:".Contains(C);

        private void TryShowOrUpdateCompletion()
        {
            var Completer = GetActiveCompleter();
            if (Completer == null) return;

            string CurrentText = _Editor.Document.GetText(0, _Editor.TextArea.Caret.Offset);
            List<string> Words = Completer.Query(CurrentText);

            if (Words == null || Words.Count == 0)
            {
                _Completion?.Close();
                _LastPrefix = "";
                return;
            }

            string Prefix = GetCurrentPrefix().ToLowerInvariant();

            if (_LastPrefix != null && Prefix == _LastPrefix && _Completion != null)
                return;

            _LastPrefix = Prefix;

            if (_Completion != null)
            {
                _Completion.Closed -= OnCompletionClosed;
                _Completion.Close();
                _Completion = null;
            }

            int Offset = _Editor.TextArea.Caret.Offset;
            int Start = Offset - Prefix.Length;

            _Completion = new CompletionWindow(_Editor.TextArea);
            _Completion.StartOffset = Start >= 0 ? Start : Offset;

            _Completion.CloseAutomatically = false; 
            _Completion.CloseWhenCaretAtBeginning = false;
            _Completion.PreviewKeyDown += (s, e) =>
            {
                if (!PhoenixApp.SelfSetting.WordCompletion) return;
                if (e.Key == Key.Space || e.Key == Key.Enter)
                {
                    _Completion.Close();
                    e.Handled = false;
                }
                else 
                if (e.Key == Key.Tab)
                {
                    CommitCompletion(null);
                    e.Handled = true;
                }
            };

            StyleCompletionWindow(_Completion);

            foreach (string Word in Words)
                _Completion.CompletionList.CompletionData.Add(
                    new MyCompletionData(Word, $"Word: {Word}", GetWordIcon()));

            _Completion.Closed += OnCompletionClosed;
            _Completion.Show();
            _Completion.CompletionList.ListBox.SelectedIndex = 0;
        }

        private void OnCompletionClosed(object S, EventArgs Ev)
        {
            _Completion = null;
            _LastPrefix = "";
        }

        private string GetCurrentPrefix()
        {
            var Doc = _Editor.Document;
            int Offset = _Editor.TextArea.Caret.Offset;
            int Start = Offset;

            while (Start > 0)
            {
                char Prev = Doc.GetCharAt(Start - 1);
                if (char.IsLetterOrDigit(Prev) || Prev == '_' || Prev == '\'')
                    Start--;
                else
                    break;
            }

            return Start < Offset ? Doc.GetText(Start, Offset - Start) : string.Empty;
        }

        private WordAutoComplete GetActiveCompleter() => PhoenixApp.WordCompleter;

        private static void StyleCompletionWindow(CompletionWindow Win)
        {
            Win.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            Win.Foreground = Brushes.White;
            Win.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            Win.BorderThickness = new System.Windows.Thickness(1);
            Win.MaxHeight = 220;
            Win.CloseWhenCaretAtBeginning = true;
        }

        private static ImageSource GetWordIcon() =>
            CreateCircleIcon(Color.FromRgb(80, 200, 140));

        private static ImageSource CreateCircleIcon(Color Color)
        {
            var Dg = new DrawingGroup();
            Dg.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color), null,
                new EllipseGeometry(new System.Windows.Point(8, 8), 6, 6)));
            return new DrawingImage(Dg);
        }

        public void Dispose()
        {
            _Editor.TextArea.TextEntered -= OnTextEntered;
            _Editor.TextArea.TextEntering -= OnTextEntering;
            _Editor.TextArea.PreviewKeyDown -= OnPreviewKeyDown;
            _Completion?.Close();
        }
    }

    public class MyCompletionData : ICompletionData
    {
        private readonly string _Description;
        private readonly ImageSource _Image;

        public MyCompletionData(string Text, string Description = null, ImageSource Image = null)
        {
            this.Text = Text;
            _Description = Description;
            _Image = Image;
        }

        public ImageSource Image => _Image;
        public string Text { get; private set; }
        public object Content => Text;
        public object Description => _Description ?? Text;
        public double Priority => 0;

        public void Complete(TextArea TextArea, ISegment CompletionSegment, EventArgs E)
        {
            TextArea.Document.Replace(CompletionSegment, Text);
        }
    }


    public class Completer
    {
        public TranslateView View = null;
        public Completer(TranslateView View)
        { 
           this.View = View;
        }
        public void CheckLang(Languages Lang)
        {
            if (WordAutoComplete.WordCompleters.ContainsKey(Lang))
            {
                PhoenixApp.WordCompleter = WordAutoComplete.WordCompleters[Lang];
                View.ShowWordCompletion();
            }
            else
            {
                PhoenixApp.WordCompleter = null;
                View.HideWordCompletion();
            }
        }
    }
}
