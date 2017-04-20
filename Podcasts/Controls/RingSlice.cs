using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace Podcasts
{
    public class RingSlice : Path
    {
        private bool _isUpdating;

        #region StartAngle
        /// <summary>
        /// The start angle property.
        /// </summary>
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register(
                "StartAngle",
                typeof(double),
                typeof(RingSlice),
                new PropertyMetadata(
                    0d,
                    OnStartAngleChanged));

        /// <summary>
        /// Gets or sets the start angle.
        /// </summary>
        /// <value>
        /// The start angle.
        /// </value>
        public double StartAngle
        {
            get { return (double)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        private static void OnStartAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (RingSlice)sender;
            target.OnStartAngleChanged();
        }

        private void OnStartAngleChanged()
        {
            UpdatePath();
        }
        #endregion

        #region EndAngle
        /// <summary>
        /// The end angle property.
        /// </summary>
        public static readonly DependencyProperty EndAngleProperty =
            DependencyProperty.Register(
                "EndAngle",
                typeof(double),
                typeof(RingSlice),
                new PropertyMetadata(
                    0d,
                    OnEndAngleChanged));

        /// <summary>
        /// Gets or sets the end angle.
        /// </summary>
        /// <value>
        /// The end angle.
        /// </value>
        public double EndAngle
        {
            get { return (double)GetValue(EndAngleProperty); }
            set { SetValue(EndAngleProperty, value); }
        }

        public double AnimateEndAngleTo(double value, bool fromCurrentValue = false)
        {
            if (!fromCurrentValue)
            {
                EndAngle = StartAngle;
            }
            var animation = new DoubleAnimation
            {
                EnableDependentAnimation = true,
                To = Math.Min(359.99, value),
                From = fromCurrentValue ? EndAngle : StartAngle,
                Duration = new Duration(TimeSpan.FromMilliseconds(200))
            };
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, "EndAngle");

            var sb = new Storyboard();
            sb.Children.Add(animation);
            sb.Begin();

            if (animation.To != null)
            {
                return animation.To.Value;
            }
            return 0;
        }

        private static void OnEndAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (RingSlice)sender;
            target.OnEndAngleChanged();
        }

        private void OnEndAngleChanged()
        {
            UpdatePath();
        }
        #endregion

        #region Radius
        /// <summary>
        /// The radius property
        /// </summary>
        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register(
                "Radius",
                typeof(double),
                typeof(RingSlice),
                new PropertyMetadata(
                    0d,
                    OnRadiusChanged));

        /// <summary>
        /// Gets or sets the outer radius.
        /// </summary>
        /// <value>
        /// The outer radius.
        /// </value>
        public double Radius
        {
            get { return (double)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        private static void OnRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (RingSlice)sender;
            target.OnRadiusChanged();
        }

        private void OnRadiusChanged()
        {
            Width = Height = 2 * Radius;
            UpdatePath();
        }
        #endregion

        #region InnerRadius
        /// <summary>
        /// The inner radius property
        /// </summary>
        public static readonly DependencyProperty InnerRadiusProperty =
            DependencyProperty.Register(
                "InnerRadius",
                typeof(double),
                typeof(RingSlice),
                new PropertyMetadata(
                    0d,
                    OnInnerRadiusChanged));

        /// <summary>
        /// Gets or sets the inner radius.
        /// </summary>
        /// <value>
        /// The inner radius.
        /// </value>
        public double InnerRadius
        {
            get { return (double)GetValue(InnerRadiusProperty); }
            set { SetValue(InnerRadiusProperty, value); }
        }

        private static void OnInnerRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (RingSlice)sender;
            var newInnerRadius = (double)e.NewValue;
            target.OnInnerRadiusChanged(newInnerRadius);
        }

        private void OnInnerRadiusChanged(double newInnerRadius = 0)
        {
            if (newInnerRadius < 0)
            {
                throw new ArgumentException("InnerRadius can't be a negative value.", nameof(newInnerRadius));
            }

            UpdatePath();
        }
        #endregion

        #region Center
        /// <summary>
        /// Center Dependency Property
        /// </summary>
        public static readonly DependencyProperty CenterProperty =
            DependencyProperty.Register(
                "Center",
                typeof(Point?),
                typeof(RingSlice),
                new PropertyMetadata(null, OnCenterChanged));

        /// <summary>
        /// Gets or sets the Center property. This dependency property 
        /// indicates the center point.
        /// Center point is calculated based on Radius and StrokeThickness if not specified.    
        /// </summary>
        public Point? Center
        {
            get { return (Point?)GetValue(CenterProperty); }
            set { SetValue(CenterProperty, value); }
        }

        /// <summary>
        /// Handles changes to the Center property.
        /// </summary>
        /// <param name="d">
        /// The <see cref="DependencyObject"/> on which
        /// the property has changed value.
        /// </param>
        /// <param name="e">
        /// Event data that is issued by any event that
        /// tracks changes to the effective value of this property.
        /// </param>
        private static void OnCenterChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (RingSlice)d;
            target.OnCenterChanged();
        }

        private void OnCenterChanged()
        {
            UpdatePath();
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="RingSlice" /> class.
        /// </summary>
        public RingSlice()
        {
            SizeChanged += OnSizeChanged;
        }
        
        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            UpdatePath();
        }

        /// <summary>
        /// Suspends path updates until EndUpdate is called;
        /// </summary>
        public void BeginUpdate()
        {
            _isUpdating = true;
        }

        /// <summary>
        /// Resumes immediate path updates every time a component property value changes. Updates the path.
        /// </summary>
        public void EndUpdate()
        {
            _isUpdating = false;
            UpdatePath();
        }

        private void UpdatePath()
        {
            var innerRadius = InnerRadius + StrokeThickness / 2;
            var outerRadius = Radius - StrokeThickness / 2;

            if (_isUpdating ||
                ActualWidth == 0 ||
                innerRadius <= 0 ||
                outerRadius < innerRadius)
            {
                return;
            }

            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure {IsClosed = true};

            var center =
                Center ??
                new Point(
                    outerRadius + StrokeThickness / 2,
                    outerRadius + StrokeThickness / 2);

            // Starting Point
            pathFigure.StartPoint =
                new Point(
                    center.X + Math.Sin(StartAngle * Math.PI / 180) * innerRadius,
                    center.Y - Math.Cos(StartAngle * Math.PI / 180) * innerRadius);

            // Inner Arc
            var innerArcSegment = new ArcSegment
            {
                IsLargeArc = (EndAngle - StartAngle) >= 180.0,
                Point = new Point(
                    center.X + Math.Sin(EndAngle*Math.PI/180)*innerRadius,
                    center.Y - Math.Cos(EndAngle*Math.PI/180)*innerRadius),
                Size = new Size(innerRadius, innerRadius),
                SweepDirection = SweepDirection.Clockwise
            };

            var lineSegment =
                new LineSegment
                {
                    Point = new Point(
                        center.X + Math.Sin(EndAngle * Math.PI / 180) * outerRadius,
                        center.Y - Math.Cos(EndAngle * Math.PI / 180) * outerRadius)
                };

            // Outer Arc
            var outerArcSegment = new ArcSegment
            {
                IsLargeArc = (EndAngle - StartAngle) >= 180.0,
                Point = new Point(
                    center.X + Math.Sin(StartAngle*Math.PI/180)*outerRadius,
                    center.Y - Math.Cos(StartAngle*Math.PI/180)*outerRadius),
                Size = new Size(outerRadius, outerRadius),
                SweepDirection = SweepDirection.Counterclockwise
            };

            pathFigure.Segments.Add(innerArcSegment);
            pathFigure.Segments.Add(lineSegment);
            pathFigure.Segments.Add(outerArcSegment);
            pathGeometry.Figures.Add(pathFigure);
            InvalidateArrange();
            Data = pathGeometry;
        }
    }
}

