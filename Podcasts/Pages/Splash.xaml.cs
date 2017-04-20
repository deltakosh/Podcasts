using System;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Podcasts
{
    public sealed partial class Splash
    {
        Shell shell;

        public Splash()
        {
            InitializeComponent();

            PositionLogo();

            StatusText.Text = StringsHelper.LoadingMessage;

            Notifier.Dispatcher = Dispatcher;
        }

        private void PositionLogo()
        {
            var splashScreen = (Application.Current as App).SavedArgs.SplashScreen;

            if (splashScreen != null)
            {
                Logo.SetValue(Canvas.LeftProperty, splashScreen.ImageLocation.X);
                Logo.SetValue(Canvas.TopProperty, splashScreen.ImageLocation.Y);
                Logo.Height = splashScreen.ImageLocation.Height / ImageTools.AdaptativeScale;
                Logo.Width = splashScreen.ImageLocation.Width / ImageTools.AdaptativeScale;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var version = Package.Current.Id.Version;

            if (version.StringVersion() != LocalSettings.Instance.CurrentVersion)
            {
                Logo.Visibility = Visibility.Collapsed;
                LogoBis.Visibility = Visibility.Visible;
                LogoBis.MaxWidth = Logo.Width;

                // What's new
                Version.Text = $"{StringsHelper.About}{version.StringVersion()}";
                var file = await CoreTools.GetWhatsNewFileAsync();
                Whatsnew.Visibility = Visibility.Visible;
                var news = await FileIO.ReadLinesAsync(file);
                var first = true;
                foreach (var s in news)
                {
                    if (!first)
                    {
                        var line = new Rectangle
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = 1,
                            Margin = new Thickness(10, 5, 10, 5),
                            Stroke = new SolidColorBrush(Colors.White),
                            Opacity = 0.5
                        };

                        WhatsNewPanel.Children.Add(line);
                    }

                    first = false;

                    var textblock = new TextBlock
                    {
                        Text = s,
                        FontSize = 18,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontWeight = Windows.UI.Text.FontWeights.ExtraLight,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(5),
                        TextWrapping = TextWrapping.WrapWholeWords
                    };
                    WhatsNewPanel.Children.Add(textblock);
                }

                GotIt.Opacity = 0;
            }

            // Background task
            try
            {
                var backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync();
                if (backgroundAccessStatus == BackgroundAccessStatus.AlwaysAllowed ||
                    backgroundAccessStatus == BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
                {
                    var taskName = "BackgroundTask.RefreshTask";
                    var taskRegistered = BackgroundTaskRegistration.AllTasks.Any(task => task.Value.Name == taskName);

                    if (!taskRegistered)
                    {
                        var builder = new BackgroundTaskBuilder
                        {
                            Name = taskName,
                            TaskEntryPoint = "BackgroundTask.RefreshTask"
                        };

                        builder.SetTrigger(new TimeTrigger(60, false));
                        builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                        builder.Register();
                    }
                }
            }
            catch
            {
                // Ignore
            }
        
            var kind = (Application.Current as App).SavedArgs.Kind;

            StatusText.Text = StringsHelper.ParsingMessage;
            await Library.DeserializeAsync();

            LocalSettings.Instance.NewEpisodesCount = 0;            
            StatusText.Text = "";

            // Shell
            shell = await App.CreateShellFrameAsync();

            if (Whatsnew.Visibility != Visibility.Visible)
            {
                App.EnableShell(shell);
                shell = null;
            }
            else
            {
                WaitRing.Visibility = Visibility.Collapsed;
                PleaseWait.Opacity = 0;
                GotIt.Opacity = 1.0;
            }
        }

        private void GotIt_Click(object sender, RoutedEventArgs e)
        {
            LocalSettings.Instance.CurrentVersion = Package.Current.Id.Version.StringVersion();
            App.EnableShell(shell);
            shell = null;
        }

        private void Splash_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionLogo();
        }
    }
}
