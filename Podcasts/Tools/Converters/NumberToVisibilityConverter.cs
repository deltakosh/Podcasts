using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class NumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || ((int)value) == 0)
            {
                return parameter != null ? Visibility.Visible : Visibility.Collapsed;
            }

            return parameter != null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
