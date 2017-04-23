using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Podcasts
{
    public sealed partial class OptionsPage
    {
        public OptionsPage()
        {
            InitializeComponent();
        }

        internal override void ClearReferences()
        {
            if (SleepTimer.IsStarted)
            {
                SleepTimer.OnTick -= CheckTimer;
            }
        }

        private void Country_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Country.SelectedItem == null)
            {
                return;
            }
            AppSettings.Instance.Market = Country.SelectedItem.ToString();
        }

        void OptionsPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var markets = new List<string> { "ES", "GB", "FR", "IT", "US", "DE", "PT", "RU" };

            if (!markets.Contains(AppSettings.Instance.Market))
            {
                markets.Add(AppSettings.Instance.Market);
            }

            markets = markets.OrderBy(m => m).ToList();

            Country.ItemsSource = markets;

            Country.SelectedItem = AppSettings.Instance.Market;

            LastEpisodes.ItemsSource = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20, 30, 40, 50 };
            LastEpisodes.SelectedItem = AppSettings.Instance.DownloadLastEpisodesCount;

            RewindSteps.ItemsSource = new[] { 10, 20, 30, 40, 50, 60 };
            RewindSteps.SelectedItem = AppSettings.Instance.RewindStep;

            ForwardSteps.ItemsSource = new[] { 10, 20, 30, 40, 50, 60 };
            ForwardSteps.SelectedItem = AppSettings.Instance.ForwardStep;

            GlobalStateManager.SelectedMenuIndex = 6;

            var version = Package.Current.Id.Version;
            VersionText.Text = "Podcasts v" + version.StringVersion();

            AutoPlayToggle.IsOn = AppSettings.Instance.AutoPlay;
            MeteredToggle.IsOn = LocalSettings.Instance.Metered;
            StreamOnMeteredToggle.IsOn = LocalSettings.Instance.StreamOnMetered;
            NotificationToggle.IsOn = LocalSettings.Instance.Notifications;
            DeleteWhenPlayedToggle.IsOn = LocalSettings.Instance.DeleteDownloadWhenPlayed;
            SystemAccentColorToggle.IsOn = LocalSettings.Instance.UseSystemAccent;
            AutomaticallyAddNewEpisodeToPlayListToggle.IsOn = AppSettings.Instance.AutomaticallyAddNewEpisodeToPlaylist;
            DarkThemeToggle.IsOn = LocalSettings.Instance.DarkTheme;
            CloudSyncToggle.IsOn = LocalSettings.Instance.CloudSync;
            VideoPlaybackToggle.IsOn = LocalSettings.Instance.VideoPlayback;
            InvertSkipControlsToggle.IsOn = LocalSettings.Instance.InvertSkipControls;
            RemovedPlayedEpisodeFromPlayListToggle.IsOn = LocalSettings.Instance.RemovedPlayedEpisodeFromPlayList;
            AutoSyncOnCloseToggle.IsOn = LocalSettings.Instance.AutoSyncOnClose;

            ExternalStorageToggle.IsOn = false;
            if (!string.IsNullOrEmpty(LocalSettings.Instance.ExternalFolderPath))
            {
                PickedFolder.Text = LocalSettings.Instance.ExternalFolderPath;
                ExternalStorageToggle.IsOn = true;
            }

            ExternalStorageToggle.Toggled += ExternalStorageToggle_Toggled;

            var source = new[] {
                new { value = 5, label = "5" },
                new { value = 10, label = "10" },
                new { value = 20, label = "20" },
                new { value = 30, label = "30" },
                new { value = 40, label = "40" },
                new { value = 50, label = "50" },
                new { value = 60, label = "60" },
                new { value = 70, label = "70" },
                new { value = 80, label = "80" },
                new { value = 90, label = "90" },
                new { value = 100, label = "100" },
                new { value = 1000, label = "1000" },
                new { value = int.MaxValue, label = StringsHelper.Forever }};

            DeleteOderThanDay.ItemsSource = source;
            DeleteOderThanDay.DisplayMemberPath = "label";
            DeleteOderThanDay.SelectedValuePath = "value";
            DeleteOderThanDay.SelectedItem = source.FirstOrDefault(a => a.value == LocalSettings.Instance.DeleteEpisodesOlderThan);

            ClockSeconds.StartAngle = 0;
            ClockCurrent.StartAngle = 0;
            ClockSeconds.EndAngle = 0;
            ClockCurrent.EndAngle = 0;
            ClockMissing.StartAngle = 0;
            ClockMissing.EndAngle = 359.999;

            CheckTimer();

            if (SleepTimer.IsStarted)
            {
                StartTimer.Content = StringsHelper.Stop;
                SleepTimer.OnTick += CheckTimer;
            }
            else
            {
                StartTimer.Content = StringsHelper.Start;
            }

            if (!AppSettings.Instance.TipSent)
            {
                try
                {
                    if ((Application.Current as App).LicenseInformation == null)
                    {
                        Pivot.Items.Remove(SupportPivot);
                    }
                    else
                    {
                        var active = (Application.Current as App).LicenseInformation.ProductLicenses["Support"].IsActive || (Application.Current as App).LicenseInformation.ProductLicenses["SupportMax"].IsActive;

                        if (active)
                        {
                            MarkSupportActivated();
                        }
                    }
                }
                catch
                {
                    // Ignore error
                }
            }
            else
            {
                MarkSupportActivated();
            }
        }

        void MarkSupportActivated()
        {
            Pivot.Items.Remove(SupportPivot);
            Pivot.Items.Add(SupportPivot);
            SupportButton.Visibility = Visibility.Collapsed;
            SupportMaxButton.Visibility = Visibility.Collapsed;
            SupportText.Text = StringsHelper.ThankYou;
        }

        private void CheckTimer()
        {
            if (!SleepTimer.IsStarted)
            {
                ClockSeconds.StartAngle = 0;
                ClockCurrent.StartAngle = 0;
                ClockMissing.StartAngle = 0;
                ClockMissing.EndAngle = 359.999;

                ClockSeconds.AnimateEndAngleTo(0, true);
                ClockCurrent.AnimateEndAngleTo(0, true);

                ClockText.Text = LocalSettings.Instance.SleepTimerDuration + "min";
                StartTimer.Content = StringsHelper.Start;
                RemoveTime.IsEnabled = true;
                AddTime.IsEnabled = true;
                return;
            }

            StartTimer.Content = StringsHelper.Stop;
            RemoveTime.IsEnabled = false;
            AddTime.IsEnabled = false;
            var diff = DateTime.Now.Subtract(SleepTimer.StartDate);
            var total = LocalSettings.Instance.SleepTimerDuration;

            var secondsEndAngle = 359.999 * (diff.Seconds + diff.Milliseconds / 1000.0) / 60.0;
            ClockSeconds.AnimateEndAngleTo(secondsEndAngle, secondsEndAngle >= ClockSeconds.EndAngle);
            ClockCurrent.AnimateEndAngleTo((359.999 * diff.TotalMinutes) / total, true);

            ClockMissing.StartAngle = ClockCurrent.EndAngle;

            var add = diff.TotalMinutes == 0 ? 0 : 1;
            ClockText.Text = ((int)(total - diff.TotalMinutes) + add) + "min";
        }

        private async void ExternalStorageToggle_Toggled(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            foreach (var podcast in Library.Podcasts)
            {
                await Library.RemovePodcastLocalDataAsync(podcast, true, !string.IsNullOrEmpty(LocalSettings.Instance.ExternalFolderPath));
            }
            WaitRingManager.IsWaitRingVisible = false;

            if (ExternalStorageToggle.IsOn)
            {
                try
                {
                    var picker = new FolderPicker();
                    picker.FileTypeFilter.Add("*");
                    var folder = await picker.PickSingleFolderAsync();

                    if (folder != null)
                    {
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace("ExternalStorage", folder);
                        PickedFolder.Text = folder.Path;
                        LocalSettings.Instance.ExternalFolderPath = folder.Path;
                    }
                    else
                    {
                        ExternalStorageToggle.IsOn = false;
                    }
                }
                catch
                {
                    ExternalStorageToggle.IsOn = false;
                }
                return;
            }

            LocalSettings.Instance.ExternalFolderPath = "";
            PickedFolder.Text = "";
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("ExternalStorage"))
            {
                StorageApplicationPermissions.FutureAccessList.Remove("ExternalStorage");
            }
        }

        private async void Load_OnClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".cast");
            filePicker.FileTypeFilter.Add(".xml");
            filePicker.FileTypeFilter.Add(".opml");

            var file = await filePicker.PickSingleFileAsync();

            if (file != null)
            {
                WaitRingManager.IsWaitRingVisible = true;
                try
                {
                    Playlist.CurrentPlaylist.Clear();
                    if (file.FileType.ToLower() == ".cast")
                    {
                        await Library.LoadCastFile(file, false);
                    }
                    else
                    {
                        await Library.LoadOPMLFile(file, false);
                    }
                    await Library.SaveAsync();
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await App.MessageAsync(StringsHelper.Success_LoadFromFile);
                    await WaitRingManager.ShowBlurBackground(false);

                    GlobalStateManager.CurrentShell.Navigate(typeof(LibraryPage));
                }
                catch (Exception ex)
                {
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await Messenger.ErrorAsync($"{StringsHelper.Error_LoadFromFile}: {ex.Message}");
                    await WaitRingManager.ShowBlurBackground(false);
                }
            }
        }

        private async void Save_OnClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileSavePicker { DefaultFileExtension = ".cast" };
            filePicker.FileTypeChoices.Add(StringsHelper.CastFiles, new[] { ".cast" });
            filePicker.FileTypeChoices.Add(StringsHelper.OPMLFiles, new[] { ".xml", ".opml" });

            var file = await filePicker.PickSaveFileAsync();

            if (file != null)
            {
                WaitRingManager.IsWaitRingVisible = true;
                try
                {
                    if (file.FileType.ToLower() == ".cast")
                    {
                        await Library.SaveCastFile(file);
                    }
                    else
                    {
                        await Library.SaveOPMLFile(file);
                    }
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await App.MessageAsync(StringsHelper.Success_SaveToFile);
                    await WaitRingManager.ShowBlurBackground(false);
                }
                catch
                {
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await Messenger.ErrorAsync(StringsHelper.Error_SaveToFile);
                    await WaitRingManager.ShowBlurBackground(false);
                }
            }
        }

        private void LastEpisodes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LastEpisodes.SelectedItem == null)
            {
                return;
            }
            AppSettings.Instance.DownloadLastEpisodesCount = LastEpisodes.SelectedIndex;
        }

        private void AutoPlayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.AutoPlay = AutoPlayToggle.IsOn;
        }

        private void RewindSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RewindSteps.SelectedItem == null)
            {
                return;
            }
            AppSettings.Instance.RewindStep = (int)RewindSteps.SelectedItem;
        }

        private void ForwardStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ForwardSteps.SelectedItem == null)
            {
                return;
            }
            AppSettings.Instance.ForwardStep = (int)ForwardSteps.SelectedItem;
        }

        private void MeteredToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.Metered = MeteredToggle.IsOn;
        }

        private void DeleteWhenPlayed_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.DeleteDownloadWhenPlayed = DeleteWhenPlayedToggle.IsOn;
        }

        private void NotificationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.Notifications = NotificationToggle.IsOn;
        }

        private void SystemAccentColorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.UseSystemAccent = SystemAccentColorToggle.IsOn;
        }

        private void AutomaticallyAddNewEpisodeToPlayListToggle_Toggled(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.AutomaticallyAddNewEpisodeToPlaylist = AutomaticallyAddNewEpisodeToPlayListToggle.IsOn;
        }

        private void DeleteOderThanDays_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LocalSettings.Instance.DeleteEpisodesOlderThan = (int)DeleteOderThanDay.SelectedValue;
        }

        private void AddTime_Click(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.SleepTimerDuration += 1;
            CheckTimer();
        }

        private void RemoveTime_Click(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.SleepTimerDuration -= 1;
            CheckTimer();
        }

        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            if (SleepTimer.IsStarted)
            {
                SleepTimer.Stop();
                SleepTimer.OnTick -= CheckTimer;
            }
            else
            {
                SleepTimer.Start();
                SleepTimer.OnTick += CheckTimer;
            }
            CheckTimer();
        }

        private async void PushToCloud_Click(object sender, RoutedEventArgs e)
        {
            WaitRingManager.IsWaitRingVisible = true;
            var oldCloudSync = LocalSettings.Instance.CloudSync;
            LocalSettings.Instance.CloudSync = true;
            await Library.PublishAsync();
            await Playlist.CurrentPlaylist.PublishAsync();
            LocalSettings.Instance.CloudSync = oldCloudSync;
            WaitRingManager.IsWaitRingVisible = false;
        }

        private void DarkThemeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.DarkTheme = DarkThemeToggle.IsOn;
        }

        private async void SupportButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                WaitRingManager.IsWaitRingVisible = true;
                await CurrentApp.RequestProductPurchaseAsync("Support");
                WaitRingManager.IsWaitRingVisible = false;
                var active = (Application.Current as App).LicenseInformation.ProductLicenses["Support"].IsActive;

                if (active)
                {
                    MarkSupportActivated();
                    AppSettings.Instance.TipSent = true;
                    await App.MessageAsync(StringsHelper.ThankYou);
                }
            }
            catch
            {
                WaitRingManager.IsWaitRingVisible = false;
            }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".cast");
            filePicker.FileTypeFilter.Add(".xml");
            filePicker.FileTypeFilter.Add(".opml");

            var file = await filePicker.PickSingleFileAsync();

            if (file != null)
            {
                WaitRingManager.IsWaitRingVisible = true;
                try
                {
                    if (file.FileType.ToLower() == ".cast")
                    {
                        await Library.LoadCastFile(file, true);
                    }
                    else
                    {
                        await Library.LoadOPMLFile(file, true);
                    }
                    await Library.SaveAsync();
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await App.MessageAsync(StringsHelper.Success_LoadFromFile);
                    await WaitRingManager.ShowBlurBackground(false);

                    GlobalStateManager.CurrentShell.Navigate(typeof(LibraryPage));
                }
                catch (Exception ex)
                {
                    WaitRingManager.IsWaitRingVisible = false;
                    await WaitRingManager.ShowBlurBackground(true);
                    await Messenger.ErrorAsync($"{StringsHelper.Error_LoadFromFile}: {ex.Message}");
                    await WaitRingManager.ShowBlurBackground(false);
                }
            }
        }

        private async void SupportMaxButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                WaitRingManager.IsWaitRingVisible = true;
                await CurrentApp.RequestProductPurchaseAsync("SupportMax");
                WaitRingManager.IsWaitRingVisible = false;
                var active = (Application.Current as App).LicenseInformation.ProductLicenses["SupportMax"].IsActive;

                if (active)
                {
                    MarkSupportActivated();
                    AppSettings.Instance.TipSent = true;
                    await App.MessageAsync(StringsHelper.ThankYou);
                }
            }
            catch
            {
                WaitRingManager.IsWaitRingVisible = false;
            }
        }

        private async void CloudSyncToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.CloudSync = CloudSyncToggle.IsOn;

            if (LocalSettings.Instance.CloudSync)
            {
                WaitRingManager.IsWaitRingVisible = true;
                await OneDriveSettings.Instance.Initialize();
                WaitRingManager.IsWaitRingVisible = false;
            }
        }

        private async void ForceGetFromCloud_Click(object sender, RoutedEventArgs e)
        {
            var oldCloudSync = LocalSettings.Instance.CloudSync;
            try
            {
                WaitRingManager.IsWaitRingVisible = true;

                Playlist.CurrentPlaylist.Clear();
                LocalSettings.Instance.CloudSync = true;
                LocalSettings.Instance.ForceCloudSync = true;

                var libraryData = await Library.GetFromCloudAsync();
                var playlistData = await Playlist.GetFromCloudAsync();

                if (!string.IsNullOrEmpty(libraryData))
                {
                    if (!string.IsNullOrEmpty(playlistData))
                    {
                        await Playlist.DumpFromCloudAsync(playlistData);
                    }

                    await Library.DumpFromCloudAsync(libraryData);
                }
            }
            catch (Exception ex)
            {
                await Messenger.ErrorAsync(ex.Message);
                App.TrackException(ex);
            }

            WaitRingManager.IsWaitRingVisible = false;
            LocalSettings.Instance.ForceCloudSync = false;
            LocalSettings.Instance.CloudSync = oldCloudSync;
        }

        private void VideoPlaybackToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.VideoPlayback = VideoPlaybackToggle.IsOn;
        }

        private void InvertSkipControlsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.InvertSkipControls = InvertSkipControlsToggle.IsOn;
        }

        private void RemovedPlayedEpisodeFromPlayListToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.RemovedPlayedEpisodeFromPlayList = RemovedPlayedEpisodeFromPlayListToggle.IsOn;
        }

        private void StreamOnMeteredToggle_OnToggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.StreamOnMetered = StreamOnMeteredToggle.IsOn;
        }

        private void AutoSyncOnCloseToggle_OnToggled(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.AutoSyncOnClose = AutoSyncOnCloseToggle.IsOn;
        }

        private async void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
        {
            App.TrackEvent("Review");
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store:REVIEW?PFN=15798DavidCatuhe.Cast_x8akzp4bebrnj", UriKind.Absolute));
        }
    }
}
