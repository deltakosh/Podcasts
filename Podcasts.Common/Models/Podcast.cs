using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Windows.Data.Xml.Dom;
using Newtonsoft.Json;
using Windows.UI.Core;
using Microsoft.Toolkit.Uwp;
using System.Diagnostics;

namespace Podcasts
{
    [DataContract]
    public class Podcast : Notifier
    {
        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public string Link { get; set; }
        [DataMember]
        public string FeedUrl { get; set; }
        [DataMember]
        public string Login { get; set; }
        [DataMember]
        public string Password { get; set; }

        [DataMember]
        public string Image { get; set; }

        public string LocalImage { get; set; }

        [DataMember]
        public string Category { get; set; }

        private string root;
        private bool imageDownloadInProgress;

        [DataMember]
        public string Root
        {
            get
            {                
                if (string.IsNullOrEmpty(root) && IsInLibrary)
                {
                    var sanitizedRoot = CoreTools.SanitizeFilename(Title);
                    root = sanitizedRoot;
                    FileHelper.CreateLocalFolder(root, true);
                    FileHelper.CreateLocalFolder(root, false);
                }
                return root;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && IsInLibrary)
                {
                    var sanitizedRoot = CoreTools.SanitizeFilename(Title);
                    if (value != sanitizedRoot)
                    {
                        FileHelper.RenameLocalFolder(value, sanitizedRoot, true);
                        FileHelper.RenameLocalFolder(value, sanitizedRoot, false);
                        root = sanitizedRoot;
                        return;
                    }
                }
                root = value;
            }
        }

        [DataMember]
        public ObservableCollection<Episode> Episodes
        {
            get; set;
        }

        public Podcast()
        {
            Episodes = new ObservableCollection<Episode>();
        }

        public bool HasUnplayed => Episodes.Any(e => !e.IsPlayed);
        public int UnplayedCount => Episodes.Sum(e => e.IsPlayed ? 0 : 1);

        public bool IsInLibrary => !string.IsNullOrEmpty(Category);

        public void RaisePlayedCountChanged()
        {
            RaisePropertyChanged(nameof(HasUnplayed));
            RaisePropertyChanged(nameof(UnplayedCount));
        }

        public Episode GetPreviousEpisode(Episode currentEpisode)
        {
            var index = Episodes.IndexOf(currentEpisode);

            if (index == 0)
            {
                return currentEpisode;
            }

            return Episodes[index - 1];
        }

        public Episode GetNextEpisode(Episode currentEpisode)
        {
            var index = Episodes.IndexOf(currentEpisode);

            if (index == Episodes.Count - 1)
            {
                return currentEpisode;
            }

            return Episodes[index + 1];
        }

        public Episode GetEpisodeByEnclosure(string enclosure)
        {
            return Episodes.FirstOrDefault(e => e.Enclosure == enclosure);
        }

        Action toExecuteWhenReady;
        public void ExecuteWhenReady(Action action)
        {
            lock (Episodes)
            {
                if (Episodes.Count == 0)
                {
                    toExecuteWhenReady = action;
                    return;
                }

                action();
            }
        }

        public override string ToString()
        {
            //var temp2 = JsonConvert.SerializeObject(this);
            //var temp = SonSerializer.Serialize(this);

            //return temp2;
            return JsonConvert.SerializeObject(this);
        }

        public void Clean()
        {
            foreach (var episode in Episodes)
            {
                episode.Clean();
            }
        }

        public async void GetLocalImage()
        {
            if (!IsInLibrary)
            {
                LocalImage = Image;
                RaisePropertyChanged(nameof(LocalImage));
                return;
            }

            await Task.Run(async () =>
            {
                lock(this)
                {
                    if (imageDownloadInProgress)
                    {
                        return;
                    }

                    imageDownloadInProgress = true;
                }

                try
                {
                    if (LocalImage != null)
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(Image))
                    {
                        LocalImage = "ms-appx:///Assets/IconFull.png";
                        RaisePropertyChanged(nameof(LocalImage));
                        return;
                    }

                    var questionMarkPosition = Image.IndexOf("?", StringComparison.Ordinal);

                    var imageFile = Path.Combine(Root, questionMarkPosition == -1 ? Path.GetFileName(Image) : Path.GetFileName(Image.Substring(0, questionMarkPosition)));

                    if (!await FileHelper.IsLocalFileExistsAsync(imageFile, false))
                    {
                        if (!await CoreTools.DownloadDirectToFile(Image, imageFile, false))
                        {
                            LocalImage = "ms-appx:///Assets/IconFull.png";
                            RaisePropertyChanged(nameof(LocalImage));
                            return;
                        }
                    }

                    if (string.IsNullOrEmpty(LocalImage))
                    {
                        LocalImage = "ms-appdata:///local/" + imageFile;
                        RaisePropertyChanged(nameof(LocalImage));
                    }
                }
                catch
                {
                    // Ignore error
                    LocalImage = "ms-appx:///Assets/IconFull.png";
                    RaisePropertyChanged(nameof(LocalImage));
                }
                finally
                {
                    lock (this)
                    {
                        imageDownloadInProgress = false;
                    }
                }
            });
        }

        public void ReOrder()
        {
            if (Episodes == null || Episodes.Count == 0)
            {
                return;
            }

            Episodes = new ObservableCollection<Episode>(Episodes.OrderByDescending(e => e.PublicationDate));
        }

        public async void CheckDownloads()
        {
            await Task.Run(async () =>
            {
                try
                {
                    var folder = await FileHelper.GetLocalFolder(true);
                    
                    if (await folder.TryGetItemAsync(Root) == null)
                    {
                        return;
                    }

                    var podcastFolder = await folder.GetFolderAsync(Root);
                    var files = await podcastFolder.GetFilesAsync();

                    foreach (var file in files)
                    {
                        var fileName = file.Name;
                        var episode = Episodes.FirstOrDefault(e => e.LocalFilename.ContainsIgnoreCase(fileName));

                        if (episode != null)
                        {
                            episode.IsAlreadyDownloaded = true;
                            episode.UpdateDownloadInfo();
                        }
                    }
                }
                catch
                {
                    // Ignore error
                }
            });
        }

        public async Task RefreshAsync(bool notify, bool checkDownloads, bool forceRefresh)
        {
            try
            {
                foreach (var episode in Episodes)
                {
                    if (episode.IsAlreadyDownloaded || episode.DownloadInProgress)
                    {
                        if (episode.IsPlayed && LocalSettings.Instance.DeleteDownloadWhenPlayed)
                        {
                            episode.DeleteDownload(true);
                        }

                        if (episode.DownloadedDate.HasValue && DateTime.Now.Subtract(episode.DownloadedDate.Value).TotalDays >= LocalSettings.Instance.DeleteEpisodesOlderThan)
                        {
                            episode.DeleteDownload(true);
                        }
                    }
                }

                GetLocalImage();

                if (!NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
                {
                    if (checkDownloads)
                    {
                        CheckForAutomaticDownloads();
                    }
                    return;
                }

                if (!forceRefresh && !LocalSettings.Instance.Metered && NetworkHelper.Instance.ConnectionInformation.IsInternetOnMeteredConnection)
                {
                    return;
                }

                var data = await CoreTools.DownloadStringAsync(FeedUrl, true, Login, Password);
                var document = new Windows.Data.Xml.Dom.XmlDocument();

                try
                {
                    data = data.Replace("﻿<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "");
                    document.LoadXml(data);
                }
                catch
                {
                    data = await CoreTools.DownloadStringAsync(FeedUrl, false, Login, Password);
                    document.LoadXml(data);
                }

                await ParseAsync(document, notify, checkDownloads);
            }
            catch
            {
                // Ignore error
            }
        }

        public async void AddToLibrary()
        {
            Library.Podcasts.Add(this);

            await Library.SaveAsync();

            GetLocalImage();
            for (var index = 0; index < AppSettings.Instance.DownloadLastEpisodesCount && index < Episodes.Count; index++)
            {
                Episodes[index].DownloadAsync(false);
            }
        }

        public static Podcast FromString(string data)
        {
            return JsonConvert.DeserializeObject<Podcast>(data);
        }

        async Task ParseAsync(Windows.Data.Xml.Dom.XmlDocument document, bool notify, bool checkDownloads)
        {
            try
            {
                GetLocalImage();

                var channel = document.GetElementsByTagName("channel")[0];

                var tempList = new List<Episode>();

                foreach (var currentItem in channel.ChildNodes)
                {
                    if (currentItem.NodeName != "item")
                    {
                        continue;
                    }

                    var item = currentItem as Windows.Data.Xml.Dom.XmlElement;

                    var episode = new Episode
                    {
                        Link = item.GetChildNodeTextValue("link", ""),
                        Title = item.GetChildNodeTextValue("title", "").Sanitize(),
                        Subtitle = item.GetChildNodeTextValue("itunes:subtitle", "").Sanitize(),
                        Author = item.GetChildNodeTextValue("itunes:author", "").Sanitize(),
                        Summary = item.GetChildNodeTextValue("description", "").SanitizeAsHTML(),
                        Enclosure = item.GetChildNodeAttribute("enclosure", "url", ""),
                        PictureUrl = item.GetChildNodeAttribute("itunes:image", "href", ""),
                        DeclaredDuration = item.GetChildNodeTextValue("itunes:duration", ""),
                        Keywords = item.GetChildNodeTextValue("itunes:keywords", ""),
                        PublicationDate = item.GetChildNodeTextValue("pubDate", "").TryParseAsDateTime(),
                        PodcastFeedUrl = FeedUrl
                    };

                    var length = item.GetChildNodeAttribute("enclosure", "length", "");
                    double estimatedLength;
                    if (Double.TryParse(length, out estimatedLength))
                    {
                        episode.EstimatedFileSize = estimatedLength;
                    }

                    if (!episode.DeclaredDuration.Contains(":"))
                    {
                        episode.DeclaredDuration = "";
                    }

                    var itunesSummary = item.GetChildNodeTextValue("itunes:summary", "").SanitizeAsHTML();

                    if (itunesSummary.Length > episode.Summary.Length)
                    {
                        episode.Summary = itunesSummary;
                    }

                    var contentSummary = item.GetChildNodeTextValue("content:encoded", "").SanitizeAsHTML();

                    if (contentSummary.Length > episode.Summary.Length)
                    {
                        episode.Summary = contentSummary;
                    }

                    if (string.IsNullOrEmpty(episode.Author))
                    {
                        episode.Author = Title;
                    }

                    if (string.IsNullOrEmpty(episode.Enclosure))
                    {
                        episode.Enclosure = episode.Link;
                    }

                    if (string.IsNullOrEmpty(episode.PictureUrl))
                    {
                        episode.PictureUrl = LocalImage;
                    }

                    episode.Clean();

                    tempList.Add(episode);
                }

                var addedEpisodes = new List<Episode>();
                var indexToInject = 0;

                foreach (var episode in tempList.OrderByDescending(e => e.PublicationDate))
                {
                    var inLibraryEpisode = Episodes.FirstOrDefault(e => e.Enclosure == episode.Enclosure);
                    if (inLibraryEpisode != null)
                    {
                        inLibraryEpisode.Author = episode.Author;
                        inLibraryEpisode.DeclaredDuration = episode.DeclaredDuration;
                        inLibraryEpisode.Keywords = episode.Keywords;
                        inLibraryEpisode.Link = episode.Link;
                        inLibraryEpisode.PictureUrl = episode.PictureUrl;
                        inLibraryEpisode.PodcastFeedUrl = episode.PodcastFeedUrl;
                        inLibraryEpisode.Title = episode.Title;
                        inLibraryEpisode.Subtitle = episode.Subtitle;
                        inLibraryEpisode.Summary = episode.Summary;
                        inLibraryEpisode.PublicationDate = episode.PublicationDate;
                        inLibraryEpisode.EstimatedFileSize = episode.EstimatedFileSize;
                        continue;
                    }

                    await DispatchManager.RunOnDispatcherAsync(() =>
                    {
                        Episodes.Insert(indexToInject, episode);
                    });

                    indexToInject++;
                    addedEpisodes.Add(episode);

                    if (notify && IsInLibrary && LocalSettings.Instance.Notifications)
                    {
                        Messenger.Notify(string.Format(LocalSettings.Instance.NotificationMessage, episode.Title), Title, "", LocalImage);
                    }
                }

                if (addedEpisodes.Count > 0 && IsInLibrary && AppSettings.Instance.AutomaticallyAddNewEpisodeToPlaylist)
                {
                    await DispatchManager.RunOnDispatcherAsync(() =>
                    {
                        var reversedAddedEpisodes = addedEpisodes.OrderBy(e => e.PublicationDate).ToList();
                        if (Playlist.CurrentPlaylist != null)
                        {
                            Playlist.CurrentPlaylist.AddEpisodes(reversedAddedEpisodes);
                        }
                        else
                        {
                            Playlist.EpisodesToAdd.AddRange(reversedAddedEpisodes);
                        }
                    });
                }

                LocalSettings.Instance.NewEpisodesCount += addedEpisodes.Count;

                if (checkDownloads)
                {
                    CheckForAutomaticDownloads();
                }

                if (toExecuteWhenReady != null)
                {
                    toExecuteWhenReady();
                    toExecuteWhenReady = null;
                }

            }
            catch
            {
                await Messenger.ErrorAsync(StringsHelper.Error_UnableToParseRSS + " (" + Title + ")");
            }
        }

        void CheckForAutomaticDownloads()
        {
            if (!IsInLibrary)
            {
                return;
            }

            int checkCount = AppSettings.Instance.DownloadLastEpisodesCount;

            if (checkCount == 0)
            {
                return;
            }

            foreach (var episode in Episodes.OrderByDescending(e => e.PublicationDate))
            {
                if (!episode.IsPlayed && !episode.DownloadInProgress && !episode.IsAlreadyDownloaded)
                {
                    episode.DownloadAsync(false);
                }

                checkCount--;
                if (checkCount == 0)
                {
                    return;
                }
            }
        }

        public static async Task<Podcast> ParseAsync(string url, bool checkDownloads, string login = null, string password = null)
        {
            try
            {
                var data = await CoreTools.DownloadStringAsync(url, true, login, password);
                var document = new Windows.Data.Xml.Dom.XmlDocument();

                try
                {
                    document.LoadXml(data);
                }
                catch
                {
                    data = await CoreTools.DownloadStringAsync(url, false, login, password);
                    document.LoadXml(data);
                }

                var channel = document.GetElementsByTagName("channel")[0] as Windows.Data.Xml.Dom.XmlElement;

                var result = new Podcast
                {
                    Title = channel.GetChildNodeTextValue("title", "").Sanitize(),
                    Description = channel.GetChildNodeTextValue("description", "").Sanitize(),
                    Link = channel.GetChildNodeTextValue("link", ""),
                    Image = channel.GetChildNodeAttribute("itunes:image", "href", ""),
                    FeedUrl = url,
                    Login = login,
                    Password = password
                };

                if (string.IsNullOrEmpty(result.Image))
                {
                    result.LocalImage = "ms-appx:///Assets/IconFull.png";
                }

                if (string.IsNullOrEmpty(result.Description))
                {
                    result.Description = channel.GetChildNodeTextValue("itunes:summary", "").Sanitize();
                }

                await result.ParseAsync(document, false, checkDownloads);

                result.ReOrder();

                return result;
            }
            catch
            {
                return null;
            }
        }

        public void MarkAsPlayed()
        {
            foreach (var episode in Episodes)
            {
                episode.IsPlayed = true;
            }
        }
    }
}
