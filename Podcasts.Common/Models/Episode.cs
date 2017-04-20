using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Newtonsoft.Json;
using System.Linq;
using Windows.Storage;

namespace Podcasts
{
    [DataContract]
    public class Episode : Notifier
    {
        CancellationTokenSource cancellationTokenSource;

        [DataMember]
        public double EstimatedFileSize { get; set; }

        [DataMember]
        public string Link { get; set; }
        [DataMember]
        public string Title
        {
            get { return title; }
            set
            {
                if (Title == value)
                {
                    return;
                }

                title = value;

                RaisePropertyChanged(nameof(Title));
            }
        }
        [DataMember]
        public string Subtitle
        {
            get { return subtitle; }
            set
            {
                if (subtitle == value)
                {
                    return;
                }
                subtitle = value;
                RaisePropertyChanged(nameof(Subtitle));
            }
        }
        [DataMember]
        public string Author
        {
            get { return author; }
            set
            {
                if (author == value)
                {
                    return;
                }
                author = value;
                RaisePropertyChanged(nameof(Author));
            }
        }

        public string Summary
        {
            get
            {
                return summary;
            }
            set
            {
                if (summary == value)
                {
                    return;
                }
                summary = value;
                RaisePropertyChanged(nameof(Summary));
            }
        }

        [DataMember]
        public string PictureUrl
        {
            get
            {
                if (string.IsNullOrEmpty(pictureUrl) && Podcast != null)
                {
                    return Podcast.Image;
                }
                return pictureUrl;
            }
            set
            {
                if (pictureUrl == value)
                {
                    return;
                }

                pictureUrl = value;
                RaisePropertyChanged(nameof(PictureUrl));
            }
        }

        public string DeclaredDuration { get; set; }

        public string Keywords { get; set; }

        [DataMember]
        public DateTime PublicationDate
        {
            get { return publicationDate; }
            set
            {
                if (publicationDate == value)
                {
                    return;
                }
                publicationDate = value;
                RaisePropertyChanged(nameof(PublicationDate));
                RaisePropertyChanged(nameof(PublishedText));
            }
        }

        [DataMember]
        public bool WasDeleted { get; set; }

        [DataMember]
        public string Enclosure { get; set; }

        [DataMember]
        public bool IsPlayed
        {
            get { return isPlayed; }
            set
            {
                if (value == isPlayed)
                {
                    return;
                }
                isPlayed = value;
                RaisePropertyChanged(nameof(IsPlayed));

                Podcast?.RaisePlayedCountChanged();

                if (IsPlayed)
                {
                    if (LocalSettings.Instance.DeleteDownloadWhenPlayed && (Playlist.CurrentPlaylist == null || Playlist.CurrentPlaylist.CurrentEpisode != this))
                    {
                        if (DownloadInProgress)
                        {
                            CancelDownload();
                        }
                        else if (IsAlreadyDownloaded)
                        {
                            DeleteDownload(true);
                        }
                    }

                    if (Playlist.CurrentPlaylist != null && LocalSettings.Instance.RemovedPlayedEpisodeFromPlayList && Playlist.CurrentPlaylist.CurrentEpisode != this)
                    { 
                        Playlist.CurrentPlaylist.RemoveEpisode(this);
                    }

                    Library.RaiseOnPlayedEpisode(this);

                    if (Playlist.CurrentPlaylist != null)
                    {
                        CoreTools.UpdateTile(Playlist.CurrentPlaylist.CurrentEpisode);
                    }
                }
            }
        }

        string uniqueID;
        [DataMember]
        public string UniqueID
        {
            get
            {
                if (string.IsNullOrEmpty(uniqueID))
                {
                    uniqueID = CoreTools.SanitizeFilename(Title);
                }

                return uniqueID;
            }
            set
            {
                uniqueID = value;
            }
        }

        [DataMember]
        public string PodcastFeedUrl { get; set; }

        Podcast podcast;
        public Podcast Podcast
        {
            get
            {
                if (string.IsNullOrEmpty(PodcastFeedUrl))
                {
                    return null;
                }
                return podcast ?? (podcast = Library.GetPodcastByFeedUrl(PodcastFeedUrl));
            }
        }

        public string DownloadInfo { get; set; }
        public double DownloadSize { get; set; }

        public void FallbackToPodcastPicture()
        {
            if (Podcast == null)
            {
                return;
            }

            PictureUrl = Podcast.LocalImage;
        }

        public async void UpdateDownloadInfo()
        {
            if (!IsAlreadyDownloaded)
            {
                DownloadInfo = "";
                DownloadSize = 0;
            }
            else
            {
                try
                {
                    var targetFile = await FileHelper.GetLocalFileAsync(LocalFilename, true);
                    var fileBasicProperties = await targetFile.GetBasicPropertiesAsync();

                    DownloadedDate = fileBasicProperties.DateModified.DateTime;

                    DownloadSize = fileBasicProperties.Size / 1048576.0;
                    DownloadInfo = $"{DownloadSize:F} MB";
                }
                catch
                {
                    DownloadSize = 0;
                    DownloadInfo = "";
                }
            }

            RaisePropertyChanged(nameof(DownloadInfo));
        }


        public string PublishedText => $"{PublicationDate.ToString("d")}";

        public bool InLibrary => Podcast != null;

        public bool CanBeDownloaded => InLibrary && !IsAlreadyDownloaded && !DownloadInProgress;

        public string LocalFilename
        {
            get
            {
                try
                {
                    if (!InLibrary || Podcast == null || string.IsNullOrEmpty(Enclosure))
                    {
                        return "";
                    }

                    var questionMarkPosition = Enclosure.IndexOf("?", StringComparison.Ordinal);

                    if (questionMarkPosition == -1)
                    {
                        return Path.Combine(Podcast.Root, UniqueID + Path.GetExtension(Enclosure));
                    }

                    return Path.Combine(Podcast.Root,
                        UniqueID + Path.GetExtension(Enclosure.Substring(0, questionMarkPosition)));
                }
                catch
                {
                    return "";
                }
            }
        }

        public DateTime? DownloadedDate { get; set; }

        bool isAlreadyDownloaded;
        public bool IsAlreadyDownloaded
        {
            get
            {
                return isAlreadyDownloaded;
            }
            set
            {
                if (isAlreadyDownloaded == value)
                {
                    return;
                }

                isAlreadyDownloaded = value;
                RaisePropertyChanged(nameof(IsAlreadyDownloaded));
                RaisePropertyChanged(nameof(CanBeDownloaded));

                if (isAlreadyDownloaded)
                {
                    Library.AddDownloadedEpisode(this);
                }
                else if (!DownloadInProgress)
                {
                    Library.RemoveDownloadedEpisode(this);
                }

                Library.RaiseOnEpisodeDownloadedStateChanged(this);
            }
        }

        int downloadProgress;
        public int DownloadProgress
        {
            get
            {
                return downloadProgress;
            }
            set
            {
                downloadProgress = value;
                RaisePropertyChanged(nameof(DownloadProgress));
            }
        }

        bool downloadInProgress;
        public bool DownloadInProgress
        {
            get
            {
                return downloadInProgress;
            }
            set
            {
                if (downloadInProgress == value)
                {
                    return;
                }

                downloadInProgress = value;
                RaisePropertyChanged(nameof(DownloadInProgress));
                RaisePropertyChanged(nameof(CanBeDownloaded));

                if (downloadInProgress)
                {
                    Library.AddDownloadedEpisode(this);
                }
                else if (!IsAlreadyDownloaded)
                {
                    Library.RemoveDownloadedEpisode(this);
                }
            }
        }

        void UpdateDownloadProgress(DownloadOperation download)
        {
            if (download.Progress.TotalBytesToReceive == 0)
            {
                DownloadProgress = 0;
                return;
            }
            DownloadProgress = (int)((download.Progress.BytesReceived * 100) / download.Progress.TotalBytesToReceive);
        }

        public async Task CheckDownloadedStateAsync()
        {
            if (!InLibrary || IsAlreadyDownloaded)
            {
                return;
            }

            if (await FileHelper.IsLocalFileExistsAsync(LocalFilename + ".tmp", false))
            {
                var file = await FileHelper.GetLocalFileAsync(LocalFilename + ".tmp", false);
                var attribs = await file.GetBasicPropertiesAsync();
                if (attribs.Size > 0)
                {
                    var rootFolder = await FileHelper.GetLocalFolder(true);

                    var path = Path.GetDirectoryName(LocalFilename);
                    var targetFolder = await rootFolder.CreateFolderAsync(path, CreationCollisionOption.OpenIfExists);
                    var newName = FileHelper.GetNotTooLongPath(Path.GetFileName(LocalFilename), targetFolder);

                    await file.CopyAsync(targetFolder, newName, NameCollisionOption.ReplaceExisting);
                }
                await file.DeleteAsync();
            }

            IsAlreadyDownloaded = await FileHelper.IsLocalFileExistsAsync(LocalFilename, true);

            if (IsAlreadyDownloaded)
            {
                UpdateDownloadInfo();
            }
        }

        public bool DownloadAttached { get; set; }

        public async void DownloadAsync(bool force = true)
        {
            if (DownloadAttached || string.IsNullOrEmpty(Enclosure) || IsAlreadyDownloaded || DownloadInProgress)
            {
                return;
            }

            if (!force && WasDeleted)
            {
                return;
            }

            try
            {
                var downloader = new BackgroundDownloader { CostPolicy = LocalSettings.Instance.Metered ? BackgroundTransferCostPolicy.Always : BackgroundTransferCostPolicy.UnrestrictedOnly };
                DownloadInProgress = true;
                DownloadAttached = true;

                var targetFile = await FileHelper.CreateLocalFileAsync(LocalFilename + ".tmp", false);
                var download = downloader.CreateDownload(new Uri(Enclosure, UriKind.Absolute), targetFile);

                download.Priority = BackgroundTransferPriority.Default;

                cancellationTokenSource = new CancellationTokenSource();
                try
                {
                    await download.StartAsync().AsTask(cancellationTokenSource.Token, new Progress<DownloadOperation>(UpdateDownloadProgress));
                    await CheckDownloadedStateAsync();
                }
                catch
                {
                    IsAlreadyDownloaded = false;
                    DownloadProgress = 0;
                    await FileHelper.TryDeleteFileAsync(targetFile);
                }
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;

            }
            catch
            {
                IsAlreadyDownloaded = false;
                DownloadProgress = 0;
            }

            DownloadInProgress = false;
            DownloadAttached = false;
        }

        public async Task DeleteDownloadAsync(bool silent = false)
        {
            if (DownloadInProgress)
            {
                cancellationTokenSource?.Cancel();
            }

            try
            {
                if (await FileHelper.IsLocalFileExistsAsync(LocalFilename, true))
                {
                    var targetFile = await FileHelper.GetLocalFileAsync(LocalFilename, true);
                    await FileHelper.TryDeleteFileAsync(targetFile);
                }

                if (await FileHelper.IsLocalFileExistsAsync(LocalFilename + ".tmp", false))
                {
                    var targetFile = await FileHelper.GetLocalFileAsync(LocalFilename + ".tmp", false);
                    await FileHelper.TryDeleteFileAsync(targetFile);
                }

                WasDeleted = true;
            }
            catch
            {
                if (!silent)
                {
                    await Messenger.ErrorAsync(StringsHelper.Error_UnableToDeleteDownload);
                }
            }
            IsAlreadyDownloaded = false;
            DownloadAttached = false;
            DownloadInProgress = false;
            DownloadedDate = null;
        }

        public void Clean()
        {
            if (Enclosure.StartsWith("//"))
            {
                Enclosure += "http:";
            }
        }

        public async void DeleteDownload(bool silent = false)
        {
            await DeleteDownloadAsync(silent);
        }

        public void CancelDownload(bool silent = false)
        {
            if (!DownloadInProgress)
            {
                return;
            }
            cancellationTokenSource?.Cancel();
            DeleteDownload(silent);
        }

        internal async void AttachAsync(DownloadOperation download)
        {
            if (DownloadAttached || string.IsNullOrEmpty(Enclosure) || IsAlreadyDownloaded)
            {
                return;
            }

            try
            {
                DownloadInProgress = true;
                DownloadAttached = true;
                cancellationTokenSource = new CancellationTokenSource();
                try
                {
                    await download.AttachAsync().AsTask(cancellationTokenSource.Token, new Progress<DownloadOperation>(UpdateDownloadProgress));
                    await CheckDownloadedStateAsync();
                }
                catch
                {
                    IsAlreadyDownloaded = false;
                    DownloadProgress = 0;
                    await FileHelper.TryDeleteFileAsync(download.ResultFile);
                }

                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }
            catch
            {
                // Ignore error
            }

            DownloadInProgress = false;
            DownloadAttached = false;
        }

        double position;
        [DataMember]
        public double Position
        {
            get
            {
                return position;
            }
            set
            {
                if (position == value)
                {
                    return;
                }

                position = value;

                RaisePropertyChanged(nameof(Position));
            }
        }

        double duration;
        [DataMember]
        public double Duration
        {
            get
            {
                return duration;
            }
            set
            {
                if (duration == value)
                {
                    return;
                }

                duration = value;

                RaisePropertyChanged(nameof(Duration));
            }
        }

        string title;
        string subtitle;
        string pictureUrl;
        DateTime publicationDate;
        string summary;
        string author;
        bool isPlayed;

        // Serialization
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string ToCompleteString()
        {
            var settings = new JsonSerializerSettings { ContractResolver = new IgnoreDataMemberContractResolver() };
            return JsonConvert.SerializeObject(this, settings);
        }

        public static Episode FromString(string data)
        {
            return JsonConvert.DeserializeObject<Episode>(data);
        }

        public static Episode FromCompleteString(string data)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new IgnoreDataMemberContractResolver() };
            return JsonConvert.DeserializeObject<Episode>(data, settings);
        }
    }
}
