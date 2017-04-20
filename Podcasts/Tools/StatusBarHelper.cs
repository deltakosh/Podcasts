using System;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI;

namespace Podcasts
{
    public static class StatusBarHelper
    {
        public static async Task HideAsync()
        {
            if (!IsSupported)
            {
                return;
            }
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();

            await statusBar.HideAsync();
        }

        public static void SetTransparent()
        {
            if (!IsSupported)
            {
                return;
            }
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();

            statusBar.BackgroundOpacity = 0;
        }

        public static double GetOffset()
        {
            if (!IsSupported)
            {
                return 0;
            }
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();

            return statusBar.OccludedRect.Height;
        }

        public static bool IsSupported => ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar") &&
                                          ApiInformation.IsMethodPresent("Windows.UI.ViewManagement.StatusBar", "HideAsync");
    }
}
