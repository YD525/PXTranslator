using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NIM
{
    public class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get { return (GridLength)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }

        public GridLength To
        {
            get { return (GridLength)GetValue(ToProperty); }
            set { SetValue(ToProperty, value); }
        }

        public IEasingFunction EasingFunction { get; set; }

        public override Type TargetPropertyType
        {
            get { return typeof(GridLength); }
        }

        protected override Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public override object GetCurrentValue(object DefaultOriginValue, object DefaultDestinationValue, AnimationClock AnimationClock)
        {
            if (AnimationClock.CurrentProgress == null)
            {
                return From;
            }

            double Progress = AnimationClock.CurrentProgress.Value;

            if (EasingFunction != null)
            {
                Progress = EasingFunction.Ease(Progress);
            }

            double CurrentValue = From.Value + (To.Value - From.Value) * Progress;

            return new GridLength(CurrentValue, GridUnitType.Star);
        }
    }
}