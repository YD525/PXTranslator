using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NIM.UIManage;
using NIMEngine;
using NIMEngine.Common;
using NIMEngine.Platform;
using NIMEngine.Translate;

namespace NIM.UIManagement
{
    /// <summary>
    /// Interaction logic for NodeStyleWin.xaml
    /// </summary>
    public partial class NodeStyleWin : Window
    {
        public NodeStyleWin()
        {
            InitializeComponent();
        }

        public void SetHeaderTagEnableInFo(Grid Header, string InFo)
        {
            if (Header.Children[1] is StackPanel)
            {
                Label GetInFoControlHandle = ((Border)((StackPanel)Header.Children[1]).Children[0]).Child as Label;
                GetInFoControlHandle.Content = InFo;
            }
            if (Header.Children[1] is Border)
            {
                Label GetInFoControlHandle = ((Border)Header.Children[1]).Child as Label;
                GetInFoControlHandle.Content = InFo;
            }
        }

        public Grid GenNodeTree(string Tittle)
        {
            Grid NewHeaderTag = UIHelper.CloneElement(HeaderTag);
            NewHeaderTag.Tag = Tittle;
            Label GetTittle = NewHeaderTag.Children[0] as Label;
            GetTittle.Content = Tittle;
            return NewHeaderTag;
        }
        public Grid GenMainNodeTree(string Tittle)
        {
            Grid NewHeaderTag = UIHelper.CloneElement(MainHeaderTag);
            NewHeaderTag.Tag = Tittle;
            Label GetTittle = NewHeaderTag.Children[0]  as Label;
            GetTittle.Content = Tittle;

            return NewHeaderTag;
        }

        public Color GetNodeColor(CustomPlatformType Type)
        {
            switch (Type)
            {
                case CustomPlatformType.CloudAI:
                    return Color.FromRgb(239, 234, 190);
                case CustomPlatformType.LocalAI:
                    return Color.FromRgb(250, 227, 6);
                case CustomPlatformType.Traditional:
                    return Color.FromRgb(255, 255, 255);
            }

            return Color.FromRgb(250, 227, 6);
        }

        public void SetNodeEnable(Grid NodeGrid,bool Enable)
        {
            Grid GetMask = NodeGrid.Children[0] as Grid;

            if (Enable)
            {
                GetMask.Visibility = Visibility.Visible;
            }
            else
            {
                GetMask.Visibility = Visibility.Collapsed;
            }
        }

        public ContentControl GetNodeLight(Grid NodeGrid)
        {
            Grid NodeBody = NodeGrid.Children[1] as Grid;

            return NodeBody.Children[1] as ContentControl;
        }

        public class HeaderInFo
        {
            public StackPanel Parent;
            public PlatformType MainType;
            public CustomPlatformType Type;
            public int CustomID;
       

            public HeaderInFo(StackPanel Parent, PlatformType mainType, CustomPlatformType type, int customID)
            {
                this.Parent = Parent;
                MainType = mainType;
                Type = type;
                CustomID = customID;
            }
        }

        public Grid GenNode(StackPanel MainStack,string PlatformName, PlatformType MainType,CustomPlatformType Type,int CustomID, bool Enable)
        {
            Grid NodeGrid = UIHelper.CloneElement(Node);
            NodeGrid.Tag = new HeaderInFo(MainStack,MainType, Type,CustomID);

            Grid GetMask = NodeGrid.Children[0] as Grid;

            Border GetEnableBtn = (GetMask.Children[1] as Grid).Children[0] as Border;
            GetEnableBtn.Tag = NodeGrid;
            GetEnableBtn.PreviewMouseDown += GetEnableBtn_PreviewMouseDown;

            if (Enable)
            {
                GetMask.Visibility = Visibility.Collapsed;
            }

            Grid NodeBody = NodeGrid.Children[1] as Grid;

            StackPanel GetStackPanel = NodeBody.Children[0] as StackPanel;
            Grid GetColorGrid = GetStackPanel.Children[0] as Grid;

            GetColorGrid.Background = new SolidColorBrush(GetNodeColor(Type));

            Label GetTittle = GetStackPanel.Children[1] as Label;
            GetTittle.Content = PlatformName;

            TextBlock DisableBtn = GetStackPanel.Children[2] as TextBlock;
            DisableBtn.Tag = NodeGrid;
            DisableBtn.PreviewMouseDown += DisableBtn_PreviewMouseDown;

            if (PlatformName.Equals("PreTranslate Node"))
            {
                DisableBtn.Visibility = Visibility.Hidden;
            }

            return NodeGrid;
        }

        public void SyncCount(StackPanel Nodes)
        {
            foreach (var Get in Nodes.Children)
            {
                if (Get is Grid)
                { 
                    Grid SetGrid = (Grid)Get;
                    if (SetGrid.Children[0] is Label)
                    {
                        string GetName = P_Convert.ObjToStr(SetGrid.Tag);
                        switch (GetName)
                        {
                            case "Engine Nodes":
                                {
                                    if (NIMApp.EngineSetting.PreTranslateEnable)
                                    {
                                        SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", 1, 1));
                                    }
                                    else
                                    {
                                        SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", 0, 1));
                                    }
                                }
                            break;
                            case "Cloud AI Nodes":
                                {
                                    int TotalCount = 0;
                                    int EnableCount = 0;
                                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                                    {
                                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo == null)
                                        {
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.Gemini ||
                                                NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.ChatGpt ||
                                                NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.DeepSeek)
                                            {
                                                TotalCount++;
                                                if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                                {
                                                    EnableCount++;
                                                }
                                            }
                                        }
                                        else
                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.Type == CustomPlatformType.CloudAI)
                                        {
                                            TotalCount++;
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                            {
                                                EnableCount++;
                                            }
                                        }
                                    }
                                    SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", EnableCount,TotalCount));
                                }
                            break;
                            case "Local AI Nodes":
                                {
                                    int TotalCount = 0;
                                    int EnableCount = 0;
                                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                                    {
                                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo == null)
                                        {
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.LMLocalAI)
                                            {
                                                TotalCount++;
                                                if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                                {
                                                    EnableCount++;
                                                }
                                            }
                                        }
                                        else
                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.Type == CustomPlatformType.LocalAI)
                                        {
                                            TotalCount++;
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                            {
                                                EnableCount++;
                                            }
                                        }
                                    }
                                    SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", EnableCount, TotalCount));
                                }
                            break;
                            case "Traditional Nodes":
                                {
                                    int TotalCount = 0;
                                    int EnableCount = 0;
                                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                                    {
                                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo == null)
                                        {
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.DeepL)
                                            {
                                                TotalCount++;
                                                if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                                {
                                                    EnableCount++;
                                                }
                                            }
                                        }
                                        else
                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.Type == CustomPlatformType.Traditional)
                                        {
                                            TotalCount++;
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                            {
                                                EnableCount++;
                                            }
                                        }
                                    }
                                    SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", EnableCount, TotalCount));
                                }
                            break;
                            case "Interactive Nodes":
                                {
                                    int TotalCount = 0;
                                    int EnableCount = 0;

                                    for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                                    {
                                        var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                                        if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == PlatformType.HumanTranslation)
                                        {
                                            if (NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable)
                                            {
                                                EnableCount++;
                                            }

                                            TotalCount++;
                                        }
                                    }

                                    SetHeaderTagEnableInFo(SetGrid, string.Format("{0} / {1} Enabled", EnableCount, TotalCount));
                                }
                            break;
                        }
                    }
                }
            }
        }

        private void DisableBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Grid GetNodeGrid = ((TextBlock)sender).Tag as Grid;
            HeaderInFo GetInFo = GetNodeGrid.Tag as HeaderInFo;

            Grid GetMask = GetNodeGrid.Children[0] as Grid;

            if (GetInFo.MainType == PlatformType.Null && GetInFo.Type == CustomPlatformType.Null && GetInFo.CustomID == 0)
            {
                NIMApp.EngineSetting.PreTranslateEnable = false;
            }
            else
            {
                for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                {
                    var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                    if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo == null
                        &&
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == GetInFo.MainType)
                    {
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable = false;
                        break;
                    }
                    else
                    if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo != null
                        &&
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.CustomID == GetInFo.CustomID)
                    {
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable = false;
                        break;
                    }
                }
            }

            GetMask.Visibility = Visibility.Visible;

            Phoenix.SaveConfig();
            SyncCount((GetNodeGrid.Tag as HeaderInFo).Parent);
        }

        private void GetEnableBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Grid GetNodeGrid = ((Border)sender).Tag as Grid;
            HeaderInFo GetInFo = GetNodeGrid.Tag as HeaderInFo;

            Grid GetMask = GetNodeGrid.Children[0] as Grid;

            if (GetInFo.MainType == PlatformType.Null && GetInFo.Type == CustomPlatformType.Null && GetInFo.CustomID == 0)
            {
                NIMApp.EngineSetting.PreTranslateEnable = true;
            }
            else
            {
                for (int i = 0; i < NIMApp.EngineSetting.PlatformConfigs.Count; i++)
                {
                    var GetKey = NIMApp.EngineSetting.PlatformConfigs.ElementAt(i).Key;

                    if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo == null
                        &&
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Platform == GetInFo.MainType)
                    {
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable = true;
                        break;
                    }
                    else
                    if (NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo != null
                        &&
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].CustomInFo.CustomID == GetInFo.CustomID)
                    {
                        NIMApp.EngineSetting.PlatformConfigs[GetKey].Enable = true;
                        break;
                    }
                }
            }

            GetMask.Visibility = Visibility.Collapsed;

            Phoenix.SaveConfig();
            SyncCount((GetNodeGrid.Tag as HeaderInFo).Parent);
        }

        public Grid GenEmptyNode(CustomPlatformType Type)
        {
            Grid EmptyNodeGrid = UIHelper.CloneElement(EmptyNode);

            Border GetAddBtn = EmptyNodeGrid.Children[0] as Border;
            GetAddBtn.Tag = Type;

            GetAddBtn.PreviewMouseDown += GetAddBtn_PreviewMouseDown;

            return EmptyNodeGrid;
        }

        private void GetAddBtn_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border)
            { 
               Border GetBtnHandle = (Border)sender;
               CustomPlatformType GetType = (CustomPlatformType)GetBtnHandle.Tag;

                CustomWizard NCustomWizard = new CustomWizard(NIMApp.WorkWin);
                NCustomWizard.Owner = NIMApp.WorkWin;
                NCustomWizard.Show();
                NCustomWizard.SelectPlatformType(GetType);
            }
        }
    }
}
