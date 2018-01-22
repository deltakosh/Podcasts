using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public sealed partial class SearchPage
    {
        bool needRefresh;
        CancellationTokenSource cancellationTokenSource;
        SearchResponseEntry entryToEdit;

        public SearchPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            needRefresh = (e.NavigationMode != NavigationMode.Back);
        }

        private void RootPage_Loaded(object sender, RoutedEventArgs e)
        {
            GlobalStateManager.SelectedMenuIndex = 0;

            if (needRefresh)
            {
                SearchBox.Text = "";
                ResultsListView.ItemsSource = null;
            }

            SearchBox.Focus(FocusState.Programmatic);
        }

        void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox.Text.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }
            ReSync();
        }

        void ReSync()
        {
            cancellationTokenSource?.Cancel();

            cancellationTokenSource = new CancellationTokenSource();

            DoSearch(cancellationTokenSource.Token);
        }

        void DoSearch(CancellationToken token)
        {
            var searchFilter = SearchBox.Text;
            ProgressRing.Visibility = Visibility.Visible;
            Task.Run(async () =>
            {
                SearchResponseEntry[] results = null;
                try
                {
                    results = await SearchEngine.SearchAsync(searchFilter, AppSettings.Instance.Market);
                }
                catch
                {
                    //  Ignore error
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        ResultsListView.ItemsSource = results;
                        ProgressRing.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        //  Ignore error
                    }
                });
            }, token);
        }

        private async void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var source = e.ClickedItem as SearchResponseEntry;

            if (source == null)
            {
                return;
            }

            WaitRingManager.IsWaitRingVisible = true;

            var podcast = await Podcast.ParseAsync(source.feedUrl, false);

            WaitRingManager.IsWaitRingVisible = false;

            if (podcast != null)
            {
                GlobalStateManager.CurrentShell.Navigate(typeof(PodcastPage), podcast.ToString());
            }
        }

        private async void AddToLibraryMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                entryToEdit = item.DataContext as SearchResponseEntry;
            }

            var flyout = RootGrid.Resources["AddToLibraryFlyout"] as Flyout;

            var listViewItem = ResultsListView.ContainerFromItem(entryToEdit) as GridViewItem;
            if (flyout != null)
            {
                var editor = ((FrameworkElement)flyout.Content).FindDescendantByName("AddToMyPodcastsEditor") as AddToMyPodcasts;

                WaitRingManager.IsWaitRingVisible = true;
                var podcast = await Podcast.ParseAsync(entryToEdit.feedUrl, false);
                WaitRingManager.IsWaitRingVisible = false;

                if (podcast != null)
                {
                    editor?.SetPodcast(podcast);
                    flyout.ShowAt(listViewItem);
                }
            }
        }

        private void EditCategoryFlyout_OnOpening(object sender, object e)
        {
        }

        private void CategoryEditor_OnUpdate()
        {
            Messenger.Notify(StringsHelper.Success_AddPodcastToLibrary, "library");
        }

        private void MenuFlyout_OnClosed(object sender, object e)
        {
        }

        private void RootPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CoreTools.HandleItemsWidth(ResultsListView);
        }

        private async void BrowseControl_OnUpdate(string url, string login, string password)
        {
            WaitRingManager.IsWaitRingVisible = true;

            try
            {
                if (!url.ContainsIgnoreCase("http"))
                {
                    url = "http://" + url;
                }

                var podcast = await Podcast.ParseAsync(url, false, login, password);

                GlobalStateManager.CurrentShell.Navigate(typeof(PodcastPage), podcast.ToString());
            }
            catch
            {
                await Messenger.ErrorAsync(StringsHelper.Error_UnableToParseRSS);
            }
            WaitRingManager.IsWaitRingVisible = false;
        }

        private void Flyout_Opening(object sender, object e)
        {
            var flyout = sender as Flyout;
            var browser = flyout.Content as BrowseControl;

            //browser.Reset();
        }

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            var searchResponse = (SearchResponseControl) sender;

            searchResponse.ContextFlyout = (MenuFlyout)RootGrid.Resources["MenuFlyout"];
        }
    }
}
