using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Podcasts
{
    public class AppSettings
    {
        private static AppSettings instance;
        public static AppSettings Instance => instance ?? (instance = new AppSettings());

        public string this[string key]
        {
            get
            {
                if (!ContainsKey(key))
                {
                    return null;
                }

                return ApplicationData.Current.RoamingSettings.Values[key].ToString();
            }
            set
            {
                ApplicationData.Current.RoamingSettings.Values[key] = value;
            }
        }

        public bool ContainsKey(string key)
        {
            return ApplicationData.Current.RoamingSettings.Values.ContainsKey(key);
        }

        int? podcastsCount;
        public int PodcastsCount
        {
            get
            {
                if (!podcastsCount.HasValue)
                {
                    podcastsCount = CoreTools.GetSerializedIntValue("PodcastsCount", false);
                }
                return podcastsCount.Value;
            }
            set
            {
                podcastsCount = value;
                CoreTools.SetSerializedValue("PodcastsCount", false, podcastsCount);
            }
        }

        string uniqueUserId;
        public string UniqueUserId
        {
            get
            {
#if DEBUG
                return "97bd4928-f47f-43c9-bafc-4872b981f592";
#else
                if (string.IsNullOrEmpty(uniqueUserId))
                {
                    uniqueUserId = CoreTools.GetSerializedStringValue("HighPriority", false, Guid.NewGuid().ToString());
                    CoreTools.SetSerializedValue("HighPriority", false, uniqueUserId);
                }
                return uniqueUserId;
#endif
            }
            set
            {
#if DEBUG
                return;
#else
                uniqueUserId = value;
                CoreTools.SetSerializedValue("HighPriority", false, uniqueUserId);
#endif
            }
        }

        int? downloadLastEpisodesCount;
        public int DownloadLastEpisodesCount
        {
            get
            {
                if (!downloadLastEpisodesCount.HasValue)
                {
                    downloadLastEpisodesCount = CoreTools.GetSerializedIntValue("DownloadLastEpisodesCount", false, 1);
                }
                return downloadLastEpisodesCount.Value;
            }
            set
            {
                downloadLastEpisodesCount = value;
                CoreTools.SetSerializedValue("DownloadLastEpisodesCount", false, value);
            }
        }

        int? forwardStep;
        public int ForwardStep
        {
            get
            {
                if (!forwardStep.HasValue)
                {
                    forwardStep = CoreTools.GetSerializedIntValue("ForwardStep", false, 10);
                }
                return forwardStep.Value;
            }
            set
            {
                forwardStep = value;
                CoreTools.SetSerializedValue("ForwardStep", false, value);
            }
        }

        int? rewindStep;
        public int RewindStep
        {
            get
            {
                if (!rewindStep.HasValue)
                {
                    rewindStep = CoreTools.GetSerializedIntValue("RewindStep", false, 10);
                }
                return rewindStep.Value;
            }
            set
            {
                rewindStep = value;
                CoreTools.SetSerializedValue("RewindStep", false, value);
            }
        }

        string market;
        public string Market
        {
            get
            {
                if (string.IsNullOrEmpty(market))
                {
                    market = CoreTools.GetSerializedStringValue("Market", false, RegionInfo.CurrentRegion.TwoLetterISORegionName);
                }
                return market;
            }
            set
            {
                market = value;
                CoreTools.SetSerializedValue("Market", false, market);
            }
        }

        public bool AutomaticallyAddNewEpisodeToPlaylist
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("AutomaticallyAddNewEpisodeToPlaylist", false, true);
            }
            set
            {
                CoreTools.SetSerializedValue("AutomaticallyAddNewEpisodeToPlaylist", false, value);
            }
        }

        public bool AutoPlay
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("AutoPlay", false, true);
            }
            set
            {
                CoreTools.SetSerializedValue("AutoPlay", false, value);
            }
        }

        public bool TipSent
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("TipSent", false);
            }
            set
            {
                CoreTools.SetSerializedValue("TipSent", false, value);
            }
        }

        public bool OneDriveWarningDisplayed
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("OneDriveWarningDisplayed", false);
            }
            set
            {
                CoreTools.SetSerializedValue("OneDriveWarningDisplayed", false, value);
            }
        }

        public bool OneDriveFolderMoved
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("OneDriveFolderMoved", false);
            }
            set
            {
                CoreTools.SetSerializedValue("OneDriveFolderMoved", false, value);
            }
        }
    }
}
