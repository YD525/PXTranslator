using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NIM.UIManage;
using PhoenixEngine;
using PhoenixEngine.Common;
using PhoenixEngine.Engine;
using PhoenixEngine.Language;
using PhoenixEngine.Memory;
using PhoenixEngine.P_Delegate;
using PhoenixEngine.Platform;
using PhoenixEngine.Platform.LocalAI;
using PhoenixEngine.Request;
using PhoenixEngine.Translate;
using PhoenixEngine.Unit;
using NIM.ApplicationLayer;

namespace NIM
{
    /// <summary>
    /// Interaction logic for CustomWizard.xaml
    /// </summary>
    public partial class CustomWizard : Window
    {
        private PhoenixGui _Owner;
        public CustomWizard(PhoenixGui Owner)
        {
            InitializeComponent();
            this._Owner = Owner;
        }

        public void SelectPlatformType(CustomPlatformType Type)
        {
            switch (Type)
            {
                case CustomPlatformType.CloudAI:
                    {
                        PlatformType.SelectedValue = "Cloud AI";
                    }
                    break;
                case CustomPlatformType.LocalAI:
                    {
                        PlatformType.SelectedValue = "Local AI";
                    }
                    break;
                case CustomPlatformType.Traditional:
                    {
                        PlatformType.SelectedValue = "Traditional";
                    }
                    break;
            }
        }

        public int Step = 1;

        public void SyncUI()
        {
            StepLab.Content = string.Format("{0}/3", Step);

            switch (Step)
            {
                case 1:
                    {
                        Tittle.Content = "Add Platform";

                        PlatformType.Items.Clear();
                        PlatformType.Items.Add("Local AI");
                        PlatformType.Items.Add("Cloud AI");
                        PlatformType.Items.Add("Traditional");

                        if (CurrentPlatformType.Length > 0)
                        {
                            PlatformType.SelectedValue = CurrentPlatformType;
                        }
                    }
                    break;
                case 2:
                    {
                        Tittle.Content = "Config Request body";

                        AutomaticFieldsHint.Text = _AutomaticFieldsList.Count > 0
                            ? "Automatic fields available for this platform: " + string.Join("  /  ", _AutomaticFieldsList)
                            : "";
                    }
                    break;
                case 3:
                    {
                        Tittle.Content = "Identify the content returned by the request";
                    }
                    break;
            }

            if (Step == 3)
            {
                NextBtn.Visibility = Visibility.Collapsed;
                FinishBtn.Visibility = Visibility.Visible;
            }
            else
            {
                NextBtn.Visibility = Visibility.Visible;
                FinishBtn.Visibility = Visibility.Collapsed;
            }

            if (Step == 1)
            {
                BackBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackBtn.Visibility = Visibility.Visible;
            }

            foreach (var GetView in Views.Children)
            {
                if (GetView is Grid)
                {
                    Grid ViewHandle = (Grid)GetView;
                    string GetViewName = P_Convert.ObjToStr(ViewHandle.Name);
                    if (GetViewName.Equals(string.Format("View{0}", Step)))
                    {
                        ViewHandle.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ViewHandle.Visibility = Visibility.Hidden;
                    }
                }
            }
        }
        public string CurrentPlatformType = "";
        private CustomPlatformInFo CustomPlatform = null;
        private CustomReqCore TestCustomCore = null;
        private ReqQueryRuleItem QueryRule = null;

        // Automatic fields available for the currently selected platform type (Step 1),
        // reused as the dropdown source for every binding row in Step 2.
        private List<string> _AutomaticFieldsList = new List<string>();

        private void Next(object sender, MouseButtonEventArgs e)
        {
            if (Step == 1)
            {
                if (CustomPlatform == null)
                {
                    CustomPlatform = new CustomPlatformInFo();
                    TestCustomCore = new CustomReqCore();
                    CustomPlatform.CustomID = NIMApp.EngineSetting.PlatformConfigs.Count + 1;
                }

                CustomPlatform.Name = PlatformName.Text;

                if (CustomPlatform.Name.Length == 0)
                {
                    MessageBoxExtend.Show(this,"Msg", "Please set the platform name.",PreviewDialogSeverity.Information);
                    return;
                }
                if (CurrentPlatformType.Length == 0)
                {
                    MessageBoxExtend.Show(this,"Msg","Please select the platform type.",PreviewDialogSeverity.Information);
                    return;
                }

                switch (CurrentPlatformType)
                {
                    case "Local AI":
                        {
                            TestModelCap.Visibility = Visibility.Visible;
                            TestModel.Visibility = Visibility.Visible;

                            _AutomaticFieldsList = new List<string> { "{AI_Prompt}", "{AI_Model}" };
                            CustomPlatform.Type = CustomPlatformType.LocalAI;
                        }
                        break;
                    case "Cloud AI":
                        {
                            TestModelCap.Visibility = Visibility.Visible;
                            TestModel.Visibility = Visibility.Visible;

                            _AutomaticFieldsList = new List<string> { "{API_KEY}", "{AI_Prompt}", "{AI_Model}" };
                            CustomPlatform.Type = CustomPlatformType.CloudAI;
                        }
                        break;
                    case "Traditional":
                        {
                            TestModelCap.Visibility = Visibility.Collapsed;
                            TestModel.Visibility = Visibility.Collapsed;

                            _AutomaticFieldsList = new List<string> { "{API_KEY}", "{SourceStr}", "{P_From}", "{P_To}" };
                            CustomPlatform.Type = CustomPlatformType.Traditional;
                        }
                        break;
                }
            }

            if (Step == 2)
            {
                if (CurrentResponse.Length == 0)
                {
                    MessageBoxExtend.Show(this,"Msg", "Please click TestCall first to ensure the API returns a normal response.",PreviewDialogSeverity.Warning);
                    return;
                }
            }

            if (Step < 3)
            {
                Step++;
                SyncUI();
            }

            if (Step == 3)
            {
                P_Response.Text = CurrentResponse;
                var GetKeyValues = CustomPlatformHelper.GetJsonValues(CurrentResponse);

                QueryRule = new ReqQueryRuleItem();

                if (GetKeyValues.Count == 0)
                {
                    QueryRule.ByJson = false;
                    IsJson.IsChecked = false;
                }
                else
                {
                    QueryRule.ByJson = true;
                    IsJson.IsChecked = true;

                    P_ResponseTags.Items.Clear();

                    foreach (var GetItem in GetKeyValues)
                    {
                        if (CustomPlatform.Type == CustomPlatformType.LocalAI || CustomPlatform.Type == CustomPlatformType.CloudAI)
                        {
                            if (MatchTranslationJson(GetItem.Value))
                            {
                                QueryRule.FieldName = GetItem.Key;
                                FieldName.Content = string.Format("FieldName:{0}", GetItem.Key);
                                MessageBoxExtend.Show(this,"Msg", "The fields have been automatically retrieved; please click Finish to end this wizard.",PreviewDialogSeverity.Information);
                            }
                        }

                        P_ResponseTags.Items.Add(string.Format("{0}->{1}", GetItem.Key, GetItem.Value));
                    }
                }
            }
        }

        private void Back(object sender, MouseButtonEventArgs e)
        {
            if (Step > 1)
            {
                Step--;
                SyncUI();
            }

            if (Step == 2)
            {
                CurrentResponse = string.Empty;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CustomPlatform = null;
            CurrentPlatformType = string.Empty;
            _AutomaticFieldsList = new List<string>();

            SyncUI();
        }

        private void PlatformType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var GetSelectValue = P_Convert.ObjToStr(PlatformType.SelectedValue);
            if (GetSelectValue.Length > 0)
            {
                CurrentPlatformType = GetSelectValue;
            }
        }

        // Builds/updates the tag list for a field (Url/Header/Payload), keeping any value the
        // user already bound for a key that is still present, instead of wiping it on every keystroke.
        private List<ReqReplaceTag> MergeTags(List<ReqCustomKeyValue> RawKeyValues, List<ReqReplaceTag> OldTags)
        {
            List<ReqReplaceTag> Merged = new List<ReqReplaceTag>();

            foreach (var Kv in RawKeyValues)
            {
                var Old = OldTags != null ? OldTags.FirstOrDefault(T => T.Key == Kv.Key) : null;
                Merged.Add(Old != null ? Old : new ReqReplaceTag(Kv.Key, Kv.Value));
            }

            return Merged;
        }

        // Renders one row per detected key with an editable ComboBox showing its current
        // bound value; picking or typing a value commits immediately via TagValue_Changed.
        private void RebuildBindingRows(StackPanel Container, string TagTypeName, List<ReqCustomKeyValue> RawKeyValues, List<ReqReplaceTag> Tags)
        {
            Container.Children.Clear();

            foreach (var Kv in RawKeyValues)
            {
                var BoundTag = Tags.FirstOrDefault(T => T.Key == Kv.Key);
                string CurrentValue = BoundTag != null ? BoundTag.GetValue() : Kv.Value;

                Grid Row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                Row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

                Label KeyLabel = new Label
                {
                    Content = Kv.Key,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(KeyLabel, 0);

                ComboBox ValueBox = new ComboBox
                {
                    IsEditable = true,
                    Text = CurrentValue,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ItemsSource = _AutomaticFieldsList,
                    Tag = new Tuple<string, string>(TagTypeName, Kv.Key)
                };
                ValueBox.SelectionChanged += TagValue_Changed;
                ValueBox.LostKeyboardFocus += TagValue_Changed;
                Grid.SetColumn(ValueBox, 1);

                Row.Children.Add(KeyLabel);
                Row.Children.Add(ValueBox);

                Container.Children.Add(Row);
            }
        }

        private void TagValue_Changed(object sender, RoutedEventArgs e)
        {
            if (CustomPlatform == null) return;

            ComboBox Box = (ComboBox)sender;
            var Info = (Tuple<string, string>)Box.Tag;
            string TagTypeName = Info.Item1;
            string Key = Info.Item2;
            string NewValue = Box.Text;

            List<ReqReplaceTag> Tags;
            switch (TagTypeName)
            {
                case "Url": Tags = CustomPlatform.Url_Tags; break;
                case "Header": Tags = CustomPlatform.Header_Tags; break;
                case "Payload": Tags = CustomPlatform.PayLoad_Tags; break;
                default: return;
            }

            var ExistingTag = Tags.FirstOrDefault(T => T.Key == Key);
            if (ExistingTag != null)
            {
                ExistingTag.SetValue(NewValue, ReqEncodeType.Null);
            }
            else
            {
                Tags.Add(new ReqReplaceTag(Key, NewValue));
            }
        }

        private void Url_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TestCustomCore == null) return;

            CustomPlatform.Url = HttpUtility.UrlDecode(Url.Text);
            TestCustomCore.SetUrl(CustomPlatform.Url);

            var RawTags = TestCustomCore.GetUrlKeyValues();
            CustomPlatform.Url_Tags = MergeTags(RawTags, CustomPlatform.Url_Tags);

            RebuildBindingRows(UrlBindings, "Url", RawTags, CustomPlatform.Url_Tags);
        }

        private void Header_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TestCustomCore == null) return;

            CustomPlatform.Header = Header.Text;
            TestCustomCore.SetHeader(CustomPlatform.Header);

            var RawTags = TestCustomCore.GetHeaderKeyValues();
            CustomPlatform.Header_Tags = MergeTags(RawTags, CustomPlatform.Header_Tags);

            RebuildBindingRows(HeaderBindings, "Header", RawTags, CustomPlatform.Header_Tags);
        }

        private void Payload_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TestCustomCore == null) return;

            CustomPlatform.PayLoad = Payload.Text;
            TestCustomCore.SetPayLoad(CustomPlatform.PayLoad);

            var RawTags = TestCustomCore.GetPayLoadKeyValues();
            CustomPlatform.PayLoad_Tags = MergeTags(RawTags, CustomPlatform.PayLoad_Tags);

            RebuildBindingRows(PayloadBindings, "Payload", RawTags, CustomPlatform.PayLoad_Tags);
        }

        private void IsPost_Click(object sender, RoutedEventArgs e)
        {
            if (IsPost.IsChecked == true)
            {
                CustomPlatform.IsPost = true;
            }
            else
            {
                CustomPlatform.IsPost = false;
            }
        }

        public string ApiKey = "";
        private void TestApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApiKey = TestApiKey.Text;
        }

        public string CurrentResponse = "";
        private void TestCall(object sender, MouseButtonEventArgs e)
        {
            PlatformConfig NPlatformConfig = new PlatformConfig();
            NPlatformConfig.Platform = PhoenixEngine.Translate.PlatformType.CustomPlatform;
            NPlatformConfig.Enable = true;

            int TestID = 525;

            NPlatformConfig.CustomInFo = CustomPlatform;

            if (!NIMApp.EngineSetting.PlatformConfigs.ContainsKey(TestID))
            {
                NIMApp.EngineSetting.PlatformConfigs.Add(TestID, NPlatformConfig);
            }
            else
            {
                throw (new Exception("Adding more than 500 platforms is not supported."));
            }

            BaseUnit TestUnit = new BaseUnit(-525, "525", "", "Test Line", "", "", 100);
            var UnitGroup = new Translator("-1", NIMApp.SelfSetting.SourceLanguage, NIMApp.SelfSetting.TargetLanguage, false).ToUnitGroup(TestUnit);
            Languages From = NIMApp.SelfSetting.SourceLanguage;
            Languages To = NIMApp.SelfSetting.TargetLanguage;

            if (From == To)
            {
                From = Languages.English;
                To = Languages.French;
            }
            try
            {

                switch (CurrentPlatformType)
                {
                    case "Local AI":
                        {
                            AICall GenAICall = new AICall();
                            CustomLocalAIApi NCustomLocalAIApi = new CustomLocalAIApi();
                            NCustomLocalAIApi.Init(TestID, new AITranslationMemory(), NIMApp.EngineSetting);

                            NCustomLocalAIApi.Model = Model;

                            NCustomLocalAIApi.QuickTrans(
                                new List<ReplaceTag>(),
                                UnitGroup,
                                From,
                                To,
                                false,
                                0,
                                string.Empty,
                                ref GenAICall
                                );

                            Response.Text = GenAICall.ReceiveString;
                            CurrentResponse = GenAICall.ReceiveString;

                            MessageBoxExtend.Show(this,"Msg", CurrentResponse,PreviewDialogSeverity.Information);
                        }
                        break;
                    case "Cloud AI":
                        {
                            AICall GenAICall = new AICall();
                            CustomAIApi NCustomAIApi = new CustomAIApi();
                            NCustomAIApi.Init(TestID, new AITranslationMemory(), NIMApp.EngineSetting, ProxyCenter.CurrentProxy);

                            NCustomAIApi.Model = Model;

                            NCustomAIApi.QuickTrans(
                                ApiKey,
                                new List<ReplaceTag>(),
                                UnitGroup,
                                From,
                                To,
                                false,
                                0,
                                string.Empty,
                                ref GenAICall
                                );

                            Response.Text = GenAICall.ReceiveString;
                            CurrentResponse = GenAICall.ReceiveString;

                            MessageBoxExtend.Show(this,"Msg",CurrentResponse,PreviewDialogSeverity.Information);
                        }
                        break;
                    case "Traditional":
                        {
                            PlatformCall GenPlatformCall = new PlatformCall();
                            CustomApi NCustomApi = new CustomApi();
                            NCustomApi.Init(TestID, NIMApp.EngineSetting, ProxyCenter.CurrentProxy);

                            NCustomApi.QuickTrans(
                                ApiKey,
                                UnitGroup,
                                From,
                                To,
                                ref GenPlatformCall
                            );

                            Response.Text = GenPlatformCall.ReceiveString;
                            CurrentResponse = GenPlatformCall.ReceiveString;

                            MessageBoxExtend.Show(this,"Msg",CurrentResponse,PreviewDialogSeverity.Information);
                        }
                        break;
                }

            }
            catch (Exception Ex)
            {
                MessageBoxExtend.Show(this,"Msg",Ex.Message,PreviewDialogSeverity.Error);
            }

            if (NIMApp.EngineSetting.PlatformConfigs.ContainsKey(TestID))
            {
                NIMApp.EngineSetting.PlatformConfigs.Remove(TestID);
            }
        }

        public bool MatchTranslationJson(string Input)
        {
            return Regex.IsMatch(Input, @"^\s*\{\s*""translation""\s*:\s*""(?:\\.|[^""\\])*""\s*\}\s*$");
        }

        private void P_ResponseTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetSelectValue = P_Convert.ObjToStr(P_ResponseTags.SelectedValue);
            if (GetSelectValue.Trim().Length > 0)
            {
                string GetKey = GetSelectValue.Substring(0, GetSelectValue.IndexOf("->"));
                FieldName.Content = string.Format("FieldName:{0}", GetKey);
                QueryRule.FieldName = GetKey;
            }
        }

        private void LeftStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            QueryRule.LeftStr = LeftStr.Text.Trim();
        }

        private void RightStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            QueryRule.RightStr = RightStr.Text.Trim();
        }

        private void SplitStr_TextChanged(object sender, TextChangedEventArgs e)
        {
            QueryRule.SplitStr = SplitStr.Text.Trim();
        }

        private void TestGetResponseBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string TransStr = "";
            if (QueryRule.ByJson)
            {
                var GetTags = CustomPlatformHelper.GetJsonValues(CurrentResponse);

                for (int i = 0; i < GetTags.Count; i++)
                {
                    if (GetTags[i].Key.Equals(QueryRule.FieldName))
                    {
                        TransStr = GetTags[i].Value;
                        break;
                    }
                }
            }
            else
            if (QueryRule.SplitStr.Trim().Length > 0)
            {
                TransStr = CurrentResponse.Substring(CurrentResponse.LastIndexOf(QueryRule.SplitStr) + QueryRule.SplitStr.Length);
            }
            else
            if (QueryRule.LeftStr.Trim().Length > 0)
            {
                TransStr = CurrentResponse.StringDivision(QueryRule.LeftStr, QueryRule.RightStr);
            }

            MessageBoxExtend.Show(this,"Msg",TransStr,PreviewDialogSeverity.Information);
        }

        private void FinishBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformConfig NPlatformConfig = new PlatformConfig();
            NPlatformConfig.Platform = PhoenixEngine.Translate.PlatformType.CustomPlatform;
            NPlatformConfig.Enable = false;

            CustomPlatform.QueryRule = QueryRule;

            NPlatformConfig.ApiKeys.Add(ApiKey);

            while (NIMApp.EngineSetting.PlatformConfigs.ContainsKey(CustomPlatform.CustomID))
            {
                CustomPlatform.CustomID = NIMApp.EngineSetting.PlatformConfigs.Count + 1;
            }

            NPlatformConfig.Model = Model;
            NPlatformConfig.CustomInFo = CustomPlatform;

            NIMApp.EngineSetting.PlatformConfigs.Add(CustomPlatform.CustomID, NPlatformConfig);
            Phoenix.SaveConfig();

            ClearValue();

            UIHelper.SyncNodes(_Owner.Nodes);
            Phoenix.ReSetKeyData();

            this.Close();
        }

        public void ClearValue()
        {
            CustomPlatform = null;
            CurrentPlatformType = string.Empty;
            QueryRule = null;
            _AutomaticFieldsList = new List<string>();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ClearValue();
        }

        public string Model = "";
        private void TestModel_TextChanged(object sender, TextChangedEventArgs e)
        {
            Model = TestModel.Text;
        }
    }
}