using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Podcasts
{
    public class WebviewNavigator : DependencyObject
    {
        public static readonly DependencyProperty ContentProperty = DependencyProperty.RegisterAttached("Content", typeof(string), typeof(WebviewNavigator), new PropertyMetadata("", ContentCallback));

        private static void ContentCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var content = (string)e.NewValue;

            var webview = d as WebView;
 
            webview?.NavigateToString($"<html><style>*{{font-family:Segoe UI Light; background-color: #EAEAEA;overflow-x: hidden}}</style><body>" + content + "</body></html>");
        }

        public static void SetContent(UIElement element, string value)
        {
            element.SetValue(ContentProperty, value);
        }

        public static string GetContent(UIElement element)
        {
            return (string)element.GetValue(ContentProperty);
        }      
    }

}
