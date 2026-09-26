using System;
using NIM.TranslateManage;

namespace NIM.UIManagement
{
    public class ChartData
    {
        public bool Paused = false;
        private double Current = 0;
        public long Total = 0;

        private long SingleUseLimit = 0;
        public double SetCurrent(long Current)
        {
            try
            {
                if (!Paused)
                {
                    this.Current = Current;
                    Total += Current;
                    CheckLimit();
                    return this.Current;
                }

                return 0;
            }
            catch { return 0; }
        }

        public double GetCurrent()
        {
            try
            {
                var CurrentTemp = this.Current;
                this.Current = 0;
                return CurrentTemp;
            }
            catch { return 0; }
        }

        public void ReSet()
        {
            this.Current = this.Total = 0;
        }

        // Stops translation when token usage exceeds the set limit.
        // This is only an approximate protection and may not match real costs,
        // since each platform calculates token usage differently.
        // For accurate cost control, please set limits in your service provider's dashboard.
        public void CheckLimit()
        {
            if (this.SingleUseLimit != 0)
                if (this.Total > this.SingleUseLimit)
                {
                    //If the token limit is exceeded, the fuse will trip, forcibly terminating the translation process.
                    //DeFine.WorkWin.ActiveTab.Mod.TranslationStatus = StateControl.Cancel;

                    //DeFine.WorkWin.ActiveTab.Mod.SyncTransState(new Action(() =>
                    //{
                    //    DeFine.WorkWin.Dispatcher.Invoke(new Action(() =>
                    //    {
                    //        //DeFine.WorkWin.SyncTransStateUI();
                    //    }));
                    //}), false);
                }
        }

        public void SetTokenUseLimit(long MaxToken)
        {
            this.SingleUseLimit = MaxToken;
        }
    }
}
