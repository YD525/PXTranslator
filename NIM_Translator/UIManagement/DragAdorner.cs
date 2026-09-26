using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;

namespace NIM.UIManagement
{
    public class DragAdorner : Adorner
    {
        private readonly Border _Visual;
        private readonly TranslateTransform _Transform = new TranslateTransform();

        public DragAdorner(UIElement AdornedElement, UIElement DragSource, Point StartPosition) : base(AdornedElement)
        {
            Size Size = DragSource.RenderSize;

            _Visual = new Border
            {
                Width = Size.Width,
                Height = Size.Height,
                Background = new VisualBrush(DragSource) { Stretch = Stretch.None },
                Opacity = 0.85,
                RenderTransform = _Transform,
                IsHitTestVisible = false
            };

            _Transform.X = StartPosition.X - Size.Width / 2;
            _Transform.Y = StartPosition.Y - Size.Height / 2;

            AddVisualChild(_Visual);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int Index) => _Visual;

        protected override Size MeasureOverride(Size ConstraintSize)
        {
            _Visual.Measure(ConstraintSize);
            return _Visual.DesiredSize;
        }

        protected override Size ArrangeOverride(Size FinalSize)
        {
            _Visual.Arrange(new Rect(FinalSize));
            return FinalSize;
        }

        public void UpdatePosition(Point Position)
        {
            _Transform.X = Position.X - _Visual.Width / 2;
            _Transform.Y = Position.Y - _Visual.Height / 2;
        }
    }
}
