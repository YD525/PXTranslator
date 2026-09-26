using NIM.UIManage;
using NIM.UIManagement;
using System;
using System.Collections.Generic;
using System.Threading;
using NIMEngine.Events;
using NIMEngine.P_Delegate;
using NIMEngine.Unit;
using NIMEngine.Platform;
using static NIMEngine.Platform.HumanTranslationApi;
using System.Windows;

namespace NIM.TranslateManage
{
    public class TranslatorInterface
    {
        public static void Init()
        {
            HumanTranslationApi.WaitHumanInput += new AwaitHumanTranslationHandler((Send) => 
            {
                if (NIMApp.WorkWin == null)
                {
                    return string.Empty;
                }

                InteractiveView NInteractiveView = null;

                Application.Current.Dispatcher.Invoke(new Action(() => 
                {
                    NInteractiveView = new InteractiveView();
                    NInteractiveView.Owner = NIMApp.WorkWin;
                    NInteractiveView.SetSend(Send);
                }));

                while (!NInteractiveView.CanExit)
                {
                    Thread.Sleep(500);
                }

                string Received = NInteractiveView.Received;

                Application.Current.Dispatcher.Invoke(new Action(() => 
                {
                    NInteractiveView.CanClose = true;
                    NInteractiveView.Close();
                }));

                return Received;
            });

            EngineEvents.SetDataCall += Recv;
            EngineEvents.SetBaseUnitStateChangedCallback += BaseUnitStateChanged;

            RegListener("PreLog", new List<int>() { 2 }, new Action<int, object>((Sign, Any) =>
            {
                if (Sign == 2)
                {
                    if (Any is PreTranslateCall)
                    {
                        PreTranslateCall GetCall = (PreTranslateCall)Any;

                        UIHelper.NodeCallCallback(0, GetCall.Platform);
                    }
                }
            }));

            RegListener("MainLog", new List<int>() { 0 }, new Action<int, object>((Sign, Any) =>
            {
                if (Sign == 0)
                {
                    if (Any is string)
                    {
                        LogHelper.SetMainLog((string)Any);
                    }
                }
            }));

            RegListener("InputOutputLog", new List<int>() { 3, 5 }, new Action<int, object>((Sign, Any) =>
            {
                if (Sign == 5 || Sign == 3)
                {
                    long Length = 0;

                    if (Any is AICall)
                    {
                        AICall GetCall = (AICall)Any;

                        UIHelper.NodeCallCallback(GetCall.CustomID, GetCall.Platform);

                        if (GetCall.SendString != null)
                        {
                            Length = GetCall.SendString.Length;
                        }

                        LogHelper.SetInputLog(GetCall.Platform.ToString() + "->\n" + GetCall.SendString);
                        LogHelper.SetOutputLog(GetCall.Platform.ToString() + "->\n" + GetCall.ReceiveString);
                    }
                    if (Any is PlatformCall)
                    {
                        PlatformCall GetCall = (PlatformCall)Any;

                        UIHelper.NodeCallCallback(GetCall.CustomID, GetCall.Platform);

                        if (GetCall.SendString != null)
                        {
                            Length = GetCall.SendString.Length;
                        }

                        LogHelper.SetInputLog(GetCall.Platform.ToString() + "->\n" + GetCall.SendString);
                        LogHelper.SetOutputLog(GetCall.Platform.ToString() + "->\n" + GetCall.ReceiveString);
                    }

                    if (NIMApp.ChartDataRef != null)
                    {
                        NIMApp.ChartDataRef.SetCurrent(Length);
                    }
                }
            }));
        }

        /// <summary>
        /// Protect the translation object being entered by the user from being changed
        /// </summary>
        /// <param name="Item"></param>
        /// <returns></returns>
        public static UnitContext<BaseUnit> BaseUnitStateChanged(string ID,BaseUnit Item, UnitTranslationState State)
        {
            if (State == UnitTranslationState.Queued)
            {
                UnitContext<BaseUnit> Sign = new UnitContext<BaseUnit>();
                Sign.Data = null;

                NIMApp.WorkWin.Dispatcher.Invoke(new Action(() => {
                    if (NIMApp.WorkWin != null)
                    {
                        for (int i = 0; i < NIMApp.WorkWin.TabViews.Children.Count; i++)
                        {
                            if (NIMApp.WorkWin.TabViews.Children[i] is TranslateView)
                            {
                                var Mod = (NIMApp.WorkWin.TabViews.Children[i] as TranslateView).Mod;

                                if (Mod.Path.Equals(ID))//The reason for using the file path as the primary key ID is that a user cannot translate two pieces of content with the same path at the same time, and there are also limitations in the outer layer I implemented..
                                {
                                    //This way, you can determine which Translator the BaseUnit belongs to by its ID, and then retrieve the ListView control itself based on the ModFile ~.
                                    var ListView = Mod.ListView;

                                    FakeGrid QueryGrid = ListView.KeyToFakeGrid(Item.Key);

                                    if (QueryGrid != null)
                                    {
                                        if (QueryGrid.Translated.Length == 0)
                                        {
                                            Sign.ControlSignal.Sign = 1;
                                            Sign.Data = Item;
                                        }
                                        else
                                        {
                                            Sign.ControlSignal.Sign = -1;
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }));

                if (Sign.Data != null)
                {
                    return Sign;
                }
                else
                {
                    return null;
                }
            }

           

            return new UnitContext<BaseUnit>();
        }

        public class RecvListener
        {
            public string Key = "";
            public List<int> ActiveIDs = new List<int>();
            public Action<int, object> Method = null;

            public RecvListener(string Key, List<int> ActiveIDs, Action<int, object> Func)
            {
                this.Key = Key;
                this.ActiveIDs = ActiveIDs;
                this.Method = Func;
            }
        }

        private static ReaderWriterLockSlim ListenersLock = new ReaderWriterLockSlim();
        public static void RemoveListener(string Key)
        {
            ListenersLock.EnterWriteLock();
            try
            {
                for (int i = 0; i < RecvListeners.Count; i++)
                {
                    if (RecvListeners[i].Key.Equals(Key))
                    {
                        RecvListeners.RemoveAt(i);
                        break;
                    }
                }
            }
            finally
            {
                ListenersLock.ExitWriteLock();
            }
        }

        public static void RegListener(string Key, List<int> ActiveIDs, Action<int, object> Action)
        {
            ListenersLock.EnterWriteLock();
            try
            {
                foreach (var Get in RecvListeners)
                {
                    if (Get.Key.Equals(Key))
                    {
                        return;
                    }
                }

                RecvListeners.Add(new RecvListener(Key, ActiveIDs, Action));
            }
            finally
            {
                ListenersLock.ExitWriteLock();
            }
        }

        public static List<RecvListener> RecvListeners = new List<RecvListener>();

        //Null = 0, CacheCall = 1, PreTranslateCall = 2, PlatformCall = 3, AICall = 5
        public static void Recv(int Sign, object Any)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    for (int i = 0; i < RecvListeners.Count; i++)
                    {
                        if (RecvListeners[i].ActiveIDs.Contains(Sign))
                        {
                            RecvListeners[i].Method.Invoke(Sign, Any);
                        }
                    }
                }
                catch { }
            });
        }

        public static void LogCall(string Log)
        {
            if (NIMApp.WorkWin != null)
            {
                NIMApp.WorkWin.Dispatcher.Invoke(new Action(() =>
                {
                    NIMApp.WorkWin.MainLog.Text = Log;
                }));
            }
        }
    }

    public class TranslatorHistoryCache
    {
        public DateTime ChangeTime;
        public string Translated = "";
        public bool IsCloud = false;

        public TranslatorHistoryCache(string Translated, bool IsCloud)
        {
            this.ChangeTime = DateTime.Now;
            this.Translated = Translated;
            this.IsCloud = IsCloud;
        }
    }
    public enum StateControl
    {
        Null = 0, Run = 1, Stop = 2, Cancel = 3
    }
}