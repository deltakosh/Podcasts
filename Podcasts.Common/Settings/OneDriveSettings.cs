using Microsoft.OneDrive.Sdk;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Podcasts
{
    public class OneDriveSettings : Notifier
    {
        static OneDriveSettings instance;
        bool notInitialized = true;
        bool checkInProgress = false;
        bool initializationInProgress = false;
        OneDriveClient oneDriveClient;

        public static OneDriveSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new OneDriveSettings();
                }

                return instance;
            }
        }

        public bool CheckInProgress
        {
            get
            {
                return checkInProgress;
            }
            set
            {
                checkInProgress = value;

                RaisePropertyChanged(nameof(CheckInProgress));
            }
        }

        public async Task Initialize()
        {
            try
            {
                if (initializationInProgress)
                {
                    return;
                }

                initializationInProgress = true;
                var msaAuthenticationProvider = new OnlineIdAuthenticationProvider(new[] { "onedrive.readwrite", "wl.signin" });
                await msaAuthenticationProvider.AuthenticateUserAsync();
                oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthenticationProvider);

                notInitialized = false;

                await MoveFromOldFolder();
            }
            catch
            {
                notInitialized = true;
            }
        }

        private async Task<bool> MoveItemToNewFolder(string filename)
        {
            try
            {
                var updateItem = new Item { ParentReference = new ItemReference { Path = "/Drive/Special/AppRoot:" } };
                await oneDriveClient
                    .Drive
                    .Root
                    .ItemWithPath("/Podcasts/" + filename)
                    .Request()
                    .UpdateAsync(updateItem);
            }
            catch
            {
                AppSettings.Instance.OneDriveFolderMoved = true;
                return false;
            }

            return true;
        }

        private async Task MoveFromOldFolder()
        {
            if (AppSettings.Instance.OneDriveFolderMoved)
            {
                return;
            }

            if (!await MoveItemToNewFolder("library.dat"))
            {
                return;
            }
            if (!await MoveItemToNewFolder("playlist.dat"))
            {
                return;
            }

            try
            {
                await oneDriveClient.Drive.Root.ItemWithPath("Podcasts").Request().DeleteAsync();
            }
            catch
            {
                // Ignore error
            }
        }

        public async Task<bool> IsFileExists(string filename)
        {
            try
            {
                var item = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(filename).Request().GetAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetContentFromFileAsync(string filename, DateTime fileDate)
        {
            if (notInitialized)
            {
                return null;
            }

            try
            {
                var item = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(filename).Request().GetAsync();

                if (!LocalSettings.Instance.ForceCloudSync && fileDate.AddSeconds(30) >= item.LastModifiedDateTime)
                {
                    return "";
                }

                using (var contentStream = await oneDriveClient.Drive.Items[item.Id].Content.Request().GetAsync())
                {
                    using (var reader = new StreamReader(contentStream))
                    {
                        var content = reader.ReadToEnd();

                        return content.DecompressJson();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveContentAsync(string filename, string data)
        {
            if (notInitialized)
            {
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                using (var contentStream = new MemoryStream(bytes))
                {
                    await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(filename).Content.Request().PutAsync<Item>(contentStream);
                }
            }
            catch
            {
                // Ignore error
            }
        }
    }
}
