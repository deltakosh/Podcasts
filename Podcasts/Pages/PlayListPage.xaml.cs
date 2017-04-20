using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public sealed partial class PlayListPage
    {
        Episode episodeToShare;

        public PlayListPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        internal override void ClearReferences()
        {
            Playlist.CurrentPlaylist.Entries.CollectionChanged += Entries_CollectionChanged;
        }

        private void PlayListPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.Entries.CollectionChanged += Entries_CollectionChanged;

            PlaylistListView.DataContext = Playlist.CurrentPlaylist;
            GlobalStateManager.SelectedMenuIndex = 2;

            UpdateTitleInfos();
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTitleInfos();
        }

        private async void UpdateTitleInfos()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                TitleInfos.Text = $"{Playlist.CurrentPlaylist.Entries.Count} {StringsHelper.PodcastsPlural.Replace("(s)", Playlist.CurrentPlaylist.Entries.Count > 1 ? "s" : "")}";
            });
        }
  
        private void PlaylistListView_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var entry = e.ClickedItem as PlaylistEntry;

            if (entry == null)
            {
                return;
            }            

            Playlist.CurrentPlaylist.PlayEpisode(entry.Episode);
        }

        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var menu = (MenuFlyout)RootGrid.Resources["MenuFlyout"];

            var startGrid = (Grid)sender;
            menu.ShowAt(startGrid, e.GetPosition(startGrid));

            var entry = startGrid.DataContext as PlaylistEntry;

            if (entry == null)
            {
                return;
            }

            var episode = entry.Episode;

            MarkAsPlayedMenu.Visibility = !episode.InLibrary || episode.IsPlayed ? Visibility.Collapsed : Visibility.Visible;
            MarkAsUnplayedMenu.Visibility = episode.IsPlayed ? Visibility.Visible : Visibility.Collapsed;

            DownloadMenu.Visibility = episode.CanBeDownloaded ? Visibility.Visible : Visibility.Collapsed;
            DeleteDownloadMenu.Visibility = !episode.InLibrary || episode.CanBeDownloaded || episode.DownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
            StopDownloadMenu.Visibility = episode.DownloadInProgress ? Visibility.Visible : Visibility.Collapsed;

            DownloadSeparator.Visibility = !episode.InLibrary ? Visibility.Collapsed : Visibility.Visible;

            e.Handled = true;
        }


        private void MarkAsPlayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                entry.Episode.IsPlayed = true;
            }
        }

        private void MarkAsUnplayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                entry.Episode.IsPlayed = false;
            }
        }

        private void PlayMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                MediaPlayerHost.ResetLastOrderReceived();
                Playlist.CurrentPlaylist.PlayEpisode(entry.Episode);
            }
        }

        private void DownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var entry = frameworkElement.DataContext as PlaylistEntry;

                entry.Episode?.DownloadAsync();
            }
        }

        private void DeleteDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var entry = frameworkElement.DataContext as PlaylistEntry;

                entry.Episode?.DeleteDownload();
            }
        }

        private void StopDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var entry = frameworkElement.DataContext as PlaylistEntry;

                entry.Episode?.CancelDownload();
            }
        }

        private void UpPodcastMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                var entry = item.DataContext as PlaylistEntry;
                Playlist.CurrentPlaylist.MoveEntryUp(entry);
            }
        }

        private void DownPodcastMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                var entry = item.DataContext as PlaylistEntry;
                Playlist.CurrentPlaylist.MoveEntryDown(entry);
            }
        }

        private void DownloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            foreach (var episode in Playlist.CurrentPlaylist.Episodes)
            {
                if (episode.IsAlreadyDownloaded || episode.DownloadInProgress)
                {
                    continue;
                }

                episode.DownloadAsync();
            }
            WaitRingManager.IsWaitRingVisible = false;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.Clear();
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CoreTools.HandleItemsWidth(PlaylistListView);
        }

        private void OrderDescending_Click(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.Reorder(false);
        }

        private void OrderAscending_Click(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.Reorder(true);
        }

        private void ShareMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                var entry = item.DataContext as PlaylistEntry;
                episodeToShare = entry.Episode;

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

        private void PlaylistEntrySlidableListItem_RightCommandRequested(object sender, EventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                Playlist.CurrentPlaylist.RemoveEntry(entry);
            }
        }

        private void PlaylistEntrySlidableListItem_LeftCommandRequested(object sender, EventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                entry.Episode.IsPlayed = !entry.Episode.IsPlayed;
            }
        }
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                entry.Episode.FallbackToPodcastPicture();
            }
        }

        private void Delete_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var entry = frameworkElement?.DataContext as PlaylistEntry;

            if (entry != null)
            {
                Playlist.CurrentPlaylist.RemoveEntry(entry);
            }

            e.Handled = true;
        }
    }
}
