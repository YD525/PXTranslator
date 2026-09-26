using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NIM.YDControls
{
    public class RealtimeLineChart : FrameworkElement
    {
        #region Fields
        private readonly List<double> _DataPoints = new List<double>();
        private readonly System.Timers.Timer _Timer = new System.Timers.Timer();
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty MaxPointsProperty =
            DependencyProperty.Register("MaxPoints", typeof(int), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register("MinValue", typeof(double), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(double), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LineColorProperty =
            DependencyProperty.Register("LineColor", typeof(Color), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(Color.FromRgb(0, 200, 160), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register("LineThickness", typeof(double), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register("ShowGrid", typeof(bool), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowFillProperty =
            DependencyProperty.Register("ShowFill", typeof(bool), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridColorProperty =
            DependencyProperty.Register("GridColor", typeof(Color), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(Color.FromArgb(30, 255, 255, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register("BackgroundColor", typeof(Color), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(Color.FromRgb(12, 14, 20), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelColorProperty =
            DependencyProperty.Register("LabelColor", typeof(Color), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(Color.FromArgb(120, 255, 255, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RefreshIntervalProperty =
            DependencyProperty.Register("RefreshInterval", typeof(int), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(500, OnRefreshIntervalChanged));

        public static readonly DependencyProperty GridRowsProperty =
            DependencyProperty.Register("GridRows", typeof(int), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridColumnsProperty =
            DependencyProperty.Register("GridColumns", typeof(int), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowDotProperty =
            DependencyProperty.Register("ShowDot", typeof(bool), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register("Unit", typeof(string), typeof(RealtimeLineChart),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
        #endregion

        #region Properties
        public int MaxPoints { get { return (int)GetValue(MaxPointsProperty); } set { SetValue(MaxPointsProperty, value); } }
        public double MinValue { get { return (double)GetValue(MinValueProperty); } set { SetValue(MinValueProperty, value); } }
        public double MaxValue { get { return (double)GetValue(MaxValueProperty); } set { SetValue(MaxValueProperty, value); } }
        public Color LineColor { get { return (Color)GetValue(LineColorProperty); } set { SetValue(LineColorProperty, value); } }
        public double LineThickness { get { return (double)GetValue(LineThicknessProperty); } set { SetValue(LineThicknessProperty, value); } }
        public bool ShowGrid { get { return (bool)GetValue(ShowGridProperty); } set { SetValue(ShowGridProperty, value); } }
        public bool ShowFill { get { return (bool)GetValue(ShowFillProperty); } set { SetValue(ShowFillProperty, value); } }
        public Color GridColor { get { return (Color)GetValue(GridColorProperty); } set { SetValue(GridColorProperty, value); } }
        public Color BackgroundColor { get { return (Color)GetValue(BackgroundColorProperty); } set { SetValue(BackgroundColorProperty, value); } }
        public Color LabelColor { get { return (Color)GetValue(LabelColorProperty); } set { SetValue(LabelColorProperty, value); } }
        public int RefreshInterval { get { return (int)GetValue(RefreshIntervalProperty); } set { SetValue(RefreshIntervalProperty, value); } }
        public int GridRows { get { return (int)GetValue(GridRowsProperty); } set { SetValue(GridRowsProperty, value); } }
        public int GridColumns { get { return (int)GetValue(GridColumnsProperty); } set { SetValue(GridColumnsProperty, value); } }
        public bool ShowDot { get { return (bool)GetValue(ShowDotProperty); } set { SetValue(ShowDotProperty, value); } }
        public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
        public string Unit { get { return (string)GetValue(UnitProperty); } set { SetValue(UnitProperty, value); } }

        public double CurrentValue
        {
            get { return _DataPoints.Count > 0 ? _DataPoints[_DataPoints.Count - 1] : 0; }
        }

        public Action<RealtimeLineChart> OnTick { get; set; }
        #endregion

        #region Constructor
        public RealtimeLineChart()
        {
            _Timer.Interval = 1000;
            _Timer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnTick?.Invoke(this);
                });
            };

            Unloaded += (s, e) => _Timer.Stop();
        }
        #endregion

        #region Timer
        public void Start() { _Timer.Start(); }
        public void Stop() { _Timer.Stop(); }
      

        private static void OnRefreshIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RealtimeLineChart)d)._Timer.Interval = (int)e.NewValue;
        }
        #endregion

        #region Data API
        public void PushValue(double Value)
        {
            if (MaxValue > MinValue)
                Value = Math.Max(MinValue, Math.Min(MaxValue, Value));

            _DataPoints.Add(Value);

            while (_DataPoints.Count > MaxPoints)
                _DataPoints.RemoveAt(0);

            InvalidateVisual();
        }

        public void Clear()
        {
            _DataPoints.Clear();
            InvalidateVisual();
        }
        #endregion

        #region Range
        private void GetValueRange(out double Min, out double Max)
        {
            if (MaxValue > MinValue)
            {
                Min = MinValue;
                Max = MaxValue;
                return;
            }

            if (_DataPoints.Count == 0)
            {
                Min = 0;
                Max = 1;
                return;
            }

            Min = double.MaxValue;
            Max = double.MinValue;

            foreach (double V in _DataPoints)
            {
                if (V < Min) Min = V;
                if (V > Max) Max = V;
            }

            if (Math.Abs(Max - Min) < 0.0001)
                Max = Min + 1;

            double Padding = (Max - Min) * 0.1;
            Min -= Padding;
            Max += Padding;

            // Clamp to zero for non-negative data
            if (Min < 0)
                Min = 0;
        }
        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext Dc)
        {
            double W = ActualWidth;
            double H = ActualHeight;
            if (W <= 0 || H <= 0) return;

            const double PadLeft = 52, PadRight = 16, PadTop = 36, PadBottom = 36;

            double Cw = W - PadLeft - PadRight;
            double Ch = H - PadTop - PadBottom;
            if (Cw <= 0 || Ch <= 0) return;

            Dc.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, W, H));

            if (!string.IsNullOrEmpty(Title))
                Dc.DrawText(MakeText(Title, 13, FontWeights.SemiBold, new SolidColorBrush(Colors.White), 300),
                    new Point(PadLeft, 10));

            if (ShowGrid) DrawGrid(Dc, PadLeft, PadTop, Cw, Ch);
            DrawYLabels(Dc, PadLeft, PadTop, Ch);

            Dc.PushClip(new RectangleGeometry(new Rect(PadLeft, PadTop, Cw, Ch)));

            if (_DataPoints.Count >= 2)
                DrawLine(Dc, PadLeft, PadTop, Cw, Ch);

            Dc.Pop();
        }

        private void DrawGrid(DrawingContext Dc, double Left, double Top, double Cw, double Ch)
        {
            Pen Pen = new Pen(new SolidColorBrush(GridColor), 1);
            Pen.DashStyle = new DashStyle(new double[] { 4, 4 }, 0);
            Pen.Freeze();

            for (int I = 0; I <= GridRows; I++)
            {
                double Y = Top + Ch / GridRows * I;
                Dc.DrawLine(Pen, new Point(Left, Y), new Point(Left + Cw, Y));
            }

            double Step = Cw / GridColumns;

            for (int I = 0; I <= GridColumns; I++)
                Dc.DrawLine(Pen, new Point(Left + I * Step, Top), new Point(Left + I * Step, Top + Ch));
        }

        private void DrawLine(DrawingContext Dc, double Left, double Top, double Cw, double Ch)
        {
            Point[] Sp = BuildScreenPoints(Left, Top, Cw, Ch);
            int Count = Sp.Length;

            SolidColorBrush LineBrush = new SolidColorBrush(LineColor);
            LineBrush.Freeze();

            if (ShowFill)
            {
                StreamGeometry Geo = new StreamGeometry();
                using (StreamGeometryContext Ctx = Geo.Open())
                {
                    Ctx.BeginFigure(new Point(Sp[0].X, Top + Ch), true, true);
                    Ctx.LineTo(Sp[0], false, false);

                    for (int I = 1; I < Count; I++)
                    {
                        Point C1 = new Point((Sp[I - 1].X + Sp[I].X) / 2, Sp[I - 1].Y);
                        Point C2 = new Point((Sp[I - 1].X + Sp[I].X) / 2, Sp[I].Y);
                        Ctx.BezierTo(C1, C2, Sp[I], true, false);
                    }

                    Ctx.LineTo(new Point(Sp[Count - 1].X, Top + Ch), false, false);
                }

                Geo.Freeze();

                LinearGradientBrush FillBrush = new LinearGradientBrush(
                    Color.FromArgb(70, LineColor.R, LineColor.G, LineColor.B),
                    Color.FromArgb(0, LineColor.R, LineColor.G, LineColor.B),
                    new Point(0, 0), new Point(0, 1));

                FillBrush.Freeze();

                Dc.DrawGeometry(FillBrush, null, Geo);
            }

            StreamGeometry LineGeo = new StreamGeometry();

            using (StreamGeometryContext Ctx = LineGeo.Open())
            {
                Ctx.BeginFigure(Sp[0], false, false);

                for (int I = 1; I < Count; I++)
                {
                    Point C1 = new Point((Sp[I - 1].X + Sp[I].X) / 2, Sp[I - 1].Y);
                    Point C2 = new Point((Sp[I - 1].X + Sp[I].X) / 2, Sp[I].Y);
                    Ctx.BezierTo(C1, C2, Sp[I], true, false);
                }
            }

            LineGeo.Freeze();

            Pen LinePen = new Pen(LineBrush, LineThickness);
            LinePen.LineJoin = PenLineJoin.Round;
            LinePen.Freeze();

            Dc.DrawGeometry(null, LinePen, LineGeo);

            if (ShowDot)
            {
                Point Last = Sp[Count - 1];

                Dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, LineColor.R, LineColor.G, LineColor.B)), null, Last, 8, 8);
                Dc.DrawEllipse(LineBrush, null, Last, 4, 4);
                Dc.DrawEllipse(new SolidColorBrush(Colors.White), null, Last, 2, 2);
            }
        }

        private Point[] BuildScreenPoints(double Left, double Top, double Cw, double Ch)
        {
            int Count = _DataPoints.Count;

            double Min, Max;
            GetValueRange(out Min, out Max);

            double Range = Max - Min;
            double Step = Cw / Math.Max(MaxPoints - 1, 1);

            double Margin = 8;
            double UsableHeight = Ch - Margin * 2;

            Point[] Sp = new Point[Count];

            for (int I = 0; I < Count; I++)
            {
                double Normalized = (_DataPoints[I] - Min) / Range;

                double X = Left + I * Step;

                if (Count >= MaxPoints && I == Count - 1)
                {
                    X -= 5;
                }

                Sp[I] = new Point(X,Top + Margin + (1 - Normalized) * UsableHeight);
            }

            return Sp;
        }

        private void DrawYLabels(DrawingContext Dc, double Left, double Top, double Ch)
        {
            double Min, Max;
            GetValueRange(out Min, out Max);

            double Range = Max - Min;
            SolidColorBrush Brush = new SolidColorBrush(LabelColor);

            for (int I = 0; I <= GridRows; I++)
            {
                double Val = Max - Range / GridRows * I;
                double Y = Top + Ch / GridRows * I;

                var Tf = MakeText(FormatVal(Val), 10, FontWeights.Normal, Brush, 44);
                Dc.DrawText(Tf, new Point(Left - Tf.Width - 8, Y - Tf.Height / 2));
            }
        }

        private FormattedText MakeText(string Text, double Size, FontWeight Weight, Brush Brush, double MaxW)
        {
#pragma warning disable CS0618
            return new FormattedText(Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, Weight, FontStretches.Normal),
                Size, Brush)
            { MaxTextWidth = MaxW, MaxTextHeight = 40 };
#pragma warning restore CS0618
        }

        private static string FormatVal(double V)
        {
            return Math.Abs(V) >= 1000 ? (V / 1000).ToString("F1") + "k" : V.ToString("F0");
        }

        #endregion
    }
}