using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NIM.SkyrimManagement;
using PhoenixEngine.Common;

namespace NIM
{
    /// <summary>
    /// Interaction logic for NPCFinder.xaml
    /// </summary>
    public partial class NPCFinder : Window
    {
        public ModFile ModRef = null;
        public NPCFinder(ModFile ModRef)
        {
            this.ModRef = ModRef;
            InitializeComponent();
        }

        public string SearchName = "";

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < ModRef.ListView.RealLines.Count; i++)
            {
                var Key = ModRef.ListView.RealLines[i].Key;

                if (ModRef.EspReader.GameCharacters.ContainsKey(Key))
                {
                    if (ModRef.EspReader.GameCharacters[Key][0].Name.Equals(SearchName))
                    {
                        ModRef.ListView.Goto(Key);
                        return;
                    }
                }
            }
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            for (int i = 0; i < NameCache.Count; i++)
            {
                if (NameCache[i].StartsWith(NpcName.Text))
                {
                    NpcNames.SelectedValue = NameCache[i];
                    break;
                }
                else
                if (NameCache[i].Equals(NpcName.Text))
                {
                    NpcNames.SelectedValue = NameCache[i];
                    break;
                }
            }
        }
        public List<string> NameCache = new List<string>();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NpcNames.Items.Clear();

            foreach (var GetNpc in ModRef.EspReader.GameCharacters.ToList())
            {
                var Name = GetNpc.Value[0].Name;

                if (!NameCache.Contains(Name))
                {
                    NameCache.Add(Name);
                    NpcNames.Items.Add(Name);
                }
            }
        }

        private void NpcNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchName = P_Convert.ObjToStr(NpcNames.SelectedValue);
        }
    }
}
