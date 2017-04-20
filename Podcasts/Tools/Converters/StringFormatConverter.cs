using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class StringFormatConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // No format provided.
            if (parameter == null)
            {
                return value;
            }

            return string.Format((string)parameter, value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }

}
