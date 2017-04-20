using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Podcasts
{
    [DataContract]
    public class PlaylistEntry : Notifier
    {
        double? loadedPosition;

        [DataMember]
        public double Position
        {
            get
            {
                return Episode.Position;
            }

            set
            {
                if (Episode == null)
                {
                    loadedPosition = value;
                    return;
                }
                Episode.Position = value;
                RaisePropertyChanged(nameof(Position));
                RaisePropertyChanged(nameof(RemainingDuration));
            }
        }

        public double Duration
        {
            get
            {
                return Episode.Duration;
            }

            set
            {
                Episode.Duration = value;
                RaisePropertyChanged(nameof(Duration));
                RaisePropertyChanged(nameof(RemainingDuration));
            }
        }

        public double RemainingDuration => Duration - Position;

        [DataMember]
        public string Enclosure
        {
            get
            {
                return Episode.Enclosure;
            }
            set
            {
                episode = Library.GetEpisodeByEnclosure(value);
                if (episode != null)
                {
                    if (loadedPosition.HasValue)
                    {
                        episode.Position = loadedPosition.Value;
                        RaisePropertyChanged(nameof(Position));
                        RaisePropertyChanged(nameof(Duration));
                        RaisePropertyChanged(nameof(RemainingDuration));
                    }
                }
            }
        }

        public string PodcastTitle => episode.Podcast != null ? episode.Podcast.Title : "";

        public MediaPlaybackItem AssociatedPlaybackItem { get; set; }

        public bool IsStreaming { get; private set; }

        private Episode episode;
        public Episode Episode => episode;

        bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                RaisePropertyChanged(nameof(IsSelected));

                if (!IsSelected && Episode.IsPlayed)
                {
                    if (LocalSettings.Instance.DeleteDownloadWhenPlayed)
                    {
                        Episode.DeleteDownload(true);
                    }

                    if (LocalSettings.Instance.RemovedPlayedEpisodeFromPlayList)
                    {
                        if (Playlist.CurrentPlaylist != null)
                        {
                            Playlist.CurrentPlaylist.RemoveEntry(this);
                        }
                    }
                }

                if (Playlist.CurrentPlaylist == null)
                {
                    return;
                }

                if (IsSelected)
                {
                    try
                    {
                        var entries = Playlist.CurrentPlaylist.Entries.ToList();
                        foreach (var entry in entries)
                        {
                            if (entry == this)
                            {
                                continue;
                            }

                            entry.IsSelected = false;
                        }
                    }
                    catch
                    {
                        // Ignore error
                    }
                }
            }
        }

        public async Task<bool> IsOnlineOnlyAsync()
        {
            var localFile = Episode.LocalFilename;
            return string.IsNullOrEmpty(localFile) || !await FileHelper.IsLocalFileExistsAsync(localFile, true);
        }

        public async Task<MediaSource> GetSourceAsync()
        {
            if (Episode == null || string.IsNullOrEmpty(Enclosure))
            {
                return null;
            }

            if (await IsOnlineOnlyAsync())
            {
                IsStreaming = true;
                return MediaSource.CreateFromUri(new Uri(Enclosure, UriKind.Absolute));
            }

            IsStreaming = false;
            var localFile = Episode.LocalFilename;
            var file = await FileHelper.GetLocalFileAsync(localFile, true);
            return MediaSource.CreateFromStorageFile(file);
        }        
    }
}
