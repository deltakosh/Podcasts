using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public sealed partial class LibraryPage
    {
        CollectionViewSource collectionViewSource;
        Podcast podcastToEdit;
        bool needRefresh = true;
        bool blockSync;

        public LibraryPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;

        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && collectionViewSource != null)
            {
                needRefresh = false;
            }

            Library.Podcasts.CollectionChanged += Podcasts_CollectionChanged;

            base.OnNavigatedTo(e);
        }

        private async void Podcasts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.HasThreadAccess)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Sync();
                });
            }
            else
            {
                Sync();
            }
        }

        internal override void ClearReferences()
        {
            Library.Podcasts.CollectionChanged -= Podcasts_CollectionChanged;
            UnbindOnChange();
        }

        private void itemGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var podcast = e.ClickedItem as Podcast;

            if (podcast == null)
            {
                var episode = e.ClickedItem as Episode;

                if (episode == null)
                {
                    return;
                }

                GlobalStateManager.CurrentShell.Navigate(typeof(EpisodePage), episode.InLibrary ? episode.Enclosure : episode.ToCompleteString());
                return;
            }

            GlobalStateManager.CurrentShell.Navigate(typeof(PodcastPage), podcast.FeedUrl);
        }

        void OnLibraryLoaded()
        {
            if (!needRefresh)
            {
                WaitRingManager.IsWaitRingVisible = false;
                return;
            }
            blockSync = true;
            Filters.ItemsSource = new[] { StringsHelper.SeeAll, StringsHelper.SeeOnlyUnPlayed, StringsHelper.UnplayedEpisodesAsc, StringsHelper.UnplayedEpisodesDesc };
            Filters.SelectedIndex = Math.Max(0, LocalSettings.Instance.PodcastsFilter);

            blockSync = false;
            Sync();
            WaitRingManager.IsWaitRingVisible = false;
        }

        private async void LibraryPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            GlobalStateManager.SelectedMenuIndex = 3;

            WaitRingManager.IsWaitRingVisible = true;
            await Library.WaitReadyAsync();
            OnLibraryLoaded();
        }

        void BindOnChange()
        {
            UnbindOnChange();
            for (var index = 0; index < Library.Podcasts.Count; index++)
            {
                var podcast = Library.Podcasts[index];
                podcast.PropertyChanged += Podcast_PropertyChanged;
            }
        }

        void UnbindOnChange()
        {
            for (var index = 0; index < Library.Podcasts.Count; index++)
            {
                var podcast = Library.Podcasts[index];
                podcast.PropertyChanged -= Podcast_PropertyChanged;
            }
        }

        private void Podcast_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HasUnplayed" && Filters.SelectedIndex > 0)
            {
                Sync();
            }
        }

        void Sync()
        {
            if (blockSync)
            {
                return;
            }
            BindOnChange();
            IEnumerable source = null;

            var data = Library.Podcasts.ToList();
            
            switch (LocalSettings.Instance.PodcastsFilter)
            {
                case 1:
                    source = data.Where(p => p.HasUnplayed).ToList();
                    break;
                case 2:
                case 3:
                    source = data.SelectMany(p => p.Episodes).Where(e => !e.IsPlayed).ToList();
                    break;
                default:
                    source = data.ToList();
                    break;
            }

            if (CoreWindow.GetForCurrentThread().Bounds.Width < 600)
            {
                itemGridView.ItemContainerStyle = (Style)App.Current.Resources["NoOverChange"];
            }

            if (LocalSettings.Instance.PodcastsFilter < 2)
            {
                itemGridView.ItemTemplate = (DataTemplate)RootGrid.Resources["PodcastTemplate"];

                var result = from podcast in (IEnumerable<Podcast>)source
                             group podcast by podcast.Category.ToLower()
                             into grp
                             orderby grp.Key
                             select grp;

                collectionViewSource = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = result
                };

                itemGridView.DataContext = collectionViewSource;
            }
            else
            {
                itemGridView.ItemTemplate = (DataTemplate)RootGrid.Resources["EpisodeTemplate"];

                object result;
                if (LocalSettings.Instance.PodcastsFilter == 2)
                {
                    result = from episode in (IEnumerable<Episode>)source
                             group episode by episode.PublicationDate.ToString("d")
                             into grp
                             orderby DateTime.Parse(grp.Key)
                             select grp;
                }
                else
                {
                    result = from episode in (IEnumerable<Episode>)source
                             group episode by episode.PublicationDate.ToString("d")
                             into grp
                             orderby DateTime.Parse(grp.Key) descending
                             select grp;
                }

                collectionViewSource = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = result
                };

                itemGridView.DataContext = collectionViewSource;
            }
        }

        void UIElement_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var menu = (MenuFlyout)RootGrid.Resources["MenuFlyout"];

            var startGrid = (PodcastControl)sender;
            menu.ShowAt(startGrid, e.GetPosition(startGrid));

            var podcast = startGrid.DataContext as Podcast;

            if (podcast != null)
            {
                MarkAsPlayedMenu.Visibility = podcast.HasUnplayed ? Visibility.Visible : Visibility.Collapsed;
                AddAllToPlaylistMenu.Visibility = podcast.HasUnplayed ? Visibility.Visible : Visibility.Collapsed;
                AddAllToPlaylistMenuSeparator.Visibility = AddAllToPlaylistMenu.Visibility;
            }

            e.Handled = true;
        }

        private void MarkAsPlayedMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            var podcast = item?.DataContext as Podcast;

            podcast?.MarkAsPlayed();
        }

        private async void DeletePodcastMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            var podcast = item?.DataContext as Podcast;

            if (podcast != null)
            {
                await WaitRingManager.ShowBlurBackground(true);
                if (await Library.DeletePodcast(podcast))
                {
                    Sync();
                }
                await WaitRingManager.ShowBlurBackground(false);
            }
        }

        private void PodcastCategoryMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                podcastToEdit = item.DataContext as Podcast;
            }
            else
            {
                return;
            }

            if (podcastToEdit == null)
            {
                return;
            }

            var flyout = RootGrid.Resources["EditCategoryFlyout"] as Flyout;

            var gridViewItem = itemGridView.ContainerFromItem(podcastToEdit) as GridViewItem;
            flyout?.ShowAt(gridViewItem);
        }

        private void EditCategoryFlyout_OnOpening(object sender, object e)
        {
            var flyout = sender as Flyout;
            if (flyout != null)
            {
                var editor = ((FrameworkElement)flyout.Content).FindDescendantByName("AddToMyPodcastsEditor") as AddToMyPodcasts;
                editor?.SetPodcast(podcastToEdit);
            }
        }

        private void CategoryEditor_OnUpdate()
        {
            Library.Save();
            Sync();
        }

        private void MenuFlyout_OnClosed(object sender, object e)
        {
        }

        private void UpPodcastMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                var podcast = item.DataContext as Podcast;
                var index = Library.Podcasts.IndexOf(podcast);

                for (index--; index >= 0; index--)
                {
                    if (Library.Podcasts[index].Category == podcast.Category)
                    {
                        Library.Podcasts.Remove(podcast);
                        Library.Podcasts.Insert(index, podcast);
                        Library.Save();
                        Sync();
                        break;
                    }
                }
            }
        }

        private void DownPodcastMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                var podcast = item.DataContext as Podcast;
                var index = Library.Podcasts.IndexOf(podcast);

                for (index++; index < Library.Podcasts.Count; index++)
                {
                    if (Library.Podcasts[index].Category == podcast.Category)
                    {
                        Library.Podcasts.Remove(podcast);
                        Library.Podcasts.Insert(index, podcast);
                        Library.Save();
                        Sync();
                        break;
                    }
                }
            }
        }

        private void AddAllToPlaylistMenu_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            var podcast = item?.DataContext as Podcast;

            if (podcast != null && podcast.HasUnplayed)
            {
                Playlist.CurrentPlaylist.AddEpisodes(podcast.Episodes.Where(ep => !ep.IsPlayed).OrderBy(ep => ep.PublicationDate).ToList());
            }
        }

        private void Filters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Filters.SelectedIndex == -1)
            {
                return;
            }
            LocalSettings.Instance.PodcastsFilter = Filters.SelectedIndex;
            Sync();
        }

        private void EpisodeControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var menu = (MenuFlyout)RootGrid.Resources["EpisodeMenuFlyout"];

            var startGrid = (EpisodeControl)sender;
            menu.ShowAt(startGrid, e.GetPosition(startGrid));

            var episode = startGrid.DataContext as Episode;

            if (episode == null)
            {
                return;
            }

            EpisodeMarkAsPlayedMenu.Visibility = !episode.InLibrary || episode.IsPlayed ? Visibility.Collapsed : Visibility.Visible;
            MarkAsUnplayedMenu.Visibility = episode.IsPlayed ? Visibility.Visible : Visibility.Collapsed;

            DownloadMenu.Visibility = episode.CanBeDownloaded ? Visibility.Visible : Visibility.Collapsed;
            DeleteDownloadMenu.Visibility = !episode.InLibrary || episode.CanBeDownloaded || episode.DownloadInProgress ? Visibility.Collapsed : Visibility.Visible;
            StopDownloadMenu.Visibility = episode.DownloadInProgress ? Visibility.Visible : Visibility.Collapsed;

            e.Handled = true;
        }

        private void EpisodeMarkAsPlayed_OnClick(object sender, RoutedEventArgs e)
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

        private void DownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var episode = frameworkElement.DataContext as Episode;

                episode?.DownloadAsync();
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

        private void StopDownloadMenu_OnClick(object sender, RoutedEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement != null)
            {
                var episode = frameworkElement.DataContext as Episode;

                episode?.CancelDownload();
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

        private void AddAllEpisodesToPlaylistMenu_OnClick(object sender, RoutedEventArgs e)
        {
            IEnumerable<Episode> source = null;
            switch (LocalSettings.Instance.PodcastsFilter)
            {
                case 2:
                    source = Library.Podcasts.SelectMany(p => p.Episodes).Where(ep => !ep.IsPlayed).OrderBy(ep => ep.PublicationDate);
                    break;
                case 3:
                    source = Library.Podcasts.SelectMany(p => p.Episodes).Where(ep => !ep.IsPlayed).OrderByDescending(ep => ep.PublicationDate);
                    break;
            }

            Playlist.CurrentPlaylist.AddEpisodes(source.ToList());            
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CoreTools.HandleItemsWidth(itemGridView);
        }

        private void AddAllToPlaylistMenuDesc_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            var podcast = item?.DataContext as Podcast;

            if (podcast != null && podcast.HasUnplayed)
            {
                Playlist.CurrentPlaylist.AddEpisodes(podcast.Episodes.Where(ep => !ep.IsPlayed).OrderByDescending(ep => ep.PublicationDate).ToList());
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            await Library.RefreshAsync(true);
            Sync();
            WaitRingManager.IsWaitRingVisible = false;
        }
    }
}
