using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI;

namespace Podcasts
{
    public sealed partial class PodcastPage
    {
        Podcast podcast;
        Episode episodeToShare;

        public PodcastPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        private void EpisodesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var episode = e.ClickedItem as Episode;

            if (episode == null)
            {
                return;
            }

            GlobalStateManager.CurrentShell.Navigate(typeof(EpisodePage), episode.InLibrary ? episode.Enclosure : episode.ToCompleteString());
        }

        private void AddFlyout_OnOpening(object sender, object e)
        {
            var flyout = sender as Flyout;
            if (flyout != null && podcast != null)
            {
                var editor = ((FrameworkElement)flyout.Content).FindDescendantByName("AddToMyPodcastsEditor") as AddToMyPodcasts;
                editor?.SetPodcast(podcast);
            }
        }

        void Sync()
        {
            EpisodesListView.ItemsSource = LocalSettings.Instance.OnlyUnplayedEpisodes ? podcast.Episodes.Where(ep => !ep.IsPlayed) : podcast.Episodes;
        }

        async void OnLibraryLoaded()
        {
            WaitRingManager.IsWaitRingVisible = true;

            try
            {
                if (Library.ContainsFeedUrl(GetTopStackValueAsString()))
                {
                    podcast = Library.GetPodcastByFeedUrl(GetTopStackValueAsString());
                    GlobalStateManager.SelectedMenuIndex = 3;
                }
                else
                {
                    podcast = Podcast.FromString(GetTopStackValueAsString());

                    if (Library.ContainsFeedUrl(podcast.FeedUrl))
                    {
                        podcast = Library.GetPodcastByFeedUrl(podcast.FeedUrl);
                        GlobalStateManager.SelectedMenuIndex = 3;
                    }
                    else
                    {
                        await podcast.RefreshAsync(false, false, false);
                        GlobalStateManager.SelectedMenuIndex = 0;
                        EpisodesListView.ItemTemplate = (DataTemplate)Resources["ReadOnly"];
                    }

                }

                DataContext = null;
                DataContext = podcast;

                Sync();

                if (podcast.Episodes.Count != 0)
                {
                    CommandBar.Visibility = Visibility.Visible;
                    WaitRingManager.IsWaitRingVisible = false;
                    return;
                }

                await podcast.RefreshAsync(false, false, false);
                CommandBar.Visibility = Visibility.Visible;
            }
            catch
            {
                DataContext = null;
            }
            
          
            WaitRingManager.IsWaitRingVisible = false;
        }

        private async void PodcastPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            CommandBar.Visibility = Visibility.Collapsed;
            SyncFilterButtonText();
            await Library.WaitReadyAsync();
            OnLibraryLoaded();
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            await podcast.RefreshAsync(false, false, true);
            Sync();
            WaitRingManager.IsWaitRingVisible = false;
        }

        private void MarkAsPlayedButton_OnClick(object sender, RoutedEventArgs e)
        {
            podcast.MarkAsPlayed();
        }

        private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            await WaitRingManager.ShowBlurBackground(true);

            await Library.DeletePodcast(podcast);

            GlobalStateManager.CurrentShell.Navigate(typeof(LibraryPage));
            await WaitRingManager.ShowBlurBackground(false);
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;
            episode?.FallbackToPodcastPicture();
        }

        void MarkAsPlayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                episode.IsPlayed = true;
            }
        }

        private void MarkAsUnplayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                episode.IsPlayed = false;
            }
        }

        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var menu = (MenuFlyout)RootGrid.Resources["MenuFlyout"];

            var startGrid = (EpisodeControl)sender;
            menu.ShowAt(startGrid, e.GetPosition(startGrid));

            var episode = startGrid.DataContext as Episode;

            if (episode == null)
            {
                return;
            }
            
            MarkAsPlayedMenu.Visibility = !episode.InLibrary || episode.IsPlayed ? Visibility.Collapsed : Visibility.Visible;
            MarkAsUnplayedMenu.Visibility = episode.IsPlayed ? Visibility.Visible : Visibility.Collapsed;

            DownloadMenu.Visibility = episode.CanBeDownloaded ? Visibility.Visible : Visibility.Collapsed;
            DeleteDownloadMenu.Visibility = !episode.InLibrary || episode.CanBeDownloaded || episode.DownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
            StopDownloadMenu.Visibility = episode.DownloadInProgress ? Visibility.Visible : Visibility.Collapsed;

            e.Handled = true;
        }

        private void DownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var episode = frameworkElement.DataContext as Episode;

                episode?.DownloadAsync();
            }
        }

        private void MenuFlyout_OnClosed(object sender, object e)
        {
        }

        private void AddToPlaylistMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                Playlist.CurrentPlaylist.AppendEpisode(episode);
            }
        }

        private void DeleteDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var episode = frameworkElement.DataContext as Episode;

                episode?.DeleteDownload();
            }
        }

        private async void Title_OnClick(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(podcast.Link, UriKind.Absolute));
        }

        private void StopDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var episode = frameworkElement.DataContext as Episode;

                episode?.CancelDownload();
            }
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CoreTools.HandleItemsWidth(EpisodesListView);
        }

        void SyncFilterButtonText()
        {
            FilterButton.Content = LocalSettings.Instance.OnlyUnplayedEpisodes ? StringsHelper.All : StringsHelper.SeeOnlyUnPlayed;
        }

        private void FilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.OnlyUnplayedEpisodes = !LocalSettings.Instance.OnlyUnplayedEpisodes;

            SyncFilterButtonText();

            Sync();
        }

        private void ShareMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                episodeToShare = item.DataContext as Episode;

                DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();

                dataTransferManager.DataRequested += DataTransferManager_DataRequested;

                DataTransferManager.ShowShareUI();
            }
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataPackage dataPackage = args.Request.Data;
            dataPackage.Properties.Title = episodeToShare.Title;
            dataPackage.Properties.Description = episodeToShare.Subtitle;

            dataPackage.SetWebLink(new Uri(episodeToShare.Enclosure));

            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested -= DataTransferManager_DataRequested;
        }
    }
}
