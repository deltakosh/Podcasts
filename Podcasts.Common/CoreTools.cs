using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.System.Profile;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.Web.Http;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http.Filters;
using Windows.UI.Core;
using System.Linq;
using Windows.System.Display;
using System.Diagnostics;

namespace Podcasts
{
    public static class CoreTools
    {
        public static CoreDispatcher GlobalDispatcher { get; set; }
        static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        static DisplayRequest DisplayRequest;

        public static void ActivateDisplay()
        {
            if (DisplayRequest == null)
                DisplayRequest = new DisplayRequest();

            DisplayRequest.RequestActive();
        }

        public static void ReleaseDisplay()
        {
            if (DisplayRequest == null)
                return;

            DisplayRequest.RequestRelease();
        }

        public static string SanitizeFilename(string filename)
        {
            return Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        public static void HandleItemsWidth(ListViewBase gridView, double maxSize = 500.0)
        {
            if (gridView.ItemsPanelRoot == null)
            {
                return;
            }

            var panel = gridView.ItemsPanelRoot as ItemsWrapGrid;
            var totalWidth = gridView.ActualWidth - 20;
            panel.ItemWidth = totalWidth > maxSize ? maxSize : totalWidth;
        }

        public static void SetBadgeNumber(int value)
        {
            try
            {
                var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                var badgeAttributes = badgeXml.SelectSingleNode("/badge") as XmlElement;
                if (badgeAttributes != null)
                {
                    badgeAttributes.SetAttribute("value", value.ToString());

                    var badgeNotification = new BadgeNotification(badgeXml);

                    BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badgeNotification);
                }
            }
            catch
            {
                // Ignore error
            }
        }

        static async Task PrepareTileAsync(XmlDocument tileXml, Episode currentOne)
        {
            var tileTextAttributes = tileXml.GetElementsByTagName("text");

            // Tile
            if (currentOne != null)
            {
                tileTextAttributes[0].AppendChild(tileXml.CreateTextNode(currentOne.Podcast.Title));
                tileTextAttributes[1].AppendChild(tileXml.CreateTextNode(currentOne.Title));
                if (tileTextAttributes.Count > 2)
                {
                    tileTextAttributes[2].AppendChild(tileXml.CreateTextNode(currentOne.Author));
                }
            }
            else
            {
                tileTextAttributes[0].AppendChild(tileXml.CreateTextNode(""));
                tileTextAttributes[1].AppendChild(tileXml.CreateTextNode(""));
                if (tileTextAttributes.Count > 2)
                {
                    tileTextAttributes[2].AppendChild(tileXml.CreateTextNode(""));
                }
            }

            if (tileTextAttributes.Count > 3)
            {
                tileTextAttributes[3].AppendChild(tileXml.CreateTextNode(string.Format(StringsHelper.Unplayed, Library.UnplayedCount)));
            }

            var tileImageAttributes = tileXml.GetElementsByTagName("image");
            if (currentOne != null)
            {
                var imageFile = "livetile.jpg";

                if (currentOne.PictureUrl.StartsWith("ms-appdata"))
                {
                    ((XmlElement)tileImageAttributes[0]).SetAttribute("src", currentOne.PictureUrl);
                }
                else
                {
                    await DownloadDirectToFile(currentOne.PictureUrl, imageFile, false);
                    ((XmlElement)tileImageAttributes[0]).SetAttribute("src", "ms-appdata:///local/" + imageFile);
                }
            }
            else
            {
                ((XmlElement)tileImageAttributes[0]).SetAttribute("src", "ms-appx:///Assets/Icon.png");
            }
        }

        public static void UpdateTile(Episode currentOne)
        {
            if (currentOne == null)
            {
                return;
            }
            if (currentOne.Podcast == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                if (Semaphore.CurrentCount == 0)
                {
                    return;
                }
                Semaphore.Wait();

                try
                {
                    var tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare310x310SmallImageAndText01);
                    var wideXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150SmallImageAndText02);
                    var squareTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150PeekImageAndText03);

                    await PrepareTileAsync(tileXml, currentOne);
                    await PrepareTileAsync(wideXml, currentOne);
                    await PrepareTileAsync(squareTileXml, currentOne);

                    var visual = (XmlElement)tileXml.GetElementsByTagName("visual").Item(0);
                    var node = tileXml.ImportNode(wideXml.GetElementsByTagName("binding").Item(0), true);
                    visual.SetAttribute("branding", "name");
                    visual.AppendChild(node);

                    node = tileXml.ImportNode(squareTileXml.GetElementsByTagName("binding").Item(0), true);
                    visual.AppendChild(node);

                    // Update
                    var tileNotification = new TileNotification(tileXml);

                    var tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();

                    tileUpdater.Update(tileNotification);
                }
                catch
                {
                    // ignored
                }

                Semaphore.Release();
            });
        }

        public static async Task<bool> DownloadDirectToFile(string url, string localTarget, bool useExternalFileAllowed)
        {
            try
            {
                url = url.Replace("\\", "//");
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.94 Safari/537.36");
                    using (var response = await httpClient.GetAsync(new Uri(url)))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return false;
                        }

                        using (var content = response.Content)
                        {
                            var buffer = await content.ReadAsBufferAsync();

                            StorageFile outputFile = await FileHelper.CreateLocalFileAsync(localTarget, useExternalFileAllowed);
                            var props = await outputFile.GetBasicPropertiesAsync();
                            Debug.WriteLine("Downloading directly to " + outputFile.DisplayName);
                            await FileIO.WriteBytesAsync(outputFile, buffer.ToArray());

                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static string DecompressJson(this string data)
        {
            if (data.StartsWith("{") || data.StartsWith("["))
            {
                return data;
            }
            using (var input = new MemoryStream(Convert.FromBase64String(data)))
            {
                using (var output = new MemoryStream())
                {
                    using (var gs = new GZipStream(input, CompressionMode.Decompress, true))
                    {
                        gs.CopyTo(output);
                    }
                    return Encoding.UTF8.GetString(output.ToArray(), 0, (int)output.Length);
                }
            }
        }

        public static string Compress(this string data)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                using (var output = new MemoryStream())
                {
                    using (var gs = new GZipStream(output, CompressionLevel.Optimal, true))
                    {
                        gs.Write(bytes, 0, bytes.Length);
                    }

                    output.Position = 0;

                    var result = Convert.ToBase64String(output.ToArray());

                    return result;
                }
            }
            catch
            {
                return data;
            }
        }

        public static DateTime TryParseAsDateTime(this string data)
        {
            if (data.Contains(","))
            {
                var index = data.IndexOf(',');
                data = data.Substring(index + 1);
            }

            data = data.ReplaceIgnoreCase("pst", "");
            data = data.ReplaceIgnoreCase("pdt", "");
            data = data.ReplaceIgnoreCase("est", "");
            data = data.ReplaceIgnoreCase("edt", "");

            DateTime result;
            if (DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out result))
            {
                return result;
            }

            if (DateTime.TryParse(data, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out result))
            {
                return result;
            }

            if (DateTime.TryParse(data, new CultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out result))
            {
                return result;
            }

            if (DateTime.TryParse(data, new CultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out result))
            {
                return result;
            }

            return DateTime.Now;
        }

        public static string Sanitize(this string value)
        {
            value = System.Net.WebUtility.HtmlDecode(value);
            return value.ReplaceIgnoreCase("<P>", "").ReplaceIgnoreCase("</P>", "\r\n").ReplaceIgnoreCase("</BR>", "").ReplaceIgnoreCase("<BR />", "").Trim();
        }

        public static string SanitizeAsHTML(this string value)
        {
            value = System.Net.WebUtility.HtmlDecode(value);

            if (value.ContainsIgnoreCase("<P>") || value.ContainsIgnoreCase("<br>") || value.ContainsIgnoreCase("<br />"))
            {
                return value;
            }
            return value.ReplaceIgnoreCase("\n", "<BR>");
        }

        public static bool IsRunningOnMobile => AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile";
        public static bool IsRunningOnXbox => AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox";

        public static void WriteAdvancedString(this DataWriter writer, string value)
        {
            if (value == null)
            {
                writer.WriteUInt32(0);
                return;
            }
            var stringToWrite = value.Replace("&nbsp;", " ");
            writer.WriteUInt32(writer.MeasureString(stringToWrite));
            if (value.Length == 0)
            {
                return;
            }
            writer.WriteString(stringToWrite);
        }

        public static string ReadString(this DataReader reader)
        {
            var length = reader.ReadUInt32();

            if (length == 0)
            {
                return "";
            }

            return reader.ReadString(length);
        }

        public static string ReplaceIgnoreCase(this string source, string search, string newValue)
        {
            var returnValue = source.Replace(search.ToLower(), newValue.ToLower());
            returnValue = returnValue.Replace(search.ToUpper(), newValue.ToUpper());

            return returnValue;
        }

        public static string StringVersion(this PackageVersion version)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public static bool ContainsIgnoreCase(this string source, string test)
        {
            if (source == null)
            {
                return false;
            }
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, test, CompareOptions.IgnoreCase) >= 0;
        }

        public static async Task<IRandomAccessStream> GetHTTPStreamAsync(string url)
        {
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(new Uri(url)))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var outputStream = new InMemoryRandomAccessStream();

                    using (var content = response.Content)
                    {
                        var responseBuffer = await content.ReadAsBufferAsync();
                        await outputStream.WriteAsync(responseBuffer);

                        outputStream.Seek(0);

                        return outputStream;
                    }
                }
            }
        }

        public static async Task<string> DownloadStringAsync(string url, bool withForcedEncoding = true, string login = null, string password = null)
        {
            HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
            filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            if (!string.IsNullOrEmpty(login))
            {
                filter.ServerCredential = new Windows.Security.Credentials.PasswordCredential(url, login, password);
            }

            using (var httpClient = new HttpClient(filter))
            {
                httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("");
                httpClient.DefaultRequestHeaders.Add("If-Modified-Since", "Sat, 01 Jan 1901 00:00:01 GMT");
                httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.94 Safari/537.36");
                using (var response = await httpClient.GetAsync(new Uri(url)))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Feed stream download for '{url}' returned http code {response.StatusCode}");

                    using (var content = response.Content)
                    {
                        var stringContent = await content.ReadAsStringAsync();
                        if (withForcedEncoding && (!stringContent.Contains("encoding=") || stringContent.ContainsIgnoreCase("encoding=\"utf-8\"")))
                        {
                            var buffer = await content.ReadAsBufferAsync();
                            var rawBytes = new byte[buffer.Length];
                            using (var reader = DataReader.FromBuffer(buffer))
                            {
                                reader.ReadBytes(rawBytes);
                            }
                            return Encoding.UTF8.GetString(rawBytes, 0, rawBytes.Length);
                        }
                        return await content.ReadAsStringAsync();
                    }
                }
            }
        }

        public static bool GetSerializedBoolValue(string name, bool localSettings, bool defaultValue = false)
        {
            try
            {
                var value = GetSerializedStringValue(name, localSettings);
                if (string.IsNullOrEmpty(value))
                    return defaultValue;

                bool result;
                if (bool.TryParse(value, out result))
                    return result;
            }
            catch
            {
                // Ignore error
            }

            return defaultValue;
        }

        public static int GetSerializedIntValue(string name, bool localSettings, int defaultValue = 0)
        {
            var value = GetSerializedStringValue(name, localSettings);
            if (value == null)
                return defaultValue;

            int result;
            if (int.TryParse(value, out result))
                return result;

            return defaultValue;
        }

        public static uint GetSerializedUIntValue(string name, bool localSettings, uint defaultValue = 0)
        {
            var value = GetSerializedStringValue(name, localSettings);
            if (value == null)
                return defaultValue;

            uint result;
            if (uint.TryParse(value, out result))
                return result;

            return defaultValue;
        }

        public static double GetSerializedDoubleValue(string name, bool localSettings, double defaultValue = 0)
        {
            var value = GetSerializedStringValue(name, localSettings);
            if (value == null)
                return defaultValue;

            double result;
            if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                return result;

            return defaultValue;
        }

        public static Color GetSerializedColorValue(string name, bool localSettings, Color defaultValue)
        {
            var value = GetSerializedStringValue(name, localSettings);
            if (value == null)
                return defaultValue;

            var splits = value.Split('|');
            var a = byte.Parse(splits[0]);
            var r = byte.Parse(splits[1]);
            var g = byte.Parse(splits[2]);
            var b = byte.Parse(splits[3]);

            return Color.FromArgb(a, r, g, b);
        }

        public static string GetSerializedStringValue(string name, bool localSettings, string defaultValue = null)
        {
            if (localSettings)
            {
                if (LocalSettings.Instance.ContainsKey(name))
                {
                    return LocalSettings.Instance[name];
                }
            }
            else
            {
                if (AppSettings.Instance.ContainsKey(name))
                {
                    return AppSettings.Instance[name];
                }
            }

            return defaultValue;
        }

        public static void SetSerializedValue(string name, bool localSettings, object value)
        {
            string valueToStore;

            if (value is double)
                valueToStore = ((double)value).ToString(CultureInfo.InvariantCulture);
            else
                valueToStore = value.ToString();

            if (localSettings)
            {
                LocalSettings.Instance[name] = valueToStore;
            }
            else
            {
                AppSettings.Instance[name] = valueToStore;
            }
        }

        public static async Task<StorageFile> GetWhatsNewFileAsync()
        {
            var language = GlobalizationPreferences.Languages[0];
            var key = language.Substring(0, 2);

            var fileName = $"whatsnew-{key}.txt";

            if (!await FileHelper.IsPackagedFileExistsAsync("Data", fileName))
            {
                fileName = "whatsnew-en.txt";
            }

            return await FileHelper.GetPackagedFileAsync("Data", fileName);
        }

        public static async Task WriteLog(string message)
        {
            var file = await FileHelper.GetOrCreateLocalFileAsync("log.txt", false);
            await FileIO.AppendTextAsync(file, message);
        }

        public static void ShowDebugToast(string msg, string subMsg)
        {
#if DEBUG
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            var toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(msg));
            toastTextElements[1].AppendChild(toastXml.CreateTextNode(subMsg));

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
#endif
        }
    }
}
