using System;
using Windows.UI.Text;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || ((bool)value) == false)
            {
                return parameter != null ? FontWeights.Normal : FontWeights.Bold;
            }

            return parameter != null ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
