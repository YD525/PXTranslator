using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace NIM
{
    public partial class NIMRadarChart : UserControl
    {
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(450);

        // ---- Public values (set these from XAML / code) ----

        public static readonly DependencyProperty TranslationQualityProperty =
            DependencyProperty.Register("TranslationQuality", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(80.0, OnValueChanged));

        public static readonly DependencyProperty TranslationSpeedProperty =
            DependencyProperty.Register("TranslationSpeed", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(70.0, OnValueChanged));

        public static readonly DependencyProperty ResourceOverheadProperty =
            DependencyProperty.Register("ResourceOverhead", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(50.0, OnValueChanged));

        public static readonly DependencyProperty AutomationProperty =
            DependencyProperty.Register("Automation", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(60.0, OnValueChanged));

        public static readonly DependencyProperty ManualizationProperty =
            DependencyProperty.Register("Manualization", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(40.0, OnValueChanged));

        // Switch for the grid-ring labels: false = A/B/C/D (default), true = 80/60/40/20
        public static readonly DependencyProperty ShowLevelValuesProperty =
            DependencyProperty.Register("ShowLevelValues", typeof(bool), typeof(NIMRadarChart),
                new PropertyMetadata(false, OnShowLevelValuesChanged));

        public bool ShowLevelValues
        {
            get { return (bool)GetValue(ShowLevelValuesProperty); }
            set { SetValue(ShowLevelValuesProperty, value); }
        }

        private static readonly string[] LevelLetters = { "A", "B", "C", "D" };

        private static void OnShowLevelValuesChanged(DependencyObject D, DependencyPropertyChangedEventArgs E)
        {
            ((NIMRadarChart)D).UpdateRadar();
        }

        // ---- Internal animated values that actually drive the drawing ----

        private static readonly DependencyProperty DisplayTranslationQualityProperty =
            DependencyProperty.Register("DisplayTranslationQuality", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(80.0, OnDisplayValueChanged));

        private static readonly DependencyProperty DisplayTranslationSpeedProperty =
            DependencyProperty.Register("DisplayTranslationSpeed", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(70.0, OnDisplayValueChanged));

        private static readonly DependencyProperty DisplayResourceOverheadProperty =
            DependencyProperty.Register("DisplayResourceOverhead", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(50.0, OnDisplayValueChanged));

        private static readonly DependencyProperty DisplayAutomationProperty =
            DependencyProperty.Register("DisplayAutomation", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(60.0, OnDisplayValueChanged));

        private static readonly DependencyProperty DisplayManualizationProperty =
            DependencyProperty.Register("DisplayManualization", typeof(double), typeof(NIMRadarChart),
                new PropertyMetadata(40.0, OnDisplayValueChanged));

        // Maps a public target property to the display property that gets animated toward it
        private static readonly Dictionary<DependencyProperty, DependencyProperty> DisplayMap =
            new Dictionary<DependencyProperty, DependencyProperty>
            {
                { TranslationQualityProperty, DisplayTranslationQualityProperty },
                { TranslationSpeedProperty, DisplayTranslationSpeedProperty },
                { ResourceOverheadProperty, DisplayResourceOverheadProperty },
                { AutomationProperty, DisplayAutomationProperty },
                { ManualizationProperty, DisplayManualizationProperty },
            };

        public double TranslationQuality
        {
            get { return (double)GetValue(TranslationQualityProperty); }
            set { SetValue(TranslationQualityProperty, Clamp(value, 0, 100)); }
        }

        public double TranslationSpeed
        {
            get { return (double)GetValue(TranslationSpeedProperty); }
            set { SetValue(TranslationSpeedProperty, Clamp(value, 0, 100)); }
        }

        public double ResourceOverhead
        {
            get { return (double)GetValue(ResourceOverheadProperty); }
            set { SetValue(ResourceOverheadProperty, Clamp(value, 0, 100)); }
        }

        public double Automation
        {
            get { return (double)GetValue(AutomationProperty); }
            set { SetValue(AutomationProperty, Clamp(value, 0, 100)); }
        }

        public double Manualization
        {
            get { return (double)GetValue(ManualizationProperty); }
            set { SetValue(ManualizationProperty, Clamp(value, 0, 100)); }
        }

        private static void OnValueChanged(DependencyObject D, DependencyPropertyChangedEventArgs E)
        {
            NIMRadarChart Chart = (NIMRadarChart)D;
            DependencyProperty DisplayProperty = DisplayMap[E.Property];

            double From = (double)Chart.GetValue(DisplayProperty);
            double To = (double)E.NewValue;

            DoubleAnimation Animation = new DoubleAnimation(From, To, AnimationDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Chart.BeginAnimation(DisplayProperty, Animation);
        }

        private static void OnDisplayValueChanged(DependencyObject D, DependencyPropertyChangedEventArgs E)
        {
            ((NIMRadarChart)D).UpdateRadar();
        }

        private const double CenterX = 280;
        private const double CenterY = 220;
        private const double Radius = 130;

        public NIMRadarChart()
        {
            InitializeComponent();
            Loaded += (S, E) => UpdateRadar();
        }

        private void UpdateRadar()
        {
            double[] Angles = { Math.PI / 2, Math.PI / 2 + 2 * Math.PI / 5, Math.PI / 2 + 4 * Math.PI / 5, Math.PI / 2 + 6 * Math.PI / 5, Math.PI / 2 + 8 * Math.PI / 5 };
            double[] Values =
            {
                (double)GetValue(DisplayTranslationQualityProperty) / 100.0,
                (double)GetValue(DisplayManualizationProperty) / 100.0,
                (double)GetValue(DisplayAutomationProperty) / 100.0,
                (double)GetValue(DisplayResourceOverheadProperty) / 100.0,
                (double)GetValue(DisplayTranslationSpeedProperty) / 100.0,
            };

            Point[] DataPoints = new Point[5];
            for (int I = 0; I < 5; I++)
            {
                double X = CenterX + Radius * Values[I] * Math.Cos(Angles[I]);
                double Y = CenterY - Radius * Values[I] * Math.Sin(Angles[I]);
                DataPoints[I] = new Point(X, Y);
            }
            DataPolygon.Points = new PointCollection(DataPoints);

            double[] Levels = { 1.0, 0.8, 0.6, 0.4, 0.2 };
            Polygon[] GridPolygons = { GridOuter, GridA, GridB, GridC, GridD };
            for (int J = 0; J < Levels.Length; J++)
            {
                Point[] Pts = new Point[5];
                for (int I = 0; I < 5; I++)
                {
                    double R = Radius * Levels[J];
                    double X = CenterX + R * Math.Cos(Angles[I]);
                    double Y = CenterY - R * Math.Sin(Angles[I]);
                    Pts[I] = new Point(X, Y);
                }
                GridPolygons[J].Points = new PointCollection(Pts);
            }

            Point[] AxisEnds = new Point[5];
            for (int I = 0; I < 5; I++)
            {
                double X = CenterX + Radius * Math.Cos(Angles[I]);
                double Y = CenterY - Radius * Math.Sin(Angles[I]);
                AxisEnds[I] = new Point(X, Y);
            }
            AxisTop.X1 = CenterX; AxisTop.Y1 = CenterY; AxisTop.X2 = AxisEnds[0].X; AxisTop.Y2 = AxisEnds[0].Y;
            AxisTopRight.X1 = CenterX; AxisTopRight.Y1 = CenterY; AxisTopRight.X2 = AxisEnds[1].X; AxisTopRight.Y2 = AxisEnds[1].Y;
            AxisBottomRight.X1 = CenterX; AxisBottomRight.Y1 = CenterY; AxisBottomRight.X2 = AxisEnds[2].X; AxisBottomRight.Y2 = AxisEnds[2].Y;
            AxisBottomLeft.X1 = CenterX; AxisBottomLeft.Y1 = CenterY; AxisBottomLeft.X2 = AxisEnds[3].X; AxisBottomLeft.Y2 = AxisEnds[3].Y;
            AxisTopLeft.X1 = CenterX; AxisTopLeft.Y1 = CenterY; AxisTopLeft.X2 = AxisEnds[4].X; AxisTopLeft.Y2 = AxisEnds[4].Y;

            double[] LabelOffsets = { 0.8, 0.6, 0.4, 0.2 };
            TextBlock[] Labels = { LabelA, LabelB, LabelC, LabelD };
            double TopAngle = Math.PI / 2;
            for (int K = 0; K < LabelOffsets.Length; K++)
            {
                double R = Radius * LabelOffsets[K];
                double X = CenterX + R * Math.Cos(TopAngle);
                double Y = CenterY - R * Math.Sin(TopAngle);
                Labels[K].Text = ShowLevelValues ? ((int)Math.Round(LabelOffsets[K] * 100)).ToString() : LevelLetters[K];
                Canvas.SetLeft(Labels[K], X - 8);
                Canvas.SetTop(Labels[K], Y - 8);
            }
        }

        private double Clamp(double Value, double Min, double Max)
        {
            if (Value < Min) return Min;
            if (Value > Max) return Max;
            return Value;
        }
    }
}