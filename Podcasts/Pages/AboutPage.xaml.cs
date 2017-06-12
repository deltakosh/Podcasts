using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.System;

namespace Podcasts
{
    public sealed partial class AboutPage
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void AboutPage_Loaded(object sender, RoutedEventArgs e)
        {
            var version = Package.Current.Id.Version;
            VersionText.Text = "Podcasts v" + version.StringVersion();

            GlobalStateManager.SelectedMenuIndex = 6;


            if (!AppSettings.Instance.TipSent)
            {
                try
                {
                    var active = (Application.Current as App).LicenseInformation.ProductLicenses["Support"].IsActive || (Application.Current as App).LicenseInformation.ProductLicenses["SupportMax"].IsActive;

                    if (active)
                    {
                        MarkSupportActivated();
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
            SupportButton.Visibility = Visibility.Collapsed;
            SupportMaxButton.Visibility = Visibility.Collapsed;
            SupportText.Text = StringsHelper.ThankYou;
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

        private async void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
        {
            App.TrackEvent("Review");
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store:REVIEW?PFN=15798DavidCatuhe.Cast_x8akzp4bebrnj", UriKind.Absolute));
        }
    }
}
