using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Podcasts
{
    public static class AnimationTools
    {
        public static void AnimateDouble(DependencyObject target, string path, double to, double duration, Action onCompleted = null, bool forever = false, bool elastic = false)
        {
            var animation = new DoubleAnimation
            {
                EnableDependentAnimation = true,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(duration))
            };

            if (elastic)
            {
                animation.EasingFunction = new ElasticEase();
            }

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, path);

            if (forever)
            {
                animation.RepeatBehavior = RepeatBehavior.Forever;
            }

            var sb = new Storyboard();
            sb.Children.Add(animation);

            if (onCompleted != null)
            {
                sb.Completed += (s, e) =>
                {
                    onCompleted();
                };
            }

            sb.Begin();
        }
    }
}
