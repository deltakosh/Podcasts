using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Connectivity;
using Newtonsoft.Json;
using Windows.Media;
using Windows.Media.Casting;
using Windows.Media.Playback;
using Windows.Networking.Connectivity;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Podcasts
{
    public static class MediaPlayerHost
    {
        public static event Action OnVideoPlayerEngaged;
        public static event Action OnVideoPlayerDisengaged;

        static MediaPlaybackList playbackList;

        static readonly MediaPlayer Player;

        static bool videoPlayInProgess;

        static bool NetworkAvailabilityCheckInProgress;

        static object lockObject = new object();
        static ManualResetEvent lockEvent = new ManualResetEvent(true);
        static int pendingOrders = 0;
        static SemaphoreSlim workSemaphore = new SemaphoreSlim(1);
        static bool pendingIndexChange = false;

        static DispatcherTimer timer;

        static MediaPlaybackItem syncedItem;

        static double trackedPosition;

        static bool started;

        static MediaPlaybackState previousState = MediaPlaybackState.None;
        static LastMediaOrder lastOrderReceived = LastMediaOrder.None;

        public static CastingSource CastingSource => Player.GetAsCastingSource();

        static MediaPlayerHost()
        {
            Player = new MediaPlayer();
            Player.CommandManager.PlayReceived += CommandManager_PlayReceived;
            Player.CommandManager.PauseReceived += CommandManager_PauseReceived;

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Interval = TimeSpan.FromSeconds(1);
        }

        public static bool ForcePlayNext
        {
            get;set;
        }

        private static void CommandManager_PauseReceived(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerPauseReceivedEventArgs args)
        {
            args.Handled = true;
            Pause(true);
        }

        private static void CommandManager_PlayReceived(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerPlayReceivedEventArgs args)
        {
            args.Handled = true;

            //if ((App.Current as App).IsInBackground)
            //{
            //    await Playlist.PreparePlaylistAsync();
            //    return;
            //}

            Play();
        }

        public static void Attach(MediaPlayerElement mediaPlayerElement)
        {
            mediaPlayerElement.SetMediaPlayer(Player);
        }

        public static async Task StartAsync()
        {
            if (started)
            {
                return;
            }

            try
            {
                started = true;
                Player.Volume = LocalSettings.Instance.Volume / 100.0;
                Player.PlaybackSession.PlaybackStateChanged += Current_CurrentStateChanged;
                Player.MediaFailed += Player_MediaFailed;

                Player.CommandManager.NextReceived += CommandManager_NextReceived;
                Player.CommandManager.PreviousReceived += CommandManager_PreviousReceived;

                LocalSettings.Instance.PropertyChanged += Instance_PropertyChanged;

                SyncPlayerCommandManager();

                Playlist.CurrentPlaylist.Entries.CollectionChanged += Entries_CollectionChanged;
                Playlist.CurrentPlaylist.OnCurrentIndexChanged += CurrentPlaylist_OnCurrentIndexChanged;

                Library.OnEpisodeDownloadedStateChanged += Library_OnEpisodeDownloadedStateChanged;

                if (Playlist.CurrentPlaylist.Entries.Count > 0)
                {
                    await CreatePlaylistAsync();
                }

                timer.Start();
            }
            catch
            {
                BackgroundMediaPlayer.Shutdown();
            }
        }

        public static void ResetLastOrderReceived()
        {
            lastOrderReceived = LastMediaOrder.None;
            ForcePlayNext = true;
        }

        private static async void Library_OnEpisodeDownloadedStateChanged(Episode episode)
        {
            await Task.Run(async () =>
            {
                await WaitForPendingOrders();
                workSemaphore.Wait();

                try
                {
                    Debug.WriteLine("Replaced episode: " + episode.Enclosure);
                    await SwitchAsync(episode);
                }
                finally
                {
                    workSemaphore.Release();
                }

            });
        }

        private static async void Timer_Tick(object sender, object e)
        {
            if (playbackList == null)
            {
                return;
            }

            if (Playlist.CurrentPlaylist.CurrentEntry == null)
            {
                return;
            }

            if (Player.Source != playbackList)
            {
                return;
            }

            bool force = false;
            if ((Player.PlaybackSession.PlaybackState == MediaPlaybackState.Buffering || Player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused) && lastOrderReceived != LastMediaOrder.Pause)
            {
                if (trackedPosition != 0 && trackedPosition != Position && Position > 1)
                {
                    force = true;
                    Debug.WriteLine("Forcing playing state..");
                    SyncState(MediaPlaybackState.Playing);
                }

                trackedPosition = Position;
            }

            if (force || Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                var currentEntry = CurrentEntry;
                var currentPosition = Position;
                SyncState(MediaPlaybackState.Playing);
                await Task.Run(() =>
                {
                    workSemaphore.Wait();
                    try
                    {
                        SaveCurrentPosition(currentEntry, currentPosition);
                    }
                    finally
                    {
                        workSemaphore.Release();
                    }
                });
            }
            else
            {
                SyncState(Player.PlaybackSession.PlaybackState);
            }
        }

        static PlaylistEntry CurrentEntry
        {
            get
            {
                if (playbackList == null || Playlist.CurrentPlaylist == null)
                {
                    return null;
                }

                return Playlist.CurrentPlaylist.GetEntryForMediaPlaybackItem(syncedItem);
            }
        }

        static Task WaitForPendingOrders()
        {
            return Task.Run(() => lockEvent.WaitOne());
        }

        private static void CurrentPlaylist_OnCurrentIndexChanged()
        {
            SyncCurrentIndex();
        }

        private static async void SyncCurrentIndex()
        {
            pendingIndexChange = true;
            await Task.Run(async () =>
            {
                await WaitForPendingOrders();
                workSemaphore.Wait();

                try
                {
                    if (Playlist.CurrentPlaylist.CurrentIndex > -1)
                    {
                        Debug.WriteLine("Switching to index #" + Playlist.CurrentPlaylist.CurrentIndex);
                        Debug.WriteLine("Switching to episode " + Playlist.CurrentPlaylist.CurrentEpisode.Title);
                        MoveTo(Playlist.CurrentPlaylist.CurrentEntry);
                    }
                }
                finally
                {
                    workSemaphore.Release();
                }
            });
            pendingIndexChange = false;
        }

        private static async void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (playbackList == null && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                return;
            }

            lock (lockObject)
            {
                if (pendingOrders == 0)
                {
                    lockEvent.Reset();
                }

                pendingOrders++;
            }

            Debug.WriteLine("Begin->Pending order count: " + pendingOrders);

            await Task.Run(async () =>
            {
                workSemaphore.Wait();
                try
                {
                    if (playbackList == null)
                    {
                        await CreatePlaylistAsync();
                    }
                    else
                    {
                        switch (e.Action)
                        {
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                                {
                                    foreach (PlaylistEntry entry in e.NewItems)
                                    {
                                        await AddSourceToPlaylistAsync(entry, Playlist.CurrentPlaylist.GetEntryIndex(entry));
                                    }
                                    ForcePlayNext = false;
                                    break;
                                }
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                                {
                                    SaveCurrentPosition();
                                    foreach (PlaylistEntry entry in e.OldItems)
                                    {
                                        RemoveFromSource(entry);
                                    }
                                    break;
                                }
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                                {
                                    SaveCurrentPosition();
                                    Pause(false);
                                    Player.Source = null;
                                    playbackList = null;
                                    break;
                                }
                        }
                    }
                }
                finally
                {
                    workSemaphore.Release();
                }

                lock (lockObject)
                {
                    pendingOrders--;

                    if (pendingOrders == 0)
                    {
                        lockEvent.Set();
                    }
                }

                Debug.WriteLine("End->Pending order count: " + pendingOrders);
            });
        }

        private static void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            CoreTools.ShowDebugToast(args.ErrorMessage, "Player_MediaFailed");
            App.TrackEvent(args.ErrorMessage);
            App.TrackException(args.ExtendedErrorCode);

            SaveCurrentPosition();

            // If network related...
            if (!NetworkAvailabilityCheckInProgress && !NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            {
                NetworkAvailabilityCheckInProgress = true;
                NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
            }
        }

        private static async void NetworkInformation_NetworkStatusChanged(object sender)
        {
            if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            {
                NetworkInformation.NetworkStatusChanged -= NetworkInformation_NetworkStatusChanged;

                Pause(false);
                Player.Source = null;
                playbackList = null;
                await CreatePlaylistAsync();

                NetworkAvailabilityCheckInProgress = false;
            }
        }

        static void SyncPlayerCommandManager()
        {
            var rule = LocalSettings.Instance.InvertSkipControls ? MediaCommandEnablingRule.Always : MediaCommandEnablingRule.Auto;

            Player.CommandManager.PreviousBehavior.EnablingRule = rule;
            Player.CommandManager.NextBehavior.EnablingRule = rule;
        }

        private static void Instance_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "InvertSkipControls")
            {
                SyncPlayerCommandManager();
            }
        }

        private static void CommandManager_PreviousReceived(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerPreviousReceivedEventArgs args)
        {
            if (LocalSettings.Instance.InvertSkipControls && Position > AppSettings.Instance.RewindStep)
            {
                args.Handled = true;
                ForcePosition(Position - AppSettings.Instance.RewindStep);
            }
        }

        private static void CommandManager_NextReceived(MediaPlaybackCommandManager sender, MediaPlaybackCommandManagerNextReceivedEventArgs args)
        {
            if (LocalSettings.Instance.InvertSkipControls && (Duration - Position > AppSettings.Instance.ForwardStep))
            {
                args.Handled = true;
                ForcePosition(Position + AppSettings.Instance.ForwardStep);
            }
        }

        static async void RaiseVideoPlayerEngaged()
        {
            await DispatchManager.RunOnDispatcherAsync(() =>
            {
                try
                {
                    OnVideoPlayerEngaged?.Invoke();
                }
                catch
                {
                    // Ignore error
                }
            });
        }

        static async void RaiseVideoPlayerDisengaged()
        {
            await DispatchManager.RunOnDispatcherAsync(() =>
            {
                try
                {
                    OnVideoPlayerDisengaged?.Invoke();
                }
                catch
                {
                    // Ignore error
                }
            });
        }

        static void CheckPlaylistState(MediaPlaybackList sender)
        {
            var newItem = sender.CurrentItem;

            if (newItem == null || pendingIndexChange)
            {
                return;
            }

            Playlist.CurrentPlaylist.CurrentEntry = Playlist.CurrentPlaylist.GetEntryForMediaPlaybackItem(newItem);
        }

        internal static async Task SwitchAsync(Episode episode)
        {
            if (playbackList == null)
            {
                return;
            }
            var entry = Playlist.CurrentPlaylist.GetEntryForEpisode(episode);

            if (entry == null)
            {
                return;
            }

            if (entry.IsStreaming && !episode.IsAlreadyDownloaded)
            {
                return;
            }

            if (!entry.IsStreaming && episode.IsAlreadyDownloaded)
            {
                return;
            }

            bool needToPlay = false;
            if (entry.IsSelected && previousState == MediaPlaybackState.Playing)
            {
                Pause(false);
                SaveCurrentPosition();
                needToPlay = true;
            }
            var entryIndex = Playlist.CurrentPlaylist.Entries.IndexOf(entry);
            if (playbackList.Items.Count <= entryIndex)
            {
                return;
            }

            pendingIndexChange = true;

            playbackList.Items.RemoveAt(entryIndex);
            await AddSourceToPlaylistAsync(entry, entryIndex);

            if (entry.IsSelected)
            {
                MoveTo(entry);

                if (needToPlay)
                {
                    Play();
                }
            }

            pendingIndexChange = false;
        }

        private static async void PlaybackList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            try
            {
                if (args.NewItem == null)
                {
                    return;
                }
              
                var currentItem = sender.CurrentItem;
                if (currentItem == null)
                {
                    return;
                }

                CheckPlaylistState(sender);

                syncedItem = playbackList.CurrentItem;

                RestorePosition();

                if (CurrentEntry == null)
                {
                    return;
                }

                Debug.WriteLine("Switched to episode#" + CurrentEntry.Episode.Title);

                if (currentItem.Source.Duration.HasValue)
                {
                    CurrentEntry.Duration = currentItem.Source.Duration.Value.TotalSeconds;
                }
                else
                {
                    CurrentEntry.Duration = 0;
                }

                if (LocalSettings.Instance.VideoPlayback && args.NewItem.VideoTracks.Count > 0)
                {
                    videoPlayInProgess = true;
                    RaiseVideoPlayerEngaged();
                }
                else if (videoPlayInProgess)
                {
                    videoPlayInProgess = false;
                    RaiseVideoPlayerDisengaged();
                }

                // Playback rate
                try
                {
                    Player.PlaybackSession.PlaybackRate = LocalSettings.Instance.PlaySpeed / 100.0;
                }
                catch (Exception)
                {
                    LocalSettings.Instance.PlaySpeed = 100;
                }
                
                if (!(Application.Current as App).IsInBackground && CurrentEntry != null && CurrentEntry.IsStreaming && NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable && NetworkHelper.Instance.ConnectionInformation.IsInternetOnMeteredConnection && !LocalSettings.Instance.StreamOnMetered)
                {
                    await DispatchManager.RunOnDispatcherAsync(async () =>
                    {
                        Pause(false);
                        await App.MessageAsync(StringsHelper.StreamingIsDisabled);
                    });
                }

                SyncState(Player.PlaybackSession.PlaybackState);
            }
            catch (Exception ex)
            {
                App.TrackException(ex);
                CoreTools.ShowDebugToast(ex.Message, "PlaybackList_CurrentItemChanged");
            }
        }

        static void RestorePosition()
        {
            var currentEntry = CurrentEntry;

            if (currentEntry == null)
            {
                return;
            }

            var newPosition = currentEntry.Position;

            if (currentEntry.Duration > 1 && (currentEntry.Duration - newPosition < 5))
            {
                newPosition = 0;
            }

            Position = newPosition;

            if (ForcePlayNext || Player.AutoPlay && lastOrderReceived != LastMediaOrder.Pause)
            {
                Play();
            }
        }

        static async Task AddSourceToPlaylistAsync(PlaylistEntry entry, int? index = null)
        {
            var source = await entry.GetSourceAsync();

            if (source == null)
            {
                return;
            }

            source.CustomProperties["entry"] = JsonConvert.SerializeObject(entry);

            var item = new MediaPlaybackItem(source);
            var displayInfo = item.GetDisplayProperties();
            displayInfo.MusicProperties.Artist = entry.Episode.Author;
            displayInfo.MusicProperties.AlbumArtist = entry.Episode.Author;
            displayInfo.MusicProperties.Title = entry.Episode.Title;
            displayInfo.MusicProperties.AlbumTitle = entry.PodcastTitle ?? "";
            displayInfo.Type = MediaPlaybackType.Music;
            try
            {
                var albumArtUri = new Uri(entry.Episode.PictureUrl);
                displayInfo.Thumbnail = RandomAccessStreamReference.CreateFromUri(albumArtUri);
            }
            catch
            {
                // Ignore error. Uri could be malformed or weird.
            }

            item.ApplyDisplayProperties(displayInfo);

            lock (Player)
            {
                if (index.HasValue && playbackList.Items.Count > index.Value)
                {
                    playbackList.Items.Insert(index.Value, item);
                }
                else
                {
                    playbackList.Items.Add(item);
                }
            }

            entry.AssociatedPlaybackItem = item;
        }

        static async Task CreatePlaylistAsync()
        {
            try
            {
                if (Playlist.CurrentPlaylist.Entries.Count == 0)
                {
                    return;
                }

                if (playbackList != null)
                {
                    playbackList.CurrentItemChanged -= PlaybackList_CurrentItemChanged;
                }

                playbackList = new MediaPlaybackList();
                playbackList.CurrentItemChanged += PlaybackList_CurrentItemChanged;

                var currentList = Playlist.CurrentPlaylist.Entries.ToList();
                foreach (var entry in currentList)
                {
                    await AddSourceToPlaylistAsync(entry);
                }

                // Set playlist
                if (playbackList.Items.Count == 0)
                {
                    return;
                }

                var currentIndex = Math.Min(Playlist.CurrentPlaylist.Entries.Count - 1, Math.Max(0, Playlist.CurrentPlaylist.CurrentIndex));

                playbackList.StartingItem = playbackList.Items[currentIndex];

                Position = Playlist.CurrentPlaylist.Entries[currentIndex].Position;
                var shouldAutoPlay = ForcePlayNext || AppSettings.Instance.AutoPlay;
                Player.AutoPlay = shouldAutoPlay;
                Player.Source = playbackList;

                if (shouldAutoPlay)
                {
                    Play();
                }

                ForcePlayNext = false;
            }
            catch (Exception ex)
            {
                App.TrackException(ex);
                CoreTools.ShowDebugToast(ex.Message, "CreatePlaylistAsync");
            }
        }

        static void SyncState(MediaPlaybackState state)
        {
            if (previousState == state)
            {
                return;
            }

            previousState = state;
            trackedPosition = 0;
            Debug.WriteLine("Playback state:" + state);
            switch (state)
            {
                case MediaPlaybackState.None:
                    Playlist.CurrentPlaylist.State.IsStreaming = false;
                    Playlist.CurrentPlaylist.State.IsPlaying = false;
                    Playlist.CurrentPlaylist.State.IsLoading = false;
                    break;
                case MediaPlaybackState.Buffering:
                    Playlist.CurrentPlaylist.State.IsStreaming = true;
                    Playlist.CurrentPlaylist.State.IsPlaying = false;
                    Playlist.CurrentPlaylist.State.IsLoading = false;
                    break;
                case MediaPlaybackState.Opening:
                    Playlist.CurrentPlaylist.State.IsStreaming = true;
                    Playlist.CurrentPlaylist.State.IsPlaying = false;
                    Playlist.CurrentPlaylist.State.IsLoading = true;
                    break;
                case MediaPlaybackState.Paused:
                    Playlist.CurrentPlaylist.State.IsStreaming = false;
                    Playlist.CurrentPlaylist.State.IsPlaying = false;
                    Playlist.CurrentPlaylist.State.IsLoading = false;
                    break;
                case MediaPlaybackState.Playing:
                    Playlist.CurrentPlaylist.State.IsStreaming = false;
                    Playlist.CurrentPlaylist.State.IsPlaying = true;
                    Playlist.CurrentPlaylist.State.IsLoading = false;
                    break;
            }
        }

        private static void Current_CurrentStateChanged(MediaPlaybackSession sender, object args)
        {
            SyncState(Player.PlaybackSession.PlaybackState);
        }

        public static double RemainingTimeInSeconds => Player.PlaybackSession.NaturalDuration.TotalSeconds - Position;

        public static double Volume
        {
            get
            {
                return LocalSettings.Instance.Volume;
            }
            set
            {
                Player.Volume = value / 100.0;
                LocalSettings.Instance.Volume = value;
            }
        }

        public static double Speed
        {
            get
            {
                return Player.PlaybackSession.PlaybackRate * 100.0;
            }
            set
            {
                try
                {
                    Player.PlaybackSession.PlaybackRate = value / 100.0;
                }
                catch
                {
                    // Ignore error
                }
            }
        }

        public static int CurrentIndex => (int)playbackList.CurrentItemIndex;

        public static void ForcePosition(double value)
        {
            if (value > Duration || value <= 0)
            {
                return;
            }

            if (Math.Abs(Position - value) < 2)
            {
                return;
            }

            if (syncedItem != playbackList.CurrentItem)
            {
                return;
            }

            if (Playlist.CurrentPlaylist.CurrentEntry == null)
            {
                return;
            }

            if (syncedItem != Playlist.CurrentPlaylist.CurrentEntry.AssociatedPlaybackItem)
            {
                return;
            }

            Debug.WriteLine("Position forced to " + value);

            Position = value;
        }

        public static double Position
        {
            get
            {
                try
                {
                    return Player.PlaybackSession.Position.TotalSeconds;
                }
                catch
                {
                    return 0;
                }
            }
            set
            {
                Player.PlaybackSession.Position = TimeSpan.FromSeconds(value);
                SaveCurrentPosition();
            }
        }

        public static double Duration => Math.Max(1, Player.PlaybackSession.NaturalDuration.TotalSeconds);

        public static bool IsPaused => Player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused;

        public static void RemoveFromSource(PlaylistEntry entry)
        {
            if (playbackList != null)
            {
                if (entry == null)
                {
                    return;
                }
                playbackList.Items.Remove(entry.AssociatedPlaybackItem);

                if (playbackList.Items.Count == 0)
                {
                    Player.Source = null;
                    playbackList = null;
                }
            }
        }

        public static void Play()
        {
            try
            {
                Debug.WriteLine("Play");
                if (Player.PlaybackSession.PlaybackState == MediaPlaybackState.None || Player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                {
                    Player.Play();
                }
                lastOrderReceived = LastMediaOrder.Play;
                ForcePlayNext = false;
            }
            catch (Exception ex)
            {
                CoreTools.ShowDebugToast(ex.Message, "Play");
            }
        }

        public static async void Pause(bool publish)
        {
            try
            {
                Debug.WriteLine("Pause");
                Player.Pause();
                lastOrderReceived = LastMediaOrder.Pause;

                await Playlist.CurrentPlaylist.SaveAsync();
                if (publish)
                {
                    await Playlist.CurrentPlaylist.PublishAsync();
                }
            }
            catch (Exception ex)
            {
                CoreTools.ShowDebugToast(ex.Message, "Pause");
            }
        }

        public static void SaveCurrentPosition(PlaylistEntry currentEntry = null, double? currentPosition = null, bool force = false)
        {
            try
            {
                if (!currentPosition.HasValue)
                {
                    currentPosition = Position;
                }

                if (currentEntry == null)
                {
                    currentEntry = CurrentEntry;
                }

                if (!force)
                {
                    if (Playlist.CurrentPlaylist == null || playbackList?.CurrentItem == null ||
                        previousState != MediaPlaybackState.Playing)
                    {
                        return;
                    }
                }

                if (currentEntry != null && currentEntry.Duration > 1 && currentPosition > 1)
                {
                   // Debug.WriteLine("SaveCurrentPosition to " + currentEntry.Episode.Title + "(" + currentPosition + ")");
                    currentEntry.Position = currentPosition.Value;

                    if ((currentEntry.Duration >= currentPosition) && (currentPosition / currentEntry.Duration) > 0.95 && currentEntry.Episode != null)
                    {
                        currentEntry.Episode.IsPlayed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                App.TrackException(ex);
                CoreTools.ShowDebugToast(ex.Message, "SaveCurrentPosition");
            }
        }

        static void MoveTo(PlaylistEntry entry)
        {
            if (playbackList != null)
            {
                SaveCurrentPosition();

                var index = playbackList.Items.IndexOf(entry.AssociatedPlaybackItem);

                if (index >= 0 && playbackList.CurrentItemIndex != index && index < playbackList.Items.Count)
                {
                    Debug.WriteLine("Moving to:" + index);

                    try
                    {
                        trackedPosition = 0;
                        SyncState(MediaPlaybackState.Buffering);
                        playbackList.MoveTo((uint)index);
                    }
                    catch (Exception ex)
                    {
                        CoreTools.ShowDebugToast(ex.Message, "MoveTo");
                    }
                }
            }
        }
    }
}
