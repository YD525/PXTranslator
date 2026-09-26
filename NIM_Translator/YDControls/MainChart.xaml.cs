using System;
using System.Windows.Controls;
using NIM.UIManagement;

namespace NIM.YDControls
{
    /// <summary>
    /// Interaction logic for MainChart.xaml
    /// </summary>
    public partial class MainChart : UserControl
    {
        public MainChart()
        {
            InitializeComponent();
        }

        private void BtnPause_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DataRef.Paused = !DataRef.Paused;
            BtnPause.Content = DataRef.Paused ? "▶  RESUME" : "⏸  PAUSE";
        }

        private void BtnClear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Clear();
        }

        public void Clear()
        {
            TokenChart.Clear();
            TotalTokenChart.Clear();
        }

        public ChartData DataRef = null;

        public void SetAction(Action<RealtimeLineChart> Current, Action<RealtimeLineChart> Total,ChartData DataRef)
        {
            this.DataRef = DataRef;

            if (Current != null)
            {
                TokenChart.OnTick += new Action<RealtimeLineChart>((Ref) =>
                {
                    if(!DataRef.Paused)
                    Current.Invoke(Ref);
                });
            }

            if (Total != null)
            {
                TotalTokenChart.OnTick += new Action<RealtimeLineChart>((Ref) =>
                {
                    if (!DataRef.Paused)
                    Total.Invoke(Ref);
                });
            }
        }

        public void Start()
        {
            if (TokenChart.OnTick != null)
            {
                TokenChart.Start();
            }

            if (TotalTokenChart.OnTick != null)
            {
                TotalTokenChart.Start();
            }
        }

        public void Stop()
        {
            if (TokenChart.OnTick != null)
            {
                TokenChart.Stop();
            }

            if (TotalTokenChart.OnTick != null)
            {
                TotalTokenChart.Stop();
            }
        }
    }
}
