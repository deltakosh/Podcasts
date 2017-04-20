using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage.AccessCache;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Store;
using Windows.Globalization;
using System.Diagnostics;
using Windows.UI.Core;
using Windows.UI.Popups;
using Microsoft.HockeyApp;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.ExtendedExecution;

namespace Podcasts
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App
    {
        public static bool IsActive;
        public LaunchActivatedEventArgs SavedArgs { get; set; }
        public LicenseInformation LicenseInformation { get; private set; }
        bool viewCreationInProgress;

        public static async Task MessageAsync(string message)
        {
            if ((Application.Current as App).IsInBackground)
            {
                return;
            }

            try
            {
                MessageDialog md = new MessageDialog(message);

                md.Commands.Add(new UICommand(StringsHelper.OK));

                await md.ShowAsync();
            }
            catch
            {
                // Ignore error
            }
        }

        public App()
        {
            try
            {
                HockeyClient.Current.Configure("efdbcacb82864a0088f1b959e837e951");
            }
            catch
            {
                // Ignoring error
            }

            try
            {
                PackageVersion pv = Package.Current.Id.Version;
                GoogleAnalytics.EasyTracker.GetTracker().AppVersion = $"{pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";
            }
            catch
            {
                // Ignoring error
            }


            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += OnResuming;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            if (CoreTools.IsRunningOnMobile)
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait | DisplayOrientations.PortraitFlipped;
            }
            LocalSettings.Instance.NotificationMessage = StringsHelper.NotificationMessage;

            RequestedTheme = LocalSettings.Instance.DarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light;

            LocalSettings.Instance.ForegroundTaskIsRunning = true;
            LocalSettings.Instance.ForceCloudSync = false;

            // License
            try
            {
# if DEBUG
                LicenseInformation = CurrentAppSimulator.LicenseInformation;
#else
                LicenseInformation = CurrentApp.LicenseInformation;
#endif
            }
            catch
            {
                // No windows store account
            }

            if (CoreTools.IsRunningOnXbox)
            {
               // RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            }

            Construct();
        }

        private void OnResuming(object sender, object e)
        {
            Debug.WriteLine("OnResuming");
            //await CreateViewAsync();
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                TrackException(e.Exception);
                GoogleAnalytics.EasyTracker.GetTracker().SendException("Unobserved Task exception: " + e.Exception.Message, false);
            }
            e.SetObserved();
        }

        public static void TrackException(Exception ex)
        {
            try
            {
                HockeyClient.Current.TrackException(ex);
            }
            catch
            {
                // Ignore error
            }
        }

        public static void TrackMetric(string metricName, int value)
        {
            try
            {
                GoogleAnalytics.EasyTracker.GetTracker().SendEvent("Metric", metricName, value.ToString(), value);
            }
            catch
            {
                // Ignore error
            }
        }

        public static void TrackEvent(string eventName)
        {
            try
            {
                HockeyClient.Current.TrackEvent(eventName);
                GoogleAnalytics.EasyTracker.GetTracker().SendEvent("General", eventName, null, 0);
            }
            catch
            {
                // Ignore error
            }
        }

        public static void TrackPage(string pageName)
        {
            try
            {
                HockeyClient.Current.TrackPageView(pageName);
                GoogleAnalytics.EasyTracker.GetTracker().SendView(pageName);
            }
            catch
            {
                // Ignore error
            }
        }

        public static async Task<double> RunOnDispatcherAsync(Func<double> action)
        {
            double result = 0;
            if (CoreTools.GlobalDispatcher == null)
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    TrackException(ex);
                }
            }
            await CoreTools.GlobalDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    TrackException(ex);
                }

            });

            return result;
        }

        public void MergeResources()
        {
            var accentResourceDictionary = LocalSettings.Instance.UseSystemAccent ? new ResourceDictionary { Source = new Uri("ms-appx:///Styles/DefaultAccent.xaml") } : new ResourceDictionary { Source = new Uri("ms-appx:///Styles/Accent.xaml") };

            ResourceDictionary themeResourceDictionary;
            themeResourceDictionary = LocalSettings.Instance.DarkTheme ? new ResourceDictionary { Source = new Uri("ms-appx:///Styles/Darktheme.xaml") } : new ResourceDictionary { Source = new Uri("ms-appx:///Styles/LightTheme.xaml") };

            var mainResourceDictionary = new ResourceDictionary { Source = new Uri("ms-appx:///Styles/DefaultStyles.xaml") };
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(accentResourceDictionary);
            Resources.MergedDictionaries.Add(themeResourceDictionary);
            Resources.MergedDictionaries.Add(mainResourceDictionary);
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Window.Current.VisibilityChanged += Current_VisibilityChanged;

            if (e.PrelaunchActivated)
            {
                Exit();
                return;
            }
            LocalSettings.Instance.ForegroundTaskIsRunning = true;

            if (IsActive)
            {
                if (!string.IsNullOrEmpty(e.Arguments))
                {
                    switch (e.Arguments)
                    {
                        case "library":
                            GlobalStateManager.CurrentShell.Navigate(typeof(LibraryPage));
                            break;
                    }
                }

                Window.Current.Activate();
                return;
            }

            MergeResources();

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(500, 500));

            if (CoreTools.IsRunningOnXbox)
            {
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            }

            await StatusBarHelper.HideAsync();

            SavedArgs = e;

            // Settings
            LocalSettings.Instance = Resources["LocalSettings"] as LocalSettings;

            // External folder
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("ExternalStorage"))
            {
                // We probably need to wait if the path is on a SD card
                var retry = 0;
                while (retry < 4)
                {
                    try
                    {
                        var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("ExternalStorage");
                        if (folder != null)
                        {
                            LocalSettings.Instance.ExternalFolderPath = folder.Path;
                        }
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(500);
                        retry++;
                    }
                }
            }

            CoreTools.GlobalDispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            await CreateViewAsync(e.Arguments);

            IsActive = true;
        }

        public static async Task<Shell> CreateShellFrameAsync()
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            var shell = new Shell();

            var rootFrame = new Frame { Language = ApplicationLanguages.Languages[0] };
            // Set the default language

            if (await shell.DeserializeAsync())
            {
                try
                {
                    var navigationState = CoreTools.GetSerializedStringValue("NavigationState", true);

                    if (navigationState != null && navigationState.Contains("Podcasts."))
                    {
                        rootFrame.SetNavigationState(navigationState);
                    }
                }
                catch
                {
                    // Ignore error
                }
            }

            shell.SetFrame(rootFrame);

            return shell;
        }

        public static async void EnableShell(Shell shell)
        {
            // Place the frame in the current Window
            Window.Current.Content = shell;

            // Check cloud
            if (!LocalSettings.Instance.CloudSync)
            {
                return;
            }
            if (!AppSettings.Instance.OneDriveWarningDisplayed)
            {
                AppSettings.Instance.OneDriveWarningDisplayed = true;

                if (!await Messenger.QuestionAsync(StringsHelper.OneDriveWarning, StringsHelper.OneDriveYes, StringsHelper.OneDriveCancel))
                {
                    LocalSettings.Instance.CloudSync = false;
                    return;
                }
            }

            OneDriveSettings.Instance.CheckInProgress = true;
            await OneDriveSettings.Instance.Initialize();
            string libraryData = await Library.GetFromCloudAsync();
            string playlistData = await Playlist.GetFromCloudAsync();

            if (!string.IsNullOrEmpty(libraryData) || !string.IsNullOrEmpty(playlistData))
            {
                if (await Messenger.QuestionAsync(StringsHelper.DataFound))
                {
                    WaitRingManager.IsWaitRingVisible = true;
                    if (!string.IsNullOrEmpty(playlistData))
                    {
                        await Playlist.DumpFromCloudAsync(playlistData);
                    }

                    if (!string.IsNullOrEmpty(libraryData))
                    {
                        await Library.DumpFromCloudAsync(libraryData);
                    }

                    WaitRingManager.IsWaitRingVisible = false;
                }
            }
            OneDriveSettings.Instance.CheckInProgress = false;
        }

        private async Task CreateViewAsync(string arguments = null)
        {
            lock (this)
            {
                if (viewCreationInProgress)
                {
                    return;
                }
                viewCreationInProgress = true;
            }
            Debug.WriteLine("CreateViewAsync");
            Frame rootFrame = Window.Current.Content as Frame;

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                if (IsActive)
                {
                    try
                    {
                        Debug.WriteLine("Creating shell");
                        var shell = await CreateShellFrameAsync();
                        EnableShell(shell);
                    }
                    catch
                    {
                        // Ignore error
                    }
                }
                else
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    rootFrame = new Frame();

                    // Place the frame in the current Window
                    Window.Current.Content = rootFrame;

                    if (rootFrame.Content == null)
                    {
                        // When the navigation stack isn't restored navigate to the first page,
                        // configuring the new page by passing required information as a navigation
                        // parameter
                        rootFrame.Navigate(typeof(Splash), arguments);
                    }
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();

            lock (this)
            {
                viewCreationInProgress = false;
            }
        }

        private void Current_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                CoreTools.SetBadgeNumber(0);

                if (Library.FullRefreshExecutedOnce)
                {
                    Library.RefreshAllPodcasts();
                }
            }
        }

        async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                using (var session = new ExtendedExecutionSession())
                {
                    LocalSettings.Instance.ForegroundTaskIsRunning = false;

                    Debug.WriteLine("OnSuspending started");

                    session.Reason = ExtendedExecutionReason.SavingData;
                    session.Description = "OneDrive Sync";
                    var result = await session.RequestExtensionAsync();

                    await Playlist.CurrentPlaylist.SaveAsync();
                    await Library.SaveAsync();

                    if (result == ExtendedExecutionResult.Allowed && LocalSettings.Instance.AutoSyncOnClose)
                    {
                        Debug.WriteLine("ExtendedExecution granted");
                        await Library.PublishAsync();
                        await Playlist.CurrentPlaylist.PublishAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.WriteLine(ex);
                CoreTools.ShowDebugToast(ex.Message, "OnSuspending");
            }
            Debug.WriteLine("OnSuspending done");
            deferral.Complete();
        }

        partial void Construct();
    }
}