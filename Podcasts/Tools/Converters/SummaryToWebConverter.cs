using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Podcasts
{
    public class SummaryToWebConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "";
            }

            var backgroundColor = Application.Current.Resources["BackgroundColor"];
            var textColor = Application.Current.Resources["TextColor"];

            var style = $"<style>* {{background-color:{backgroundColor.ToString().Replace("#FF", "#")}; color: {textColor.ToString().Replace("#FF", "#")};}}</style>\n";
            return style + value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
