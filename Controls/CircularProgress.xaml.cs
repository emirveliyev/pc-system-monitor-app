using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace pc_system_monitor_app.Controls
{
    public partial class CircularProgress : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularProgress), new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(CircularProgress), new PropertyMetadata(100.0));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(CircularProgress), new PropertyMetadata(""));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register("AccentBrush", typeof(System.Windows.Media.Brush), typeof(CircularProgress),
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80))));

        private double _current = 0;

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public System.Windows.Media.Brush AccentBrush
        {
            get => (System.Windows.Media.Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public CircularProgress()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LabelText.Text = Label;
                DrawArc(0);
            };
            SizeChanged += (s, e) => DrawArc(_current);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = d as CircularProgress;
            if (c == null) return;
            double newVal = Math.Max(0, Math.Min(c.Maximum, (double)e.NewValue));
            c.AnimateTo(newVal);
        }

        private void AnimateTo(double to)
        {
            var anim = new DoubleAnimation(_current, to, new Duration(TimeSpan.FromMilliseconds(500)))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (s, e) => { _current = to; };
            BeginAnimation(DummyProperty, anim);
        }

        private static readonly DependencyProperty DummyProperty =
            DependencyProperty.Register("Dummy", typeof(double), typeof(CircularProgress),
                new PropertyMetadata(0.0, OnDummyChanged));

        private static void OnDummyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as CircularProgress;
            if (control == null) return;
            double now = 0;
            try { now = (double)e.NewValue; } catch { now = 0; }
            if (control.ValueText != null) control.ValueText.Text = $"{now:F0}%";
            control.DrawArc(now);
        }

        private void DrawArc(double value)
        {
            if (ArcCanvas == null) return;
            ArcCanvas.Children.Clear();
            double size = Math.Min(ArcCanvas.ActualWidth > 0 ? ArcCanvas.ActualWidth : 150, ArcCanvas.ActualHeight > 0 ? ArcCanvas.ActualHeight : 150);
            double cx = size / 2;
            double cy = size / 2;
            double radius = size / 2 - 8;
            double angle = 360.0 * (value / Math.Max(1, Maximum));

            var bg = new Path
            {
                Data = new EllipseGeometry(new System.Windows.Point(cx, cy), radius, radius),
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                StrokeThickness = 8,
                Opacity = 0.7
            };

            ArcCanvas.Width = size;
            ArcCanvas.Height = size;
            ArcCanvas.Children.Add(bg);

            if (angle <= 0) return;

            double startAngle = -90;
            double endAngle = startAngle + angle;
            bool large = angle > 180;
            System.Windows.Point p1 = PointOnCircle(cx, cy, radius, startAngle);
            System.Windows.Point p2 = PointOnCircle(cx, cy, radius, endAngle);

            var pf = new PathFigure { StartPoint = p1 };
            var seg = new ArcSegment
            {
                Point = p2,
                Size = new System.Windows.Size(radius, radius),
                IsLargeArc = large,
                SweepDirection = SweepDirection.Clockwise
            };
            pf.Segments.Add(seg);

            var pg = new PathGeometry();
            pg.Figures.Add(pf);

            var path = new Path
            {
                Data = pg,
                Stroke = AccentBrush,
                StrokeThickness = 8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.45 }
            };

            ArcCanvas.Children.Add(path);
        }

        private static System.Windows.Point PointOnCircle(double cx, double cy, double r, double angleDeg)
        {
            double a = angleDeg * Math.PI / 180.0;
            return new System.Windows.Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }

        public void SetAvgText(string text)
        {
            if (AvgText != null) AvgText.Text = text;
        }
    }
}