using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Podcasts
{
    public static class FileHelper
    {
        public static string GetNotTooLongPath(string filename, StorageFolder folder)
        {
            var path = Path.Combine(folder.Path, filename);
            var extension = Path.GetExtension(path);

            path = path.Substring(0, path.Length - extension.Length);

            if (path.Length > 250)
            {
                path = path.Substring(0, 250);
                path += extension;

                filename = path.Replace(folder.Path + "\\", "");
            }

            return filename;
        }

        public static async Task<StorageFolder> GetLocalFolder(bool useExternalFileAllowed)
        {
            if (!useExternalFileAllowed)
            {
                return ApplicationData.Current.LocalFolder;
            }

            if (String.IsNullOrEmpty(LocalSettings.Instance.ExternalFolderPath))
            {
                return ApplicationData.Current.LocalCacheFolder;
            }

            try
            {
                return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("ExternalStorage");
            }
            catch
            {
                LocalSettings.Instance.ExternalFolderPath = "";
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem("ExternalStorage"))
                {
                    StorageApplicationPermissions.FutureAccessList.Remove("ExternalStorage");
                }
                return ApplicationData.Current.LocalCacheFolder;
            }
        }

        public static async Task<StorageFile> CreateLocalFileAsync(string fileName, bool useExternalFileAllowed)
        {
            StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

            fileName = GetNotTooLongPath(fileName, localFolder);

            return await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }

        public static void RenameLocalFolder(string oldFolderName, string newFolderName, bool useExternalFileAllowed)
        {
            Task.Run(async () =>
            {
                try
                {
                    var localFolder = await GetLocalFolder(useExternalFileAllowed);
                    var oldFolder = await localFolder.TryGetItemAsync(oldFolderName);
                    if (oldFolder != null)
                    {
                        await oldFolder.RenameAsync(newFolderName);
                    }
                }
                catch
                {
                    // Ignore error
                }
            }).Wait();
        }

        public static void CreateLocalFolder(string folderName, bool useExternalFileAllowed)
        {
            Task.Run(async () =>
            {
                var localFolder = await GetLocalFolder(useExternalFileAllowed);
                if (await localFolder.TryGetItemAsync(folderName) == null)
                {
                    await localFolder.CreateFolderAsync(folderName);
                }
            }).Wait();
        }


        public static async Task<bool> IsPackagedFileExistsAsync(string folderName, string fileName)
        {
            StorageFolder installFolder = Package.Current.InstalledLocation;

            if (folderName != null)
            {
                installFolder = await installFolder.GetFolderAsync(folderName);
            }

            var item = await installFolder.TryGetItemAsync(fileName);

            return item != null;
        }

        public static async Task<StorageFile> GetPackagedFileAsync(string folderName, string fileName)
        {
            StorageFolder installFolder = Package.Current.InstalledLocation;

            if (folderName != null)
            {
                StorageFolder subFolder = await installFolder.GetFolderAsync(folderName);
                return await subFolder.GetFileAsync(fileName);
            }

            return await installFolder.GetFileAsync(fileName);
        }

        public static async Task<StorageFile> DeleteAndRecreateLocalFileAsync(string fileName, bool useExternalFileAllowed)
        {
            StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

            fileName = GetNotTooLongPath(fileName, localFolder);

            var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
            var basicProperties = await file.GetBasicPropertiesAsync();

            if (basicProperties.Size == 0)
            {
                return file;
            }

            while (true)
            {
                try
                {
                    await file.DeleteAsync();
                    break;
                }
                catch
                {
                    await Task.Delay(250);
                }
            }

            return await CreateLocalFileAsync(fileName, useExternalFileAllowed);
        }

        public static async Task<StorageFile> GetOrCreateLocalFileAsync(string fileName, bool useExternalFileAllowed)
        {
            StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

            fileName = GetNotTooLongPath(fileName, localFolder);

            var item = await localFolder.TryGetItemAsync(fileName);

            if (item == null)
            {
                return await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }

            return await localFolder.GetFileAsync(fileName);
        }

        public static async Task<StorageFile> GetLocalFileAsync(string fileName, bool useExternalFileAllowed)
        {
            StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

            fileName = GetNotTooLongPath(fileName, localFolder);

            return await localFolder.GetFileAsync(fileName);
        }

        public static async Task<bool> IsLocalFileExistsAsync(string fileName, bool useExternalFileAllowed)
        {
            try
            {
                StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

                fileName = GetNotTooLongPath(fileName, localFolder);

                var item = await localFolder.TryGetItemAsync(fileName);

                if (item == null)
                {
                    return false;
                }

                return true;

            }
            catch
            {
                return false;
            }
        }

        public static async Task DeleteFolderAsync(string folderName, bool useExternalFileAllowed)
        {
            try
            {
                StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

                var folderToDelete = await localFolder.GetFolderAsync(folderName);

                await folderToDelete.DeleteAsync();
            }
            catch
            {
                // ignored
            }
        }

        public static async Task TryDeleteFileAsync(IStorageFile file)
        {
            try
            {
                await file.DeleteAsync();
            }
            catch
            {
                // ignored
            }
        }

        public static async Task DeleteLocalFileAsync(string fileName, bool useExternalFileAllowed)
        {
            try
            {
                if (!await IsLocalFileExistsAsync(fileName, useExternalFileAllowed))
                {
                    return;
                }
                StorageFolder localFolder = await GetLocalFolder(useExternalFileAllowed);

                fileName = GetNotTooLongPath(fileName, localFolder);

                var file = await localFolder.GetFileAsync(fileName);

                await file.DeleteAsync();
            }
            catch
            {
                // ignored
            }
        }

        public static async Task<IStorageItem> CreatePathAsync(this StorageFolder folder, string fileLocation, CreationCollisionOption fileCollisionOption, CreationCollisionOption folderCollisionOption)
        {
            if (String.IsNullOrEmpty(fileLocation))
            {
                return null;
            }

            var separatorIndex = fileLocation.IndexOfAny(new[] { '/', '\\' });
            if (separatorIndex == -1)
            {
                return await folder.CreateFileAsync(fileLocation, fileCollisionOption);
            }
            else
            {
                var folderName = fileLocation.Substring(0, separatorIndex);
                var subFolder = await folder.CreateFolderAsync(folderName, folderCollisionOption);
                return await subFolder.CreatePathAsync(fileLocation.Substring(separatorIndex + 1), fileCollisionOption, folderCollisionOption);
            }
        }
    }
}
