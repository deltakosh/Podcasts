using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class IsInLibraryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var feedUrl = value.ToString();

            return Library.ContainsFeedUrl(feedUrl) ? Visibility.Visible :Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
