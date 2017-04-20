using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.System;
using Windows.UI.Xaml;

namespace Podcasts
{
    sealed partial class App
    {
        public bool IsInBackground { get; set; }

        partial void Construct()
        {
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
            MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;
        }

        private async void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            var level = MemoryManager.AppMemoryUsageLevel;

            Debug.WriteLine("MemoryManager_AppMemoryUsageIncreased: {0}, Mem: {1}K", level, MemoryManager.AppMemoryUsage / 1024);

            if (level == AppMemoryUsageLevel.OverLimit)
            {
                // Unload view for extra memory
                if (IsInBackground && Window.Current != null && Window.Current.Content != null)
                {
                    Debug.WriteLine("Unloading view");
                    if (GlobalStateManager.CurrentShell != null)
                    {
                        await Playlist.CurrentPlaylist.SaveAsync();
                        await GlobalStateManager.CurrentShell.SerializeAsync();
                        GlobalStateManager.CurrentShell.ClearReferences();
                    }
                    Window.Current.Content = null;
                    GC.Collect();
                }
            }
        }

        private async Task SaveState()
        {
            try
            {
                Debug.WriteLine("Save state");
                await GlobalStateManager.CurrentShell.SerializeAsync();
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.WriteLine(ex);
            }
        }

        private async void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            Debug.WriteLine("Entered background");
            IsInBackground = true;

            var deferral = e.GetDeferral();

            try
            {
                await SaveState();
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.WriteLine(ex);
            }
            Debug.WriteLine("Entered background done");
            deferral.Complete();
        }

        private async void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            Debug.WriteLine("Leaving background");
            IsInBackground = false;

            if (IsActive && Window.Current.Content == null)
            {
                Debug.WriteLine("Loading view");
                await CreateViewAsync(string.Empty);
            }
        }
    }
}
