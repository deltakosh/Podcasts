using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Podcasts
{
    public sealed partial class ReadOnlyEpisodeControl
    {
        public ReadOnlyEpisodeControl()
        {
            InitializeComponent();

            DisplayDuration = true;
        }

        public bool DisplayDuration
        {
            set
            {
                DeclaredDuration.Visibility = !value ? Visibility.Collapsed : Visibility.Visible;
                PodcastTitle.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var episode = DataContext as Episode;

            episode?.FallbackToPodcastPicture();
        }
    }
}
