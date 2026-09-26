using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NIM.FileManagement;
using NIM.UIManage;
using NIMEngine;
using NIMEngine.Common;
using NIMEngine.Platform;
using NIMEngine.Translate;
using NIM.ApplicationLayer;

namespace NIM.UIManagement
{
    /// <summary>
    /// Interaction logic for PlatformConfigStyleWin.xaml
    /// </summary>
    public partial class PlatformConfigStyleWin : Window
    {
        private NIMGui _Owner;
        public PlatformConfigStyleWin(NIMGui Owner)
        {
            InitializeComponent();
            this._Owner = Owner;
        }

        public Border GenCloudAIConfig(int Key,string PlatformName, string Document, bool IsSystemNode, List<string> Keys, string Model, CustomPlatformType CustomType, List<string> Models)
        {
            Border GetPlatformBorder = UIHelper.CloneElement(CloudAIConfig);
            GetPlatformBorder.Tag = Key;

            Grid GetChildGrid = GetPlatformBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;
            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            StackPanel ControlPanel = (Header.Children[1] as Grid).Children[0] as StackPanel;

            Label TittleLab = GetTittlePanel.Children[0] as Label;
            TittleLab.Content = PlatformName;

            Ellipse GetShape = GetTittlePanel.Children[1] as Ellipse;
            GetShape.Fill = new SolidColorBrush(NIMApp.NodeStyleWin.GetNodeColor(CustomType));

            if (Document.Length == 0)
            {
                (ControlPanel.Children[0] as Label).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[0] as Label).Content = Document;
                (ControlPanel.Children[0] as Label).PreviewMouseDown += OpenUrl;
            }

            if (IsSystemNode)
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Visible;
                (ControlPanel.Children[1] as Border).Tag = GetPlatformBorder;
                (ControlPanel.Children[1] as Border).PreviewMouseDown += PlatformConfigStyleWin_PreviewMouseDown;
            }

            TextBox GetModelTextBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[5] as Border).Child as TextBox;
            ComboBox GetModels = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[6] as ComboBox;
            GetModels.Items.Clear();

            foreach (var GetModel in Models)
            {
                GetModels.Items.Add(GetModel);
            }

            GetModelTextBox.Text = Model;

            ListBox GetKeys = ((Body.Children[0] as Grid).Children[1] as ScrollViewer).Content as ListBox;
            GetKeys.Items.Clear();

            for (int i = 0; i < Keys.Count; i++)
            {
                GetKeys.Items.Add(Keys[i]);
            }

            ((Body.Children[0] as Grid).Children[1] as ScrollViewer).PreviewMouseWheel += PlatformConfigStyleWin_PreviewMouseWheel;

            Border GetAddBtn = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[2] as Border;
            GetAddBtn.Tag = GetPlatformBorder;
            GetAddBtn.PreviewMouseDown += GetAddBtn_PreviewMouseDown;

            Border RemoveBtn = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[3] as Border;
            RemoveBtn.Tag = GetPlatformBorder;
            RemoveBtn.PreviewMouseDown += RemoveBtn_PreviewMouseDown;

            TextBox ModelBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[5] as Border).Child as TextBox;
            ModelBox.Tag = GetPlatformBorder;
            ModelBox.TextChanged += ModelBox_TextChanged;

            ComboBox GetModelComboBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[6] as ComboBox);
            GetModelComboBox.Tag = ModelBox;
            GetModelComboBox.SelectionChanged += GetModelComboBox_SelectionChanged;
            return GetPlatformBorder;
        }

        private void OpenUrl(object sender, MouseButtonEventArgs e)
        {
            if (sender is Label)
            {
                string GetUrl = P_Convert.ObjToStr((sender as Label).Content);
                if (GetUrl.Length > 0)
                {
                    if (MessageBoxExtend.Show(NIMApp.WorkWin, "Prompt", "Do you want to open your default browser and visit\n " + GetUrl + "\n?", PreviewDialogSeverity.Information, true))
                    {
                        ExplorerHelper.OpenUrl(GetUrl);
                    }
                }
            }
        }

        public Border GenLocalAIConfig(int Key,string PlatformName, string Document, bool IsSystemNode,int LocalPort,string Model, CustomPlatformType CustomType)
        {
            Border GetPlatformBorder = UIHelper.CloneElement(LocalAIConfig);
            GetPlatformBorder.Tag = Key;

            Grid GetChildGrid = GetPlatformBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;
            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            StackPanel ControlPanel = (Header.Children[1] as Grid).Children[0] as StackPanel;

            Label TittleLab = GetTittlePanel.Children[0] as Label;
            TittleLab.Content = PlatformName;

            Ellipse GetShape = GetTittlePanel.Children[1] as Ellipse;
            GetShape.Fill = new SolidColorBrush(NIMApp.NodeStyleWin.GetNodeColor(CustomType));

            if (Document.Length == 0)
            {
                (ControlPanel.Children[0] as Label).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[0] as Label).Content = Document;
                (ControlPanel.Children[0] as Label).PreviewMouseDown += OpenUrl;
            }

            if (IsSystemNode)
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Visible;
                (ControlPanel.Children[1] as Border).Tag = GetPlatformBorder;
                (ControlPanel.Children[1] as Border).PreviewMouseDown += PlatformConfigStyleWin_PreviewMouseDown;
            }

            Label GetModelLab = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[3] as Label;

            GetModelLab.Content = Model;

            TextBox GetPortTextBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[1] as Border).Child as TextBox;
            GetPortTextBox.Text = LocalPort.ToString();
            GetPortTextBox.Tag = GetPlatformBorder;
            GetPortTextBox.TextChanged += GetPortTextBox_TextChanged;

            return GetPlatformBorder;
        }

        public Border GenTraditionalConfig(int Key,string PlatformName, string Document, bool IsSystemNode, List<string> Keys,CustomPlatformType CustomType)
        {
            Border GetPlatformBorder = UIHelper.CloneElement(TraditionalConfig);
            GetPlatformBorder.Tag = Key;

            Grid GetChildGrid = GetPlatformBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;
            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            StackPanel ControlPanel = (Header.Children[1] as Grid).Children[0] as StackPanel;

            Label TittleLab = GetTittlePanel.Children[0] as Label;
            TittleLab.Content = PlatformName;

            Ellipse GetShape = GetTittlePanel.Children[1] as Ellipse;
            GetShape.Fill = new SolidColorBrush(NIMApp.NodeStyleWin.GetNodeColor(CustomType));

            if (Document.Length == 0)
            {
                (ControlPanel.Children[0] as Label).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[0] as Label).Content = Document;
                (ControlPanel.Children[0] as Label).PreviewMouseDown += OpenUrl;
            }

            if (IsSystemNode)
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Collapsed;
            }
            else
            {
                (ControlPanel.Children[1] as Border).Visibility = Visibility.Visible;
                (ControlPanel.Children[1] as Border).Tag = GetPlatformBorder;
                (ControlPanel.Children[1] as Border).PreviewMouseDown += PlatformConfigStyleWin_PreviewMouseDown;
            }

            ListBox GetKeys = ((Body.Children[0] as Grid).Children[1] as ScrollViewer).Content as ListBox;
            GetKeys.Items.Clear();

            for (int i = 0; i < Keys.Count; i++)
            {
                GetKeys.Items.Add(Keys[i]);
            }

            ((Body.Children[0] as Grid).Children[1] as ScrollViewer).PreviewMouseWheel += PlatformConfigStyleWin_PreviewMouseWheel;

            Border GetAddBtn = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[2] as Border;
            GetAddBtn.Tag = GetPlatformBorder;
            GetAddBtn.PreviewMouseDown += GetAddBtn_PreviewMouseDown;

            Border RemoveBtn = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[3] as Border;
            RemoveBtn.Tag = GetPlatformBorder;
            RemoveBtn.PreviewMouseDown += RemoveBtn_PreviewMouseDown;

            //DeepL requires special handling.
            if (CustomType == CustomPlatformType.Traditional && PlatformName.ToLower().Equals("deepl"))
            {
                var SetIndex = 3 + 1;
                CheckBox IsFreeCheck = ((Body.Children[0] as Grid).Children[0] as StackPanel).Children[SetIndex] as CheckBox;
                IsFreeCheck.Click += IsFreeCheck_Click;
                if (NIMApp.EngineSetting.GetPlatformData(PlatformType.DeepL).IsFree)
                {
                    IsFreeCheck.IsChecked = true;
                }

                IsFreeCheck.Visibility = Visibility.Visible;
            }

            return GetPlatformBorder;
        }

        private void IsFreeCheck_Click(object sender, RoutedEventArgs e)
        {           
            CheckBox GetCheck = sender as CheckBox;
            int Key = (int)PlatformType.DeepL;
            if (GetCheck.IsChecked == true)
            {
                NIMApp.EngineSetting.PlatformConfigs[Key].IsFree = true;
            }
            else
            {
                NIMApp.EngineSetting.PlatformConfigs[Key].IsFree = false;
            }

            NIM_Engine.SaveConfig();
        }

        private void GetModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox)
            {
                ComboBox GetComboBoxHandle = sender as ComboBox;
                TextBox GetTargetText = GetComboBoxHandle.Tag as TextBox;
                GetTargetText.Text = P_Convert.ObjToStr(GetComboBoxHandle.SelectedValue);
            }
        }

        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Border GetMainBorder = (sender as TextBox).Tag as Border;

            Grid GetChildGrid = GetMainBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;

            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            Label TittleLab = GetTittlePanel.Children[0] as Label;

            int GetCustomID = P_Convert.ObjToInt(GetMainBorder.Tag);

            string GetPlatformName = P_Convert.ObjToStr(TittleLab.Content);
            TextBox ModelBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[5] as Border).Child as TextBox;
            string GetModel = ModelBox.Text;
            if (GetModel.Length > 0)
            {
                if (GetCustomID <= 0)
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (GetPlatformName == "ChatGpt" && Config.Platform == PlatformType.ChatGpt && Config.CustomInFo == null)
                        {
                            Config.Model = GetModel;
                            break;
                        }
                        else
                        if (GetPlatformName == "Gemini" && Config.Platform == PlatformType.Gemini && Config.CustomInFo == null)
                        {
                            Config.Model = GetModel;
                            break;
                        }
                        else
                        if (GetPlatformName == "DeepSeek" && Config.Platform == PlatformType.DeepSeek && Config.CustomInFo == null)
                        {
                            Config.Model = GetModel;
                            break;
                        }
                        else
                        if (GetPlatformName == "LM Studio" && Config.Platform == PlatformType.LMLocalAI && Config.CustomInFo == null)
                        {
                            Config.Model = GetModel;
                            break;
                        }
                        else
                        if (GetPlatformName == "DeepL" && Config.Platform == PlatformType.DeepL && Config.CustomInFo == null)
                        {
                            Config.Model = GetModel;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (Config.CustomInFo != null && Config.CustomInFo.CustomID.Equals(GetCustomID))
                        {
                            Config.Model = GetModel;
                            break;
                        }

                    }
                }
            }

            NIM_Engine.SaveConfig();
        }

        private void GetPortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Border GetMainBorder = (sender as TextBox).Tag as Border;

            Grid GetChildGrid = GetMainBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;

            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            Label TittleLab = GetTittlePanel.Children[0] as Label;

            int GetCustomID = P_Convert.ObjToInt(GetMainBorder.Tag);

            string GetPlatformName = P_Convert.ObjToStr(TittleLab.Content);
            TextBox GetPortTextBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[1] as Border).Child as TextBox;

            int GetPort = P_Convert.ObjToInt(GetPortTextBox.Text);

            if (GetPort > 0)
            {
                if (GetCustomID <= 0)
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];

                        if (GetPlatformName == "LM Studio" && Config.Platform == PlatformType.LMLocalAI && Config.CustomInFo == null)
                        {
                            NIMApp.EngineSetting.PlatformConfigs[GetKey].LocalPort = GetPort;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (Config.CustomInFo != null && Config.CustomInFo.CustomID.Equals(GetCustomID))
                        {
                            NIMApp.EngineSetting.PlatformConfigs[GetKey].LocalPort = GetPort;
                            break;
                        }
                    }
                }
            }

            NIM_Engine.SaveConfig();
        }

        private void GetAddBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Border GetMainBorder = (sender as Border).Tag as Border;

            Grid GetChildGrid = GetMainBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;

            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            Label TittleLab = GetTittlePanel.Children[0] as Label;

            int GetCustomID = P_Convert.ObjToInt(GetMainBorder.Tag);

            string GetPlatformName = P_Convert.ObjToStr(TittleLab.Content);
            TextBox GetKeyTextBox = (((Body.Children[0] as Grid).Children[0] as StackPanel).Children[1] as Border).Child as TextBox;
            ListBox GetKeys = ((Body.Children[0] as Grid).Children[1] as ScrollViewer).Content as ListBox;

            string GetApiKey = GetKeyTextBox.Text;

            if (GetApiKey.Trim().Length > 0)
            {
                if (GetCustomID <= 0)
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (GetPlatformName == "ChatGpt" && Config.Platform == PlatformType.ChatGpt && Config.CustomInFo == null)
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "Gemini" && Config.Platform == PlatformType.Gemini && Config.CustomInFo == null)
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "DeepSeek" && Config.Platform == PlatformType.DeepSeek && Config.CustomInFo == null)
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "LM Studio" && Config.Platform == PlatformType.LMLocalAI && Config.CustomInFo == null)
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "DeepL" && Config.Platform == PlatformType.DeepL && Config.CustomInFo == null)
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }
                    }

                    NIM_Engine.ReSetKeyData();
                }
                else
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (Config.CustomInFo != null && Config.CustomInFo.CustomID.Equals(GetCustomID))
                        {
                            if (!Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Add(GetApiKey);
                                GetKeys.Items.Add(GetApiKey);
                                break;
                            }
                        }

                    }

                    NIM_Engine.ReSetKeyData();
                }
            }
        }

        private void RemoveBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Border GetMainBorder = (sender as Border).Tag as Border;

            Grid GetChildGrid = GetMainBorder.Child as Grid;
            Grid Header = GetChildGrid.Children[0] as Grid;
            Grid Body = GetChildGrid.Children[1] as Grid;

            StackPanel GetTittlePanel = (Header.Children[0] as Border).Child as StackPanel;
            Label TittleLab = GetTittlePanel.Children[0] as Label;

            int GetCustomID = P_Convert.ObjToInt(GetMainBorder.Tag);

            string GetPlatformName = P_Convert.ObjToStr(TittleLab.Content);
           
            ListBox GetKeys = ((Body.Children[0] as Grid).Children[1] as ScrollViewer).Content as ListBox;

            string GetApiKey = P_Convert.ObjToStr(GetKeys.SelectedValue);

            if (GetApiKey.Trim().Length > 0)
            {
                if (GetCustomID <= 0)
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (GetPlatformName == "ChatGpt" && Config.Platform == PlatformType.ChatGpt && Config.CustomInFo == null)
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "Gemini" && Config.Platform == PlatformType.Gemini && Config.CustomInFo == null)
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "DeepSeek" && Config.Platform == PlatformType.DeepSeek && Config.CustomInFo == null)
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "LM Studio" && Config.Platform == PlatformType.LMLocalAI && Config.CustomInFo == null)
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                        else
                        if (GetPlatformName == "DeepL" && Config.Platform == PlatformType.DeepL && Config.CustomInFo == null)
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                    }

                    NIM_Engine.ReSetKeyData();
                }
                else
                {
                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                    {
                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                        var Config = NIMApp.EngineSetting.PlatformConfigs[GetKey];
                        if (Config.CustomInFo != null && Config.CustomInFo.CustomID.Equals(GetCustomID))
                        {
                            if (Config.ApiKeys.Contains(GetApiKey))
                            {
                                Config.ApiKeys.Remove(GetApiKey);
                                GetKeys.Items.Remove(GetApiKey);
                                break;
                            }
                        }
                    }

                    NIM_Engine.ReSetKeyData();
                }
            }
        }

        private void PlatformConfigStyleWin_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            bool Confirm = MessageBoxExtend.Show(NIMApp.WorkWin, "Delete Platform", "Are you sure you want to delete this platform? Deleting it will result in the loss of all configuration settings related to this platform.",PreviewDialogSeverity.Warning,true);
            if (Confirm)
            {
                int GetID = P_Convert.ObjToInt(((sender as Border).Tag as Border).Tag);

                for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                {
                    var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;
                    if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo != null)
                    {
                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.CustomID.Equals(GetID))
                        {
                            NIMApp.EngineSetting.PlatformConfigs.Remove(GetKey);
                            NIM_Engine.SaveConfig();
                            NIMApp.WorkWin.SyncPlatformConfig();
                            UIHelper.SyncNodes(_Owner.Nodes);
                            break;
                        }
                    }
                }
            }
        }

        private void PlatformConfigStyleWin_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            var Parent = VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement;
            Parent?.RaiseEvent(
                new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent
                });
        }
    }
}
