using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Podcasts
{
    public sealed partial class PlaylistControl
    {
        public PlaylistControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ListGrid.ItemsSource = Playlist.CurrentPlaylist.Entries;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var entry = ((FrameworkElement)sender).DataContext as PlaylistEntry;

            Playlist.CurrentPlaylist.RemoveEntry(entry);

            if (Playlist.CurrentPlaylist.Entries.Count == 0)
            {
                CloseFlyout();
            }
        }

        private void ListGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            Playlist.CurrentPlaylist.PlayEpisode((e.ClickedItem as PlaylistEntry).Episode);
        }

        void CloseFlyout()
        {
            try
            {
                if (Parent == null)
                {
                    return;
                }

                var flyoutPresenter = Parent as FlyoutPresenter;
                var popup = flyoutPresenter?.Parent as Popup;
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }
            catch
            {
                // Ignore error
            }
        }
    }
}
