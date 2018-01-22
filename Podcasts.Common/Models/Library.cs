using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Connectivity;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Core;

namespace Podcasts
{
    public static class Library
    {
        static readonly MobileServiceClient mobileServiceClient = new MobileServiceClient("https://urzagatherer.azure-mobile.net/", "XWLSswZIfsbPfjflmKVVdoLPHvluzE72");
        public static ObservableCollection<Podcast> Podcasts { get; }
        public static ObservableCollection<Episode> DownloadedEpisodes { get; }

        static readonly ManualResetEvent readyEvent = new ManualResetEvent(false);
        static readonly ManualResetEvent fullRefreshEvent = new ManualResetEvent(false);

        private static bool SuccessfullyLoaded;
        private static bool SuccessfullyCreatedFromScratch;
        static bool RefreshInProgress;

        public static event Action OnFullRefreshDone;
        public static event Action<Episode> OnPlayedEpisode;
        public static event Action<Episode> OnEpisodeDownloadedStateChanged;

        static DateTime? lastFullRefresh;

        static Library()
        {
            Podcasts = new ObservableCollection<Podcast>();
            DownloadedEpisodes = new ObservableCollection<Episode>();
        }

        internal static void RaiseOnPlayedEpisode(Episode episode)
        {
            OnPlayedEpisode?.Invoke(episode);
        }

        internal static void RaiseOnEpisodeDownloadedStateChanged(Episode episode)
        {
            OnEpisodeDownloadedStateChanged?.Invoke(episode);
        }

        public static bool FullRefreshExecutedOnce
        {
            get;
            private set;
        }

        public static async Task RefreshAsync(bool checkDownloads)
        {
            foreach (var podcast in Podcasts)
            { 
                await podcast.RefreshAsync(false, checkDownloads, true);
            }
        }

        public static async Task RemovePodcastLocalDataAsync(Podcast podcast, bool silent, bool deleteFolder = true)
        {
            foreach (var episode in podcast.Episodes)
            {
                if (episode.IsAlreadyDownloaded)
                {
                    await episode.DeleteDownloadAsync(silent);
                    lock (DownloadedEpisodes)
                    {
                        DownloadedEpisodes.Remove(episode);
                    }
                }
                else if (episode.DownloadInProgress)
                {
                    episode.CancelDownload(silent);
                    lock (DownloadedEpisodes)
                    {
                        DownloadedEpisodes.Remove(episode);
                    }
                }
            }

            if (deleteFolder)
            {
                await FileHelper.DeleteFolderAsync(podcast.Root, true);
            }
        }

        public static void RemovePodcast(Podcast podcast, bool save = true)
        {
            Podcasts.Remove(podcast);

            if (save)
            {
                Save();
            }
        }

        public static Episode GetEpisodeByEnclosure(string enclosure)
        {
            return Podcasts.SelectMany(p => p.Episodes).FirstOrDefault(e => e.Enclosure == enclosure);
        }

        public static Episode GetEpisodeByUri(string enclosure)
        {
            try
            {
                return Podcasts.SelectMany(p => p.Episodes).FirstOrDefault(e => !string.IsNullOrEmpty(e.Enclosure) && new Uri(e.Enclosure).ToString() == enclosure);
            }
            catch
            {
                return null;
            }
        }

        public static bool ContainsFeedUrl(string podcastFeedUrl)
        {
            return Podcasts.Any(p => p.FeedUrl == podcastFeedUrl);
        }

        public static Podcast GetPodcastByFeedUrl(string podcastFeedUrl)
        {
            return Podcasts.FirstOrDefault(p => p.FeedUrl == podcastFeedUrl);
        }

        public static Podcast GetPodcastByRoot(string root)
        {
            return Podcasts.FirstOrDefault(p => p.Root == root);
        }

        public static string[] Categories
        {
            get { return Podcasts.Select(p => p.Category).Distinct().ToArray(); }
        }

        public static MobileServiceClient MobileServiceClient => mobileServiceClient;

        public static async Task SaveCastFile(StorageFile libraryFile = null)
        {
            bool needRename = false;
            if (libraryFile == null)
            {
                needRename = true;
                libraryFile = await FileHelper.GetOrCreateLocalFileAsync("library.dat.tmp", false);
            }

            await FileIO.WriteTextAsync(libraryFile, JsonConvert.SerializeObject(Podcasts));

            if (needRename)
            {
                await libraryFile.RenameAsync("library.dat", NameCollisionOption.ReplaceExisting);
            }
        }

        public static async Task SaveOPMLFile(StorageFile libraryFile)
        {
            var document = new Windows.Data.Xml.Dom.XmlDocument();

            var rootNode = document.CreateElement("opml");
            rootNode.AddAttribute("version", "1.1");
            document.AppendChild(rootNode);

            var headNode = document.CreateElement("head");
            headNode.AddChildWithInnerText("title", "Generated by Cast");
            headNode.AddChildWithInnerText("dateCreated", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            rootNode.AppendChild(headNode);

            var bodyNode = document.CreateElement("body");
            rootNode.AppendChild(bodyNode);

            foreach (var category in Categories)
            {
                var categoryNode = document.CreateElement("outline");
                bodyNode.AppendChild(categoryNode);
                categoryNode.AddAttribute("text", category);

                foreach (var podcast in Podcasts.Where(p => p.Category == category))
                {
                    var podcastNode = document.CreateElement("outline");
                    categoryNode.AppendChild(podcastNode);

                    podcastNode.AddAttribute("title", podcast.Title);
                    podcastNode.AddAttribute("type", "rss");
                    podcastNode.AddAttribute("text", podcast.FeedUrl);
                    podcastNode.AddAttribute("xmlUrl", podcast.FeedUrl);
                }
            }

            await document.SaveToFileAsync(libraryFile);
        }

        public static void RefreshAllPodcasts()
        {
            if (RefreshInProgress)
            {
                return;
            }

            if (Podcasts.Count == 0)
            {
                fullRefreshEvent.Set();
                return;
            }

            if (lastFullRefresh.HasValue)
            {
                if (DateTime.Now.Subtract(lastFullRefresh.Value).TotalDays < 1)
                {
                    fullRefreshEvent.Set();
                    return;
                }
            }

            lastFullRefresh = DateTime.Now;
            RefreshInProgress = true;

            Task.Run(async () =>
            {
                try
                {
                    fullRefreshEvent.Reset();

                    if (NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                    {
                        // background downloads
                        await ReconnectBackgroundDownloadsAsync();
                    }

                    var tasks = Podcasts.Select(async podcast =>
                    {
                        podcast.CheckDownloads();
                        await podcast.RefreshAsync(false, true, false);
                    }).ToList();

                    await Task.WhenAll(tasks);

                    if (LocalSettings.Instance.NewEpisodesCount > 0)
                    {
                        await SaveAsync();
                    }

                    OnFullRefreshDone?.Invoke();

                    fullRefreshEvent.Set();

                    FullRefreshExecutedOnce = true;
                }
                catch
                {
                    // Ignore error
                    fullRefreshEvent.Set();
                }

                RefreshInProgress = false;
            });
        }

        public static void Clear()
        {
            var podcasts = Podcasts.ToArray();

            foreach (var podcast in podcasts)
            {
                RemovePodcast(podcast, false);
            }
            Podcasts.Clear();
            lock (DownloadedEpisodes)
            {
                DownloadedEpisodes.Clear();
            }
        }

        public static async Task LoadCastFile(StorageFile libraryFile, bool merge)
        {
            SuccessfullyLoaded = false;

            Debug.WriteLine("> Deserialize podcasts");
            Notifier.BlockUpdates = true;

            Debug.WriteLine("> Load json file");
            var json = await FileIO.ReadTextAsync(libraryFile);
            var temp = JsonConvert.DeserializeObject<List<Podcast>>(json);

            Notifier.BlockUpdates = false;

            Debug.WriteLine("> Check downloads and local image");
            if (temp != null)
            {
                if (!merge)
                {
                    Clear();
                }

                foreach (var podcast in temp)
                {
                    if (GetPodcastByFeedUrl(podcast.FeedUrl) != null)
                    {
                        continue;
                    }

                    Podcasts.Add(podcast);
                    podcast.Clean();
                    podcast.CheckDownloads();
                    podcast.GetLocalImage();
                }
            }
            Debug.WriteLine("> Loaded episodes: " + Podcasts.SelectMany(p => p.Episodes).Count());
            Debug.WriteLine("> CheckZombieFolders");

            CheckZombieFolders();
            Debug.WriteLine("> Loading done");

            SuccessfullyLoaded = true;
        }

        public static async Task LoadOPMLFile(StorageFile libraryFile, bool merge)
        {
            var document = new Windows.Data.Xml.Dom.XmlDocument();
            var content = await FileIO.ReadTextAsync(libraryFile);
            document.LoadXml(content);

            var bodyNode = document.DocumentElement.GetChildByName("body");
            var categoriesNodes = bodyNode.ChildNodes.Where(n => n is Windows.Data.Xml.Dom.XmlElement).Cast<Windows.Data.Xml.Dom.XmlElement>().ToArray();

            SuccessfullyLoaded = false;
            if (!merge)
            {
                Clear();
            }

            foreach (var categoryNode in categoriesNodes)
            {
                if (categoryNode.HasChildNodes())
                {
                    var category = categoryNode.GetAttributeValue("text");
                    var podcastsNodes = categoryNode.ChildNodes.Where(n => n is Windows.Data.Xml.Dom.XmlElement).Cast<Windows.Data.Xml.Dom.XmlElement>().ToArray();
                    foreach (var podcastNode in podcastsNodes)
                    {
                        var feedUrl = podcastNode.GetAttributeValue("xmlUrl");

                        if (string.IsNullOrEmpty(feedUrl))
                        {
                            feedUrl = podcastNode.GetAttributeValue("text");
                        }

                        if (GetPodcastByFeedUrl(feedUrl) != null)
                        {
                            continue;
                        }

                        var podcast = await Podcast.ParseAsync(feedUrl, true);
                        if (podcast != null)
                        {
                            podcast.Category = category;
                            Podcasts.Add(podcast);
                            podcast.Clean();
                            podcast.CheckDownloads();
                            podcast.GetLocalImage();
                        }
                    }
                }
                else
                {
                    var feedUrl = categoryNode.GetAttributeValue("xmlUrl");

                    if (string.IsNullOrEmpty(feedUrl))
                    {
                        feedUrl = categoryNode.GetAttributeValue("text");
                    }

                    var podcast = await Podcast.ParseAsync(feedUrl, true);
                    if (podcast != null)
                    {
                        podcast.Category = StringsHelper.NoCategory;
                        Podcasts.Add(podcast);
                        podcast.CheckDownloads();
                        podcast.GetLocalImage();
                    }
                }
            }

            CheckZombieFolders();

            SuccessfullyLoaded = true;
        }

        public static async void CheckZombieFolders()
        {
            await Task.Run(async () =>
            {
                try
                {
                    // Cache
                    var folder = await FileHelper.GetLocalFolder(true);
                    var subfolders = (await folder.GetFoldersAsync()).ToArray();
                    foreach (var child in subfolders)
                    {
                        if (GetPodcastByRoot(child.Name) == null)
                        {
                            await child.DeleteAsync();                            
                        }
                    }

                    // Local
                    folder = await FileHelper.GetLocalFolder(false);
                    subfolders = (await folder.GetFoldersAsync()).ToArray();
                    foreach (var child in subfolders)
                    {
                        if (child.Name == "Microsoft" || child.Name == "HockeyApp") // Some known system folders
                        {
                            continue;
                        }

                        if (GetPodcastByRoot(child.Name) == null)
                        {
                            try
                            {
                                await child.DeleteAsync();
                            }
                            catch
                            {
                                // Need to ignore because of system folders
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore error
                }
            });
        }

        public static async Task SaveAsync()
        {
            if (!SuccessfullyLoaded)
            {
                return;
            }

            if (SuccessfullyCreatedFromScratch && Podcasts.Count == 0)
            {
                return;
            }

            try
            {
                await SaveCastFile();
            }
            catch
            {
                // Ignore errors
            }
        }

        public static async Task PublishAsync()
        {
            if (!SuccessfullyLoaded || !NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable || !LocalSettings.Instance.CloudSync)
            {
                return;
            }

            try
            {
                var data = JsonConvert.SerializeObject(Podcasts).Compress();
                await OneDriveSettings.Instance.SaveContentAsync("library.dat", data);
            }
            catch
            {
                // Ignore errors
            }
        }

        public static async void Save()
        {
            await SaveAsync();
        }

        public static async Task WaitReadyAsync()
        {
            await Task.Run(() =>
            {
                readyEvent.WaitOne(2000);
            });
        }

        public static async Task WaitFullRefreshAsync()
        {
            await Task.Run(() =>
            {
                fullRefreshEvent.WaitOne();
            });
        }

        static async Task DeserializeFromFileAsync()
        {
            try
            {
                Debug.WriteLine("> Load library locally");

                if (await FileHelper.IsLocalFileExistsAsync("library.dat", false))
                {
                    var libraryFile = await FileHelper.GetLocalFileAsync("library.dat", false);
                    await LoadCastFile(libraryFile, false);
                }
                else
                {
                    SuccessfullyLoaded = true;
                    SuccessfullyCreatedFromScratch = true;
                }
            }
            catch
            {
                SuccessfullyLoaded = false;
            }
        }

        internal async static void AddDownloadedEpisode(Episode episode)
        {
            if (Notifier.Dispatcher == null)
            {
                return;
            }

            if (Notifier.Dispatcher.HasThreadAccess)
            {
                lock (DownloadedEpisodes)
                {
                    if (DownloadedEpisodes.Contains(episode))
                    {
                        return;
                    }
                    DownloadedEpisodes.Add(episode);
                }
            }
            else
            {
                await Notifier.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (DownloadedEpisodes)
                    {
                        if (DownloadedEpisodes.Contains(episode))
                        {
                            return;
                        }
                        DownloadedEpisodes.Add(episode);
                    }
                });
            }
        }

        internal static async void RemoveDownloadedEpisode(Episode episode)
        {
            if (Notifier.Dispatcher == null)
            {
                return;
            }

            if (Notifier.Dispatcher.HasThreadAccess)
            {
                lock (DownloadedEpisodes)
                {
                    if (!DownloadedEpisodes.Contains(episode))
                    {
                        return;
                    }
                    DownloadedEpisodes.Remove(episode);
                }
            }
            else
            {
                await Notifier.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (DownloadedEpisodes)
                    {
                        if (!DownloadedEpisodes.Contains(episode))
                        {
                            return;
                        }
                        DownloadedEpisodes.Remove(episode);
                    }
                });
            }
        }

        public static async Task<string> GetFromCloudAsync()
        {
            var uniqueUserID = AppSettings.Instance.UniqueUserId;

            Debug.WriteLine("> Load library on OneDrive");

            if (LocalSettings.Instance.CloudSync)
            {
                try
                {
                    var fileDate = DateTime.MinValue;

                    if (!LocalSettings.Instance.ForceCloudSync)
                    {
                        if (await FileHelper.IsLocalFileExistsAsync("library.dat", false))
                        {
                            var libraryFile = await FileHelper.GetLocalFileAsync("library.dat", false);
                            var packagedFileBasicProperties = await libraryFile.GetBasicPropertiesAsync();
                            fileDate = DateTime.Parse(packagedFileBasicProperties.DateModified.ToString());
                        }
                    }

                    var oneDriveData = await OneDriveSettings.Instance.GetContentFromFileAsync("library.dat", fileDate);
                    if (oneDriveData != null)
                    {
                        return string.IsNullOrEmpty(oneDriveData) ? null : oneDriveData;
                    }

                    // Old DB
                    var castsTable = mobileServiceClient.GetTable<casts>();

                    var result = await castsTable.Where(c => c.userId == uniqueUserID && c.Date > fileDate).ToListAsync();

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
            var temp = JsonConvert.DeserializeObject<ObservableCollection<Podcast>>(cloudData);

            Clear();

            foreach (var podcast in temp)
            {
                Podcasts.Add(podcast);
                podcast.CheckDownloads();
                podcast.GetLocalImage();
            }

            CheckZombieFolders();
            await SaveAsync();

            await DeserializeAsync(true);
        }

        public static async Task DeserializeAsync(bool refresh = true)
        {
            await DeserializeFromFileAsync();
            await Playlist.PreparePlaylistAsync();

            if (refresh)
            {
                Debug.WriteLine("> Refresh all podcasts");
                lastFullRefresh = null;
                RefreshAllPodcasts();
            }

            readyEvent.Set();
        }

        public static int UnplayedCount
        {
            get
            {
                return Podcasts.SelectMany(p => p.Episodes).Count(e => !e.IsPlayed);
            }
        }

        static async Task CancelDownload(DownloadOperation download)
        {
            try
            {
                download.AttachAsync().Cancel();
                await download.ResultFile.DeleteAsync();
            }
            catch
            {
                // Ignore error
            }
        }

        public static async Task ReconnectBackgroundDownloadsAsync()
        {
            try
            {
                var downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
                foreach (DownloadOperation download in downloads)
                {
                    var requestUri = download.RequestedUri.ToString();

                    var episode = GetEpisodeByUri(requestUri);

                    if (episode == null || Path.GetFileName(episode.LocalFilename) + ".tmp" != download.ResultFile.Name)
                    {
                        await CancelDownload(download);
                        continue;
                    }

                    if (episode.DownloadAttached)
                    {
                        continue;
                    }

                    episode.AttachAsync(download);
                }
            }
            catch
            {
                // Ignore
            }
        }

        public static async Task<bool> DeletePodcast(Podcast podcast)
        {
            if (await Messenger.QuestionAsync(StringsHelper.Confirm_PodcastDelete))
            {
                foreach (var episode in podcast.Episodes)
                {
                    Playlist.CurrentPlaylist.RemoveEpisode(episode);

                    if (episode.DownloadInProgress)
                    {
                        episode.CancelDownload();
                    }
                }
                await Playlist.CurrentPlaylist.SaveAsync();
                RemovePodcast(podcast);
                CheckZombieFolders();

                return true;
            }

            return false;
        }
    }
}
