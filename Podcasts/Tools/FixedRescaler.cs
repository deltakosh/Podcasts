using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Podcasts
{
    public class FixedRescaler : DependencyObject
    {
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached("FontSize", typeof(double), typeof(FixedRescaler), new PropertyMetadata(true, FontSizeCallback));
        public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached("Width", typeof(double), typeof(FixedRescaler), new PropertyMetadata(true, WidthCallback));
        public static readonly DependencyProperty HeightProperty = DependencyProperty.RegisterAttached("Height", typeof(double), typeof(FixedRescaler), new PropertyMetadata(true, HeightCallback));

        private static void FontSizeCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var fontSize = (double)e.NewValue;
            var scaledFontSize = fontSize * Ratio;

            var control = d as Control;
            if (control != null)
            {
                control.FontSize = scaledFontSize;
                return;
            }

            var textblock = d as TextBlock;
            if (textblock != null)
            {
                textblock.FontSize = scaledFontSize;
                return;
            }

            var fontIcon = d as FontIcon;
            if (fontIcon != null)
            {
                fontIcon.FontSize = scaledFontSize;
            }
        }

        private static void WidthCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var width = (double)e.NewValue;
            var scaledWidth = width * Ratio;

            var element = d as FrameworkElement;
            if (element != null)
            {
                element.Width = scaledWidth;
            }
        }
        private static void HeightCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var height = (double)e.NewValue;
            var scaledHeight = height * Ratio;

            var element = d as FrameworkElement;
            if (element != null)
            {
                element.Height = scaledHeight;
            }
        }

        public static double Ratio
        {
            get
            {
                if (CoreTools.IsRunningOnMobile)
                {
                    var currentWidth = CoreWindow.GetForCurrentThread().Bounds.Width;

                    if (currentWidth >= 480)
                    {
                        return 1.0;
                    }
                    return Math.Max(0.5, currentWidth / 480.0);
                }

                return 1.0;
            }
        }        

        public static void SetFontSize(UIElement element, double value)
        {
            element.SetValue(FontSizeProperty, value);
        }

        public static double GetFontSize(UIElement element)
        {
            return (double)element.GetValue(FontSizeProperty);
        }
      
        public static void SetWidth(UIElement element, double value)
        {
            element.SetValue(WidthProperty, value);
        }

        public static double GetWidth(UIElement element)
        {
            return (double)element.GetValue(WidthProperty);
        }

        public static void SetHeight(UIElement element, double value)
        {
            element.SetValue(HeightProperty, value);
        }

        public static double GetHeight(UIElement element)
        {
            return (double)element.GetValue(HeightProperty);
        }
    }

}
