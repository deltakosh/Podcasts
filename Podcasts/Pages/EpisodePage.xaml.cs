using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public sealed partial class EpisodePage
    {
        Episode episode;

        public EpisodePage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;

            NoEpisode.Visibility = Visibility.Collapsed;
            IsPlayedTag.Visibility = Visibility.Collapsed;
            IsDownloadedTag.Visibility = Visibility.Collapsed;

            OneDriveSync.DataContext = OneDriveSettings.Instance;
        }

        internal override void ClearReferences()
        {
            Playlist.CurrentPlaylist.OnCurrentIndexChanged -= CurrentPlaylist_OnCurrentIndexChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            WaitRingManager.IsWaitRingVisible = true;

            NoEpisode.Visibility = Visibility.Collapsed;
            CommandBar.Visibility = Visibility.Collapsed;
            Details.Visibility = Visibility.Collapsed;
            HeaderProgress.Visibility = Visibility.Collapsed;

            bool readOnly = false;

            if (IsTopStackValueString())
            {
                var data = GetTopStackValueAsString();

                if (data.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    episode = Library.GetEpisodeByEnclosure(data);
                    GlobalStateManager.SelectedMenuIndex = 3;
                }
                else
                {
                    episode = Episode.FromCompleteString(data);
                    GlobalStateManager.SelectedMenuIndex = 0;
                    readOnly = true;
                }
                PlayButton.Visibility = Visibility.Visible;
                AddToPlaylistButton.Visibility = Visibility.Visible;
            }
            else
            {
                episode = Playlist.CurrentPlaylist.CurrentEpisode;
                GlobalStateManager.SelectedMenuIndex = 1;
                Playlist.CurrentPlaylist.OnCurrentIndexChanged += CurrentPlaylist_OnCurrentIndexChanged;
                PlayButton.Visibility = Visibility.Collapsed;
                AddToPlaylistButton.Visibility = Visibility.Collapsed;
            }

            DataContext = episode;

            if (episode == null)
            {
                NoEpisode.Visibility = Visibility.Visible;
                IsPlayedTag.Visibility = Visibility.Collapsed;
                IsDownloadedTag.Visibility = Visibility.Collapsed;

                Playlist.CurrentPlaylist.OnCurrentIndexChanged += CurrentPlaylist_OnCurrentIndexChanged;
            }
            else
            {
                HeaderProgress.Visibility = Visibility.Visible;
                CommandBar.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
                Details.Visibility = Visibility.Visible;
            }
            WaitRingManager.IsWaitRingVisible = false;

            if (episode != null && string.IsNullOrEmpty(episode.Subtitle))
            {
                WaitBar.Visibility = Visibility.Visible;
                await Library.WaitFullRefreshAsync();
                WaitBar.Visibility = Visibility.Collapsed;
            }
        }

        private void RootPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void CurrentPlaylist_OnCurrentIndexChanged()
        {
            episode = Playlist.CurrentPlaylist.CurrentEpisode;
            if (episode != null)
            {
                NoEpisode.Visibility = Visibility.Collapsed;
                HeaderProgress.Visibility = Visibility.Visible;
                CommandBar.Visibility = Visibility.Visible;
                Details.Visibility = Visibility.Visible;
                TitleGrid.Visibility = Visibility.Visible;

                DataContext = episode;
            }
            else
            {
                NoEpisode.Visibility = Visibility.Visible;
                TitleGrid.Visibility = Visibility.Collapsed;
                CommandBar.Visibility = Visibility.Collapsed;
                Details.Visibility = Visibility.Collapsed;

                DataContext = null;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (episode == null)
            {
                return;
            }
            MediaPlayerHost.ResetLastOrderReceived();
            Playlist.CurrentPlaylist.PlayEpisode(episode);
        }

        private void MarkAsPlayed_OnClick(object sender, RoutedEventArgs e)
        {
            if (episode == null)
            {
                return;
            }
            episode.IsPlayed = true;
        }

        private void MarkAsUnplayed_OnClick(object sender, RoutedEventArgs e)
        {
            if (episode == null)
            {
                return;
            }
            episode.IsPlayed = false;
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            episode?.DownloadAsync();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                episode?.FallbackToPodcastPicture();
            }
        }

        private void AddToPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.AppendEpisode(episode);
        }

        private void DeleteDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            episode?.DeleteDownload();
        }

        private void StopDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            episode?.CancelDownload();
        }

        private async void WebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            if (args.Uri == null)
            {
                return;
            }

            args.Cancel = true;

            await Launcher.LaunchUriAsync(args.Uri);
        }
    }
}
