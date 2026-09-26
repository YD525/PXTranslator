using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace NIM
{
    /// <summary>
    /// Interaction logic for SearchText.xaml
    /// </summary>
    public partial class SearchText : Window
    {
        public TextEditor CodeTextBox;
        public SearchText(TextEditor SetEditor)
        {
            this.CodeTextBox = SetEditor;
            InitializeComponent();
        }


        int Line = 0;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool IsFind = false;
            for (int i = Line; i < this.CodeTextBox.LineCount; i++)
            {
                var GetLineStr = this.CodeTextBox.Text.Substring(this.CodeTextBox.Document.Lines[i].Offset, this.CodeTextBox.Document.Lines[i].Length);
                if (GetLineStr.Contains(Search.Text))
                {
                    this.CodeTextBox.ScrollToLine(i);
                    int GetOffset = GetLineStr.IndexOf(Search.Text);
                    this.CodeTextBox.Select(this.CodeTextBox.Document.Lines[i].Offset + GetOffset, Search.Text.Length);

                    Line = i + 1;
                    IsFind = true;
                    break;
                }
            }
            if (!IsFind)
            {
                Line = 0;
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            Line = 0;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Button_Click(null,null);
            }
        }
    }
}
