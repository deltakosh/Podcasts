using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Podcasts
{
    public sealed partial class EpisodeControl
    {
        public EpisodeControl()
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

        private void Play_OnClick(object sender, EventArgs e)
        {
            var episode = DataContext as Episode;

            if (episode == null)
            {
                return;
            }

            MediaPlayerHost.ResetLastOrderReceived();

            Playlist.CurrentPlaylist.PlayEpisode(episode);
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var episode = DataContext as Episode;
            episode?.FallbackToPodcastPicture();
        }

        private void EpisodeSlidableListItem_LeftCommandRequested(object sender, EventArgs e)
        {
            var episode = DataContext as Episode;

            if (episode == null)
            {
                return;
            }

            episode.IsPlayed = !episode.IsPlayed;
        }

        private void EpisodeSlidableListItem_RightCommandRequested(object sender, EventArgs e)
        {
            var episode = DataContext as Episode;

            if (episode == null)
            {
                return;
            }

            Playlist.CurrentPlaylist.AppendEpisode(episode);
        }
    }
}
