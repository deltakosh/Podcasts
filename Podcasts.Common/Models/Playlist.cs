using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using System.Diagnostics;
using Windows.Media.Playback;

namespace Podcasts
{
    [DataContract]
    public class Playlist : Notifier
    {
        public static List<Episode> EpisodesToAdd { get; } = new List<Episode>();

        public event Action OnCurrentIndexChanged;

        [DataMember]
        public ObservableCollectionEx<PlaylistEntry> Entries { get; }

        int currentIndex = -1;

        [DataMember]
        public int CurrentIndex
        {
            get { return currentIndex; }
            set
            {
                if (value >= Entries.Count)
                {
                    return;
                }

                if (value < -1)
                {
                    return;
                }

                currentIndex = value;

                Debug.WriteLine("Playlist moved current index to " + currentIndex);

                ForceBindingRefresh();

                DispatchManager.RunOnDispatcher(() =>
                {
                    if (CurrentEntry != null)
                    {
                        CurrentEntry.IsSelected = true;
                    }
                });
            }
        }

        public bool CanGoNext => CurrentIndex < Entries.Count - 1;

        public bool CanGoPrev => CurrentIndex > 0;

        public PlaylistState State { get; } = new PlaylistState();

        public static Playlist CurrentPlaylist { get; private set; }

        public Playlist()
        {
            Entries = new ObservableCollectionEx<PlaylistEntry>();
            Entries.CollectionChanged += Entries_CollectionChanged;
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CanGoNext));
            RaisePropertyChanged(nameof(CanGoPrev));
        }

        public PlaylistEntry CurrentEntry
        {
            get
            {
                if (CurrentIndex == -1 || Entries.Count <= CurrentIndex)
                {
                    return null;
                }

                var result = Entries[CurrentIndex];

                return result;
            }
            set
            {
                var index = Entries.IndexOf(value);
                CurrentIndex = index;
            }
        }

        public List<Episode> Episodes => Entries.Select(e => e.Episode).ToList();

        public Episode CurrentEpisode => CurrentEntry?.Episode;

        void ForceBindingRefresh()
        {
            DispatchManager.RunOnDispatcher(() =>
            {
                OnCurrentIndexChanged?.Invoke();
                RaisePropertyChanged(nameof(CanGoNext));
                RaisePropertyChanged(nameof(CanGoPrev));
                RaisePropertyChanged(nameof(CurrentEntry));
                RaisePropertyChanged(nameof(CurrentEpisode));

                if (CurrentEpisode != null)
                {
                    CoreTools.UpdateTile(CurrentEpisode);
                }
            });
        }

        public PlaylistEntry GetEntryForEpisode(Episode episode)
        {
            return Entries.FirstOrDefault(e => e.Episode == episode);
        }

        public PlaylistEntry GetEntryForEnclosure(string enclosure)
        {
            return Entries.FirstOrDefault(e => e.Enclosure == enclosure);
        }

        public PlaylistEntry GetEntryForMediaPlaybackItem(MediaPlaybackItem playbackItem)
        {
            return Entries.FirstOrDefault(e => e.AssociatedPlaybackItem == playbackItem);
        }

        public int GetEntryIndex(PlaylistEntry entry)
        {
            return Entries.IndexOf(entry);
        }

        public void Clear()
        {
            Entries.Clear();
            CurrentIndex = -1;
        }

        public void PlayEpisode(Episode episode)
        {
            var entry = GetEntryForEpisode(episode);

            if (entry == null)
            {
                Entries.Clear();
                entry = AddEpisode(episode);
            }
            CurrentEntry = entry;
        }

        public void RemoveEntry(PlaylistEntry entry)
        {
            DispatchManager.RunOnDispatcher(() =>
            {
                if (entry == null)
                {
                    return;
                }

                var currentEntry = CurrentEntry;
                var previousIndex = CurrentIndex;

                if (currentEntry == entry)
                {
                    if (previousIndex + 1 < Entries.Count)
                    {
                        currentEntry = Entries[previousIndex + 1];
                    }
                    else if (previousIndex > 0)
                    {
                        currentEntry = Entries[previousIndex - 1];
                    }
                }

                Entries.Remove(entry);

                if (Entries.Count == 0)
                {
                    CurrentIndex = -1;
                }
                else
                {
                    CurrentIndex = Entries.IndexOf(currentEntry);

                    if (previousIndex == CurrentIndex)
                    {
                        ForceBindingRefresh();
                    }
                }
            });
        }

        public void RemoveEpisode(Episode episode)
        {
            RemoveEntry(GetEntryForEpisode(episode));
        }

        public void AppendEpisode(Episode episode)
        {
            var entry = GetEntryForEpisode(episode);

            if (entry != null)
            {
                return;
            }

            AddEpisode(episode);
        }

        public PlaylistEntry AddEpisode(Episode episode, int? index = null)
        {
            var entry = GetEntryForEpisode(episode);

            if (entry != null)
            {
                return entry;
            }

            entry = new PlaylistEntry
            {
                Enclosure = episode.Enclosure,
                Position = episode.Position
            };

            episode.IsPlayed = false;

            if (index.HasValue)
            {
                Entries.Insert(index.Value, entry);
            }
            else
            {
                Entries.Add(entry);
            }

            RaisePropertyChanged(nameof(CanGoNext));
            RaisePropertyChanged(nameof(CanGoPrev));

            if (CurrentIndex == -1)
            {
                CurrentIndex = 0;
            }

            return entry;
        }

        public void AddEpisodes(IList<Episode> episodes, int? newIndex = null)
        {
            if (episodes.Count == 0)
            {
                return;
            }

            var entriesToAdd = new List<PlaylistEntry>();

            foreach (var episode in episodes)
            {
                var entry = GetEntryForEpisode(episode);

                if (entry != null)
                {
                    continue;
                }

                entry = new PlaylistEntry
                {
                    Enclosure = episode.Enclosure,
                    Position = episode.Position
                };
                episode.IsPlayed = false;
                entriesToAdd.Add(entry);
            }

            if (entriesToAdd.Count > 0)
            {
                Entries.AddRange(entriesToAdd);
            }

            RaisePropertyChanged(nameof(CanGoNext));
            RaisePropertyChanged(nameof(CanGoPrev));

            if (newIndex.HasValue)
            {
                CurrentIndex = newIndex.Value;
            }

            if (CurrentIndex == -1)
            {
                CurrentIndex = 0;
            }
        }

        public void MoveEntryUp(PlaylistEntry entry)
        {
            var index = GetEntryIndex(entry);
            var savedCurrentEpisode = CurrentEpisode;

            index--;

            if (index < 0)
            {
                return;
            }

            RemoveEntry(entry);
            AddEpisode(entry.Episode, index);

            CurrentEntry = GetEntryForEpisode(savedCurrentEpisode);
        }

        public void MoveEntryDown(PlaylistEntry entry)
        {
            var index = GetEntryIndex(entry);
            var savedCurrentEpisode = CurrentEpisode;

            index++;

            if (index >= Entries.Count)
            {
                return;
            }

            RemoveEntry(entry);
            AddEpisode(entry.Episode, index);

            CurrentEntry = GetEntryForEpisode(savedCurrentEpisode);
        }

        public void Reorder(bool asc)
        {
            var currentEpisode = CurrentEpisode;

            var newList = (asc ? Episodes.OrderBy(e => e.PublicationDate) : Episodes.OrderByDescending(e => e.PublicationDate)).ToList();

            Clear();

            var initialIndex = newList.IndexOf(currentEpisode);

            AddEpisodes(newList, initialIndex);
        }

        public static async Task<string> GetFromCloudAsync()
        {
            Debug.WriteLine("> Check playlist on the cloud");
            if (LocalSettings.Instance.ForceCloudSync || LocalSettings.Instance.CloudSync)
            {
                var uniqueUserID = AppSettings.Instance.UniqueUserId;

                try
                {
                    var fileDate = DateTime.MinValue;

                    if (!LocalSettings.Instance.ForceCloudSync)
                    {
                        if (await FileHelper.IsLocalFileExistsAsync("playlist.dat", false))
                        {
                            var libraryFile = await FileHelper.GetLocalFileAsync("playlist.dat", false);
                            var packagedFileBasicProperties = await libraryFile.GetBasicPropertiesAsync();

                            fileDate = DateTime.Parse(packagedFileBasicProperties.DateModified.ToString());
                        }
                    }

                    var oneDriveData = await OneDriveSettings.Instance.GetContentFromFileAsync("playlist.dat", fileDate);
                    if (oneDriveData != null)
                    {
                        return string.IsNullOrEmpty(oneDriveData) ? null : oneDriveData;
                    }

                    if (await OneDriveSettings.Instance.IsFileExists("library.dat"))
                    {
                        return null;
                    }

                    // Old DB
                    var castPlaylistsTable = Library.MobileServiceClient.GetTable<castPlaylists>();
                    var result = await castPlaylistsTable.Where(c => c.userId == uniqueUserID && c.Date > fileDate).ToListAsync();

                    if (result.Count > 0)
                    {
                        var entry = result[0];

                        return entry.Data.DecompressJson();
                    }
                }
                catch
                {
                    // Ignore error
                }
            }

            return null;
        }

        public static async Task DumpFromCloudAsync(string cloudData)
        {
            await SaveDataAsync(cloudData);
        }

        public static async Task PreparePlaylistAsync()
        {
            Debug.WriteLine("> Load playlist locally");
            var loadedPlaylist = await LoadFromFileAsync();

            if (CurrentPlaylist == null)
            {
                CurrentPlaylist = loadedPlaylist ?? new Playlist();
            }
            else
            {
                CurrentPlaylist.Clear();
                if (loadedPlaylist != null && loadedPlaylist.Entries.Count > 0)
                {
                    CurrentPlaylist.Entries.AddRange(loadedPlaylist.Entries.ToList());
                    CurrentPlaylist.CurrentIndex = loadedPlaylist.CurrentIndex;
                }
            }

            // Add waiting episodes
            CurrentPlaylist.AddEpisodes(EpisodesToAdd);

            // Cleanup
            var wrongEpisodes = CurrentPlaylist.Entries.Where(e => e.Episode == null).ToArray();
            foreach (var wrongEpisode in wrongEpisodes)
            {
                CurrentPlaylist.Entries.Remove(wrongEpisode);
            }

            // Check limits
            if (CurrentPlaylist.CurrentIndex >= CurrentPlaylist.Entries.Count)
            {
                CurrentPlaylist.CurrentIndex = CurrentPlaylist.Entries.Count - 1;
            }

            if (CurrentPlaylist.CurrentIndex < 0 && CurrentPlaylist.Entries.Count > 0)
            {
                CurrentPlaylist.CurrentIndex = 0;
            }
        }

        private static async Task<Playlist> LoadFromFileAsync()
        {
            Playlist result = null;
            try
            {
                if (await FileHelper.IsLocalFileExistsAsync("playlist.dat", false))
                {
                    var libraryFile = await FileHelper.GetLocalFileAsync("playlist.dat", false);
                    var playlist = (await FileIO.ReadTextAsync(libraryFile)).DecompressJson();

                    result = JsonConvert.DeserializeObject<Playlist>(playlist);
                }
            }
            catch
            {
                // Ignore error
            }

            return result;
        }

        public async Task PublishAsync()
        {
            if (!LocalSettings.Instance.CloudSync)
            {
                return;
            }
            
            try
            {
                var value = JsonConvert.SerializeObject(this);

                await OneDriveSettings.Instance.SaveContentAsync("playlist.dat", value);
            }
            catch
            {
                // Ignore error
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                await SaveDataAsync(JsonConvert.SerializeObject(this));
            }
            catch
            {
                // Ignore error
            }
        }

        public static async Task SaveDataAsync(string value)
        {
            // Local file
            var playlistFile = await FileHelper.GetOrCreateLocalFileAsync("playlist.dat.tmp", false);
            await FileIO.WriteTextAsync(playlistFile, value);
            await playlistFile.RenameAsync("playlist.dat", NameCollisionOption.ReplaceExisting);
        }
    }
}
