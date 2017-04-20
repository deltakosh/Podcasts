using System;
using System.Collections.Generic;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;
using Newtonsoft.Json;

namespace Podcasts
{
    public class LocalSettings : Notifier
    {
        static LocalSettings instance;

        public static LocalSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LocalSettings();
                }

                return instance;
            }
            set
            {
                instance = value;
            }
        }

        public string this[string key]
        {
            get
            {
                if (!ContainsKey(key))
                {
                    return null;
                }

                return (string)ApplicationData.Current.LocalSettings.Values[key];
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }

        public bool ContainsKey(string key)
        {
            return ApplicationData.Current.LocalSettings.Values.ContainsKey(key);
        }

        public int CurrentRefreshedIndex
        {
            get
            {
                return CoreTools.GetSerializedIntValue("CurrentRefreshedIndex", true);                
            }
            set
            {
                CoreTools.SetSerializedValue("CurrentRefreshedIndex", true, value);
            }
        }

        string externalFolderPath;
        public string ExternalFolderPath
        {
            get
            {
                if (string.IsNullOrEmpty(externalFolderPath))
                {
                    externalFolderPath = CoreTools.GetSerializedStringValue("ExternalFolderPath", true, "");
                }
                return externalFolderPath;
            }
            set
            {
                externalFolderPath = value;
                CoreTools.SetSerializedValue("ExternalFolderPath", true, value);
            }
        }

        public string CurrentVersion
        {
            get
            {
                return CoreTools.GetSerializedStringValue("CurrentVersion", true);
            }
            set
            {
                CoreTools.SetSerializedValue("CurrentVersion", true, value);
            }
        }

        public bool Notifications
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("Notifications", true);
            }
            set
            {
                CoreTools.SetSerializedValue("Notifications", true, value);
            }
        }

        public string NotificationMessage
        {
            get
            {
                return CoreTools.GetSerializedStringValue("NotificationMessage", true);
            }
            set
            {
                CoreTools.SetSerializedValue("NotificationMessage", true, value);
            }
        }

        public double Volume
        {
            get
            {
                return CoreTools.GetSerializedDoubleValue("Volume", true, 100.0);
            }
            set
            {
                CoreTools.SetSerializedValue("Volume", true, value);
            }
        }

        public int NewEpisodesCount
        {
            get
            {
                return CoreTools.GetSerializedIntValue("NewEpisodesCount", true);
            }
            set
            {
                CoreTools.SetSerializedValue("NewEpisodesCount", true, value);               
            }
        }

        public int PodcastsFilter
        {
            get
            {
                return CoreTools.GetSerializedIntValue("PodcastsFilter", true);
            }
            set
            {
                CoreTools.SetSerializedValue("PodcastsFilter", true, value);
            }
        }

        public int PlaySpeed
        {
            get
            {
                return CoreTools.GetSerializedIntValue("PlaySpeed", true, 100);
            }
            set
            {
                CoreTools.SetSerializedValue("PlaySpeed", true, value);
            }
        }

        public bool Metered
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("Metered", true);
            }
            set
            {
                CoreTools.SetSerializedValue("Metered", true, value);
            }
        }

        public bool StreamOnMetered
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("StreamOnMetered", true);
            }
            set
            {
                CoreTools.SetSerializedValue("StreamOnMetered", true, value);
            }
        }

        public bool DeleteDownloadWhenPlayed
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("DeleteDownloadWhenPlayed", true, true);
            }
            set
            {
                CoreTools.SetSerializedValue("DeleteDownloadWhenPlayed", true, value);
            }
        }

        public bool UseSystemAccent
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("UseSystemAccent", true, true);
            }
            set
            {
                CoreTools.SetSerializedValue("UseSystemAccent", true, value);
            }
        }

        public bool ForegroundTaskIsRunning
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("ForegroundTaskIsRunning", true);
            }
            set
            {
                CoreTools.SetSerializedValue("ForegroundTaskIsRunning", true, value);
            }
        }

        public bool DarkTheme
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("DarkTheme", true);
            }
            set
            {
                CoreTools.SetSerializedValue("DarkTheme", true, value);
            }
        }

        public bool CloudSync
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("CloudSync", true, true);
            }
            set
            {
                CoreTools.SetSerializedValue("CloudSync", true, value);
            }
        }

        public bool VideoPlayback
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("VideoPlayback", true);
            }
            set
            {
                CoreTools.SetSerializedValue("VideoPlayback", true, value);
            }
        }

        public bool InvertSkipControls
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("InvertSkipControls", true);
            }
            set
            {
                CoreTools.SetSerializedValue("InvertSkipControls", true, value);
                RaisePropertyChanged(nameof(InvertSkipControls));
            }
        }

        public bool RemovedPlayedEpisodeFromPlayList
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("RemovedPlayedEpisodeFromPlayList", true);
            }
            set
            {
                CoreTools.SetSerializedValue("RemovedPlayedEpisodeFromPlayList", true, value);
            }
        }

        public bool ForceCloudSync
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("ForceCloudSync", true);
            }
            set
            {
                CoreTools.SetSerializedValue("ForceCloudSync", true, value);
            }
        }

        public int DeleteEpisodesOlderThan
        {
            get
            {
                return CoreTools.GetSerializedIntValue("DeleteEpisodesOlderThan", true, 30);
            }
            set
            {
                CoreTools.SetSerializedValue("DeleteEpisodesOlderThan", true, value);
            }
        }

        public int SleepTimerDuration
        {
            get
            {
                return CoreTools.GetSerializedIntValue("SleepTimerDuration", true, 30);
            }
            set
            {
                if (value < 1)
                {
                    value = 1;
                }

                if (value > 120)
                {
                    value = 120;
                }

                CoreTools.SetSerializedValue("SleepTimerDuration", true, value);
            }
        }

        public bool OnlyUnplayedEpisodes
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("OnlyUnplayedEpisodes", true);
            }
            set
            {
                CoreTools.SetSerializedValue("OnlyUnplayedEpisodes", true, value);
            }
        }

        public bool LocationWarnedOnce
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("LocationWarnedOnce", true);
            }
            set
            {
                CoreTools.SetSerializedValue("LocationWarnedOnce", true, value);
            }
        }

        public bool AutoSyncOnClose
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("AutoSyncOnClose", true, true);
            }
            set
            {
                CoreTools.SetSerializedValue("AutoSyncOnClose", true, value);
            }
        }

        public bool FavorRemainingDuration
        {
            get
            {
                return CoreTools.GetSerializedBoolValue("FavorRemainingDuration", true);
            }
            set
            {
                CoreTools.SetSerializedValue("FavorRemainingDuration", true, value);
            }
        }
    }
}
