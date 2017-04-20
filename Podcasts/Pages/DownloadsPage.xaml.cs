using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public sealed partial class DownloadsPage
    {
        ObservableCollection<Episode> downloads;
        public DownloadsPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            foreach (Episode episode in downloads)
            {
                episode.PropertyChanged -= Episode_PropertyChanged;
            }
            base.OnNavigatedFrom(e);
        }

        internal override void ClearReferences()
        {
            if (downloads != null)
            {
                downloads.CollectionChanged -= Downloads_CollectionChanged;
            }
        }

        private async void DownloadsPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            downloads = Library.DownloadedEpisodes;

            downloads.CollectionChanged += Downloads_CollectionChanged;

            DownloadsListView.ItemsSource = downloads;
            GlobalStateManager.SelectedMenuIndex = 4;

            foreach (var episode in downloads)
            {
                episode.UpdateDownloadInfo();
                episode.PropertyChanged += Episode_PropertyChanged;
            }

            UpdateTitleInfos();

            waitRing.Visibility = Visibility.Visible;
            await Library.WaitFullRefreshAsync();
            waitRing.Visibility = Visibility.Collapsed;
        }

        private void UpdateTitleInfos()
        {
            var size = downloads.Sum(e => e.DownloadSize);
            TitleInfos.Text = $"{downloads.Count} {StringsHelper.Downloads.Replace("(s)", downloads.Count > 1 ? "s" : "")}\n{size:F} MB";
        }

        private async void Downloads_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Episode episode in e.NewItems)
                {
                    episode.PropertyChanged += Episode_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (Episode episode in e.OldItems)
                {
                    episode.PropertyChanged -= Episode_PropertyChanged;
                }
            }

            if (Dispatcher.HasThreadAccess)
            {
                UpdateTitleInfos();
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateTitleInfos();
                });
            }
        }

        private async void Episode_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "DownloadInfo")
            {
                return;
            }

            if (Dispatcher.HasThreadAccess)
            {
                UpdateTitleInfos();
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateTitleInfos();
                });
            }
        }

        private async void Delete_OnClick(object sender, EventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                if (episode.IsAlreadyDownloaded)
                {
                    await episode.DeleteDownloadAsync();
                }
                else
                {
                    episode.CancelDownload();
                }
            }
        }

        private void DownloadsListView_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var episode = e.ClickedItem as Episode;

            if (episode == null)
            {
                return;
            }

            GlobalStateManager.CurrentShell.Navigate(typeof(EpisodePage), episode.InLibrary ? episode.Enclosure : episode.ToCompleteString());
        }

        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var menu = (MenuFlyout)RootGrid.Resources["MenuFlyout"];

            var startGrid = (Grid)sender;
            menu.ShowAt(startGrid, e.GetPosition(startGrid));

            var episode = startGrid.DataContext as Episode;

            if (episode == null)
            {
                return;
            }

            MarkAsPlayedMenu.Visibility = !episode.InLibrary || episode.IsPlayed ? Visibility.Collapsed : Visibility.Visible;
            MarkAsUnplayedMenu.Visibility = episode.IsPlayed ? Visibility.Visible : Visibility.Collapsed;

            DeleteDownloadMenu.Visibility = !episode.InLibrary || episode.CanBeDownloaded || episode.DownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
            StopDownloadMenu.Visibility = episode.DownloadInProgress ? Visibility.Visible : Visibility.Collapsed;

            e.Handled = true;
        }

        private void MarkAsPlayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null) episode.IsPlayed = true;
        }

        private void MarkAsUnplayed_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null) episode.IsPlayed = false;
        }

        private void PlayMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                MediaPlayerHost.ResetLastOrderReceived();
                Playlist.CurrentPlaylist.PlayEpisode(episode);
            }
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

        private async void DeleteDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                await episode.DeleteDownloadAsync();

                downloads.Remove(episode);
            }
        }

        private void StopDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                episode.CancelDownload();

                downloads.Remove(episode);
            }
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CoreTools.HandleItemsWidth(DownloadsListView);
        }

        private void AddToPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.AddEpisodes(downloads.ToList());
        }

        private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            var temp = downloads.ToArray();
            foreach (var episode in temp)
            {
                if (episode.IsAlreadyDownloaded)
                {
                    await episode.DeleteDownloadAsync(true);
                }
                else
                {
                    episode.CancelDownload(true);
                }
            }
            WaitRingManager.IsWaitRingVisible = false;
        }

        private async void SlidableListItem_RightCommandRequested(object sender, EventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            var episode = frameworkElement?.DataContext as Episode;

            if (episode != null)
            {
                await episode.DeleteDownloadAsync();

                downloads.Remove(episode);
            }
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                var episode = image.DataContext as Episode;
                episode?.FallbackToPodcastPicture();
            }
        }
    }
}
