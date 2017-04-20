using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Podcasts;
using Newtonsoft.Json;

namespace BackgroundTask
{
    public sealed class RefreshTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (LocalSettings.Instance.ForegroundTaskIsRunning)
            {
                return;
            }
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            CheckPodcasts(deferral);
        }

        private async void CheckPodcasts(BackgroundTaskDeferral deferral)
        {
            try
            {
                await Library.DeserializeAsync(false);

                if (Library.Podcasts.Count > 0)
                {
                    var index = LocalSettings.Instance.CurrentRefreshedIndex;

                    if (index >= Library.Podcasts.Count)
                    {
                        index = 0;
                    }

                    var podcast = Library.Podcasts[index];
                    LocalSettings.Instance.CurrentRefreshedIndex = index + 1;

                    // Refresh
                    await podcast.RefreshAsync(true, true, false);
                    await Library.SaveAsync();

                    CoreTools.SetBadgeNumber(LocalSettings.Instance.NewEpisodesCount);

                    // Update playlist
                    await Playlist.CurrentPlaylist.SaveAsync();
                }
            }
            catch
            {
                // Ignore error
            }

            deferral.Complete();
        }
    }
}
