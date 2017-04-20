using System;
using Windows.UI.Text;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class ProgressToIndeterminateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null || (int)value == 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
