using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Media.Casting;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.UI;
using Windows.Foundation.Metadata;
using System.Linq;
using Microsoft.Graph;
using Microsoft.Toolkit.Uwp.UI.Controls;

namespace Podcasts
{
    public sealed partial class Shell
    {
        Frame globalFrame;
        int selectedMenuIndex = -1;
        readonly RenderTargetBitmap rtb = new RenderTargetBitmap();
        readonly CastingDevicePicker castingPicker;
        CoreDispatcher coreDispatcher;

        public Shell()
        {
            InitializeComponent();
            GlobalStateManager.CurrentShell = this;

            //Wait Ring
            WaitRingManager.OnGetWaitRingRequired = OnGetWaitRingRequired;
            WaitRingManager.OnSetWaitRingRequired = OnSetWaitRingRequired;
            WaitRingManager.OnShowBlurBackgroundRequired = ShowBlurBackground;

            // Navigation
            GlobalStateManager.OnSelectedMenuIndexChanged = OnSelectedMenuIndexChanged;
            GlobalStateManager.OnGetSelectedMenuIndexRequired = OnGetSelectedMenuIndexRequired;

            if (CoreTools.IsRunningOnMobile)
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait | DisplayOrientations.PortraitFlipped;
            }

            MediaPlayerHost.Attach(mediaPlayerElement);

            try
            {
                if (ApiInformation.IsTypePresent("Windows.Media.Casting.CastingDevicePicker"))
                {
                    castingPicker = new CastingDevicePicker();
                    castingPicker.Filter.SupportsVideo = true;
                    castingPicker.Filter.SupportsAudio = true;
                    castingPicker.CastingDeviceSelected += CastingPicker_CastingDeviceSelected;
                }
            }
            catch
            {
                // Picker not available. Hololens for instance
                Cast.IsEnabled = false;
            }
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            GigglePlaylist();

            if (Playlist.CurrentPlaylist != null && Playlist.CurrentPlaylist.Entries.Count == 0)
            {
                VideoPlayerGrid.Visibility = Visibility.Collapsed;
                VideoControls.Visibility = Visibility.Collapsed;
            }
        }

        private async void CastingPicker_CastingDeviceSelected(CastingDevicePicker sender, CastingDeviceSelectedEventArgs args)
        {
            //Casting must occur from the UI thread.  This dispatches the casting calls to the UI thread.
            await DispatchManager.RunOnDispatcherAsync(async () =>
            {
                //Create a casting connection from our selected casting device
                CastingConnection connection = args.SelectedCastingDevice.CreateCastingConnection();

                //Cast the content loaded in the media element to the selected casting device
                await connection.RequestStartCastingAsync(MediaPlayerHost.CastingSource);
            });
        }

        private void MediaPlayerHost_OnVideoPlayerDisengaged()
        {
            VideoPlayerGrid.Visibility = Visibility.Collapsed;
            VideoControls.Visibility = Visibility.Collapsed;
            CoreTools.ReleaseDisplay();
        }

        private void MediaPlayerHost_OnVideoPlayerEngaged()
        {
            VideoPlayerGrid.Visibility = Visibility.Visible;
            VideoControls.Visibility = Visibility.Visible;
            CoreTools.ActivateDisplay();
        }

        async Task StartDissolvingAsync()
        {
            try
            {
                await rtb.RenderAsync(SplitContent);
                DissolveImage.Source = rtb;

                DissolveImage.Visibility = Visibility.Visible;
                DissolveImage.Opacity = 1.0;
                AnimationTools.AnimateDouble(DissolveImage, "Opacity", 0.0, 300, () =>
                {
                    DissolveImage.Visibility = Visibility.Collapsed;
                });

                SplitContentTransform.Y = 25;
                AnimationTools.AnimateDouble(SplitContentTransform, "Y", 0, 150);
            }
            catch
            {
                // Ignore error
                DissolveImage.Visibility = Visibility.Collapsed;
            }
        }

        async Task ShowBlurBackground(bool visible)
        {
            if (visible)
            {
                await ClientGrid.Blur(4, 200).StartAsync();
            }
            else
            {
                await ClientGrid.Blur(0, 0).StartAsync();
            }
        }

        Frame GlobalFrame
        {
            get
            {
                return globalFrame;
            }
            set
            {
                globalFrame = value;
                DataContext = globalFrame;

                globalFrame.Navigated += GlobalFrame_Navigated;
            }
        }

        private void GlobalFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (GlobalFrame.BackStackDepth > 30)
            {
                GlobalFrame.BackStack.RemoveAt(0);
                navigationStack.StatesStack.RemoveAt(0);
            }

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = GlobalFrame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        public string SelectedMenu
        {
            set
            {
                var control = FindName(value);

                var radioButton = (RadioButton)control;
                if (radioButton != null) radioButton.IsChecked = true;
            }
        }

        bool OnGetWaitRingRequired()
        {
            return waitRing.Visibility == Visibility.Visible;
        }

        void OnSetWaitRingRequired(bool value)
        {
            try
            {

                if (HamburgerMenu.DisplayMode == SplitViewDisplayMode.Overlay || HamburgerMenu.DisplayMode == SplitViewDisplayMode.CompactOverlay)
                {
                    HamburgerMenu.IsPaneOpen = false;
                }

                waitRing.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                // Ignore error
            }
        }

        // Navigation

        NavigationStack navigationStack = new NavigationStack();

        public void SetFrame(Frame frame)
        {
            GlobalFrame = frame;
        }

        public void ClearReferences()
        {
            GlobalStateManager.CurrentShell = null;

            if (GlobalFrame != null)
            {
                var page = GlobalFrame.Content as RootPage;
                page?.ClearReferences();
            }

            WaitRingManager.OnGetWaitRingRequired = null;
            WaitRingManager.OnSetWaitRingRequired = null;
            WaitRingManager.OnShowBlurBackgroundRequired = null;
            GlobalStateManager.OnSelectedMenuIndexChanged = null;
            GlobalStateManager.OnGetSelectedMenuIndexRequired = null;

            Playlist.CurrentPlaylist.OnCurrentIndexChanged -= CurrentPlaylist_OnCurrentIndexChanged;
            Playlist.CurrentPlaylist.Entries.CollectionChanged -= Entries_CollectionChanged;

            MediaPlayerHost.OnVideoPlayerEngaged -= MediaPlayerHost_OnVideoPlayerEngaged;
            MediaPlayerHost.OnVideoPlayerDisengaged -= MediaPlayerHost_OnVideoPlayerDisengaged;
            
            SystemNavigationManager.GetForCurrentView().BackRequested -= Shell_BackRequested;
            Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        void Start()
        {
            RootControlGrid.DataContext = Playlist.CurrentPlaylist;

            Playlist.CurrentPlaylist.OnCurrentIndexChanged += CurrentPlaylist_OnCurrentIndexChanged;
            Playlist.CurrentPlaylist.Entries.CollectionChanged += Entries_CollectionChanged;

            MediaPlayerHost.OnVideoPlayerEngaged += MediaPlayerHost_OnVideoPlayerEngaged;
            MediaPlayerHost.OnVideoPlayerDisengaged += MediaPlayerHost_OnVideoPlayerDisengaged;

            if (GlobalFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter

                Navigate(typeof(EpisodePage));
            }
            else
            {
                if (GlobalFrame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                }
            }

            // Back button
            SystemNavigationManager.GetForCurrentView().BackRequested += Shell_BackRequested;
            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
            coreDispatcher = Window.Current.CoreWindow.Dispatcher;
        }

        private async void CurrentPlaylist_OnCurrentIndexChanged()
        {
            await coreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SyncPlaySpeedText();
            });
        }

        private async void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Back && args.KeyStatus.IsKeyReleased)
            {
                var focusElement = FocusManager.GetFocusedElement();

                if (focusElement is TextBox)
                {
                    return;
                }
                args.Handled = true;
                await NavigateBack();
            }
        }

        private async void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (args.CurrentPoint.Properties.IsXButton1Pressed)
            {
                args.Handled = true;
                await NavigateBack();
            }
        }

        private async void Shell_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (GlobalFrame.CanGoBack)
            {
                e.Handled = true;
                await NavigateBack();
            }
        }

        public string GetNavigationState()
        {
            if (GlobalFrame == null)
            {
                return "";
            }
            return GlobalFrame.GetNavigationState();
        }

        public bool IsRootNavigation => navigationStack.StatesStack.Count < 2;

        public async void Navigate(Type type)
        {
            if (HamburgerMenu.DisplayMode == SplitViewDisplayMode.Overlay || HamburgerMenu.DisplayMode == SplitViewDisplayMode.CompactOverlay)
            {
                HamburgerMenu.IsPaneOpen = false;
            }

            lock (navigationStack)
            {
                navigationStack.StatesStack.Add(new StackData());
            }

            await StartDissolvingAsync();
            GlobalFrame.Navigate(type);
        }

        public async void Navigate(Type type, string state, int value = 0)
        {
            lock (navigationStack)
            {
                navigationStack.StatesStack.Add(new StackData { Data = state, ID = value });
            }

            await StartDissolvingAsync();
            GlobalFrame.Navigate(type);
        }

        void ShowFullscreenVideo(bool state)
        {
            mediaPlayerElement.IsFullWindow = state;
            if (CoreTools.IsRunningOnMobile)
            {
                if (!state)
                {
                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait | DisplayOrientations.PortraitFlipped;
                }
                else
                {
                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait | DisplayOrientations.PortraitFlipped | DisplayOrientations.Landscape | DisplayOrientations.LandscapeFlipped;
                }
            }
        }

        public async Task<bool> NavigateBack()
        {
            if (VideoPlayerGrid.Visibility == Visibility.Visible)
            {
                if (mediaPlayerElement.IsFullWindow)
                {
                    ShowFullscreenVideo(false);
                }
                else
                {
                    VideoPlayerGrid.Visibility = Visibility.Collapsed;
                }
                return false;
            }
            if (GlobalFrame.CanGoBack)
            {
                lock (navigationStack)
                {
                    if (navigationStack.StatesStack.Count > 0)
                    { 
                        navigationStack.Pop();
                    }
                }

                await StartDissolvingAsync();
                GlobalFrame.GoBack();
                return true;
            }

            return false;
        }

        public StackData GetTopStack()
        {
            lock (navigationStack)
            {
                if (navigationStack.StatesStack.Count == 0)
                {
                    return new StackData();
                }

                return navigationStack.StatesStack.Last();
            }
        }

        public Type GetCurrentPage()
        {
            return GlobalFrame.CurrentSourcePageType;
        }

        public async Task SerializeAsync()
        {
            try
            {
                CoreTools.SetSerializedValue("NavigationState", true, GlobalStateManager.CurrentShell.GetNavigationState());
                await navigationStack.SerializeAsync();
            }
            catch
            {
                // ignore error
            }
        }

        public async Task<bool> DeserializeAsync()
        {
            try
            {
                navigationStack = await NavigationStack.DeserializeAsync();

                return (navigationStack.StatesStack.Count != 0);
            }
            catch
            {
                return false;
            }
        }

        int OnGetSelectedMenuIndexRequired()
        {
            return selectedMenuIndex;
        }

        void OnSelectedMenuIndexChanged(int value)
        {
            selectedMenuIndex = value;
            switch (value)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                    HamburgerMenu.SelectedIndex = value;
                    break;
                case 5:
                    HamburgerMenu.SelectedOptionsIndex = 0;
                    break;
                case 6:
                    HamburgerMenu.SelectedOptionsIndex = 1;
                    break;
            }
        }

        private void CheckPaneState()
        {
            if (CoreTools.IsRunningOnMobile)
            {
                HamburgerMenu.IsPaneOpen = false;
            }

            if (HamburgerMenu.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                HamburgerMenu.IsPaneOpen = false;
            }
        }  

        private void Play_OnClick(object sender, RoutedEventArgs e)
        {
            if (VideoControls.Visibility == Visibility.Visible && mediaPlayerElement.Visibility == Visibility.Collapsed)
            {
                mediaPlayerElement.Visibility = Visibility.Visible;
            }
            MediaPlayerHost.Play();
        }

        private void Pause_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Pause(true);
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (Playlist.CurrentPlaylist.CurrentIndex > 0)
            {
                Playlist.CurrentPlaylist.CurrentIndex--;
            }
        }

        void Next_OnClick(object sender, RoutedEventArgs e)
        {
            Playlist.CurrentPlaylist.CurrentIndex++;
        }

        void VolumeChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            MediaPlayerHost.Volume = e.NewValue;
        }

        void Volume_OnLoaded(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;

            if (slider == null)
            {
                return;
            }

            slider.Value = MediaPlayerHost.Volume;
        }

        private async void Shell_OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncPlaySpeedText();

            MediaPlayerHost.Volume = LocalSettings.Instance.Volume;

            Start();

            // Start player
            await MediaPlayerHost.StartAsync();

            if (LocalSettings.Instance.FavorRemainingDuration)
            {
                RemainingDuration.Visibility = Visibility.Visible;
                Duration.Visibility = Visibility.Collapsed;
            }
            else
            {
                RemainingDuration.Visibility = Visibility.Collapsed;
                Duration.Visibility = Visibility.Visible;
            }
        }

        private void Rewind_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Position -= AppSettings.Instance.RewindStep;

            if (MediaPlayerHost.IsPaused)
            {
                MediaPlayerHost.SaveCurrentPosition(null, null, true);
            }
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Position += AppSettings.Instance.ForwardStep;

            if (MediaPlayerHost.IsPaused)
            {
                MediaPlayerHost.SaveCurrentPosition(null, null, true);
            }
        }

        private void Speed1X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 100;
            LocalSettings.Instance.PlaySpeed = 100;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void Speed2X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 200;
            LocalSettings.Instance.PlaySpeed = 200;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void Speed15X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 150;
            LocalSettings.Instance.PlaySpeed = 150;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        void CloseFlyout(FrameworkElement control)
        {
            if (control.Parent == null)
            {
                return;
            }

            var flyoutPresenter = control.Parent as FlyoutPresenter;
            var popup = flyoutPresenter?.Parent as Popup;
            if (popup != null)
                popup.IsOpen = false;
        }

        public void SyncPlaySpeedText()
        {
            try
            {
                MediaPlayerHost.Speed = LocalSettings.Instance.PlaySpeed;
                switch (LocalSettings.Instance.PlaySpeed)
                {
                    case 100:
                        PlaySpeed.Tag = "1.0x";
                        break;
                    case 125:
                        PlaySpeed.Tag = "1.25x";
                        break;
                    case 150:
                        PlaySpeed.Tag = "1.5x";
                        break;
                    case 200:
                        PlaySpeed.Tag = "2.0x";
                        break;
                    case 250:
                        PlaySpeed.Tag = "2.5x";
                        break;
                    case 300:
                        PlaySpeed.Tag = "3.0x";
                        break;
                }
            }
            catch
            {
                PlaySpeed.Tag = "1.0x";
            }

            Speed2.Tag = PlaySpeed.Tag;
        }

        private void MediaControlSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            MediaPlayerHost.ForcePosition(e.NewValue);
        }

        private void Speed125X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 125;
            LocalSettings.Instance.PlaySpeed = 125;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void Speed25X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 250;
            LocalSettings.Instance.PlaySpeed = 250;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void Speed3X_OnClick(object sender, RoutedEventArgs e)
        {
            MediaPlayerHost.Speed = 300;
            LocalSettings.Instance.PlaySpeed = 300;
            SyncPlaySpeedText();

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            ShowFullscreenVideo(true);

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        private void Visibility_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayerGrid.Visibility = VideoPlayerGrid.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            var button = sender as Button;
            if (button != null) CloseFlyout(button.Parent as FrameworkElement);
        }

        public void GigglePlaylist()
        {
            DispatchManager.RunOnDispatcher(() =>
            {
                PlaylistTransform.ScaleX = 1.5;
                PlaylistTransform.ScaleY = 1.5;
                AnimationTools.AnimateDouble(PlaylistTransform, "ScaleX", 1.0, 1250, null, false, true);
                AnimationTools.AnimateDouble(PlaylistTransform, "ScaleY", 1.0, 1250, null, false, true);
            });
        }

        private void mediaPlayerElement_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ShowFullscreenVideo(false);
        }

        private void Cast_Click(object sender, RoutedEventArgs e)
        {
            //Retrieve the location of the casting button
            GeneralTransform transform = Cast.TransformToVisual(Window.Current.Content);
            Point pt = transform.TransformPoint(new Point(0, 0));

            //Show the picker above our casting button
            castingPicker.Show(new Rect(pt.X, pt.Y, Cast.ActualWidth, Cast.ActualHeight), Windows.UI.Popups.Placement.Above);
        }

        private void MediaGrip_Click(object sender, RoutedEventArgs e)
        {
            MediaControlsGrid.Visibility = MediaControlsGrid.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

            if (MediaControlsGrid.Visibility == Visibility.Visible)
            {
                MediaGrip.Tag = "";
            }
            else
            {
                MediaGrip.Tag = "";
            }
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (mediaPlayerElement.Visibility == Visibility.Visible && e.Key == VirtualKey.Escape)
            {
                MediaPlayerHost.Pause(false);
                VideoPlayerGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void SlidingGrid_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {

        }

        private void SlidingGrid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {

        }

        private void SlidingGrid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var x = e.Velocities.Linear.X;

            // ignore a little bit velocity (+/-0.1)
            if (x <= -0.1)
            {
            }
            else
            {
                HamburgerMenu.IsPaneOpen = true;
            }
        }

        private void HamburgerMenu_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var menuItem = e.ClickedItem as HamburgerMenuGlyphItem;
            var tag = menuItem.Tag.ToString();

            CheckPaneState();

            switch (tag)
            {
                case "Search":
                    selectedMenuIndex = 0;
                    if (GetCurrentPage() != typeof(SearchPage))
                    {
                        Navigate(typeof(SearchPage));
                    }
                    break;
                case "NowPlaying":
                    selectedMenuIndex = 1;
                    if (GetCurrentPage() != typeof(EpisodePage) || GetTopStack().ID != 0)
                    {
                        Navigate(typeof(EpisodePage));
                    }
                    break;
                case "PlayList":
                    selectedMenuIndex = 2;
                    if (GetCurrentPage() != typeof(PlayListPage))
                    {
                        Navigate(typeof(PlayListPage));
                    }
                    break;
                case "Library":
                    selectedMenuIndex = 3;
                    if (GetCurrentPage() != typeof(LibraryPage))
                    {
                        Navigate(typeof(LibraryPage));
                    }
                    break;
                case "Downloads":
                    selectedMenuIndex = 4;
                    if (GetCurrentPage() != typeof(DownloadsPage))
                    {
                        Navigate(typeof(DownloadsPage));
                    }
                    break;
            }
        }

        private void HamburgerMenu_OnOptionsItemClick(object sender, ItemClickEventArgs e)
        {
            CheckPaneState();

            var menuItem = e.ClickedItem as HamburgerMenuGlyphItem;
            var tag = menuItem.Tag.ToString();

            switch (tag)
            {
                case "Options":
                    selectedMenuIndex = 5;
                    if (GetCurrentPage() != typeof(OptionsPage))
                    {
                        Navigate(typeof(OptionsPage));
                    }
                    break;

                case "About":
                    selectedMenuIndex = 6;
                    if (GetCurrentPage() != typeof(AboutPage))
                    {
                        Navigate(typeof(AboutPage));
                    }
                    break;
            }
        }

        private void Duration_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            RemainingDuration.Visibility = Visibility.Visible;
            Duration.Visibility = Visibility.Collapsed;
            LocalSettings.Instance.FavorRemainingDuration = true;
        }

        private void RemainingDuration_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            Duration.Visibility = Visibility.Visible;
            RemainingDuration.Visibility = Visibility.Collapsed;
            LocalSettings.Instance.FavorRemainingDuration = false;
        }


        private void Car_Click(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Car", true);
        }



        private void Street_Click(object sender, RoutedEventArgs e)
        {
            //Checks for the Window Size to determine what state to return to.
            int width = Convert.ToInt16(Window.Current.Bounds.Width);

            if (width < 400) {
                VisualStateManager.GoToState(this, "Small", true);
            }
            else if (width < 1280) {
                VisualStateManager.GoToState(this, "Medium", true);
            }
            else {
                VisualStateManager.GoToState(this, "Full", true);
            }
        }
    }
}
