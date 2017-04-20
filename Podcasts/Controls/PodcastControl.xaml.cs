using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Podcasts
{
    public sealed partial class PodcastControl : UserControl
    {
        public PodcastControl()
        {
            this.InitializeComponent();
        }

        private void PodcastSlidableListItem_LeftCommandRequested(object sender, EventArgs e)
        {
            var podcast = (Podcast)DataContext;

            if (podcast != null)
            {
                Playlist.CurrentPlaylist.AddEpisodes(podcast.Episodes.Where(ep => !ep.IsPlayed).OrderByDescending(ep => ep.PublicationDate).ToList());
            }
        }

        private async void PodcastSlidableListItem_RightCommandRequested(object sender, EventArgs e)
        {
            var podcast = (Podcast)DataContext;

            if (podcast != null)
            {
                await WaitRingManager.ShowBlurBackground(true);
                await Library.DeletePodcast(podcast);
                await WaitRingManager.ShowBlurBackground(false);
            }
        }
    }
}
