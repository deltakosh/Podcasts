using System;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || ((bool)value) == false)
            {
                return parameter != null ? 0.8 : 1.0;
            }

            return parameter != null ? 1.0 : 0.8;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
