using System;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class SecondsToTimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var result = TimeSpan.FromSeconds((int)(double) value).ToString("c");

            if (parameter != null)
            {
                result = "-" + result;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
