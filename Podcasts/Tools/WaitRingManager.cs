using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Podcasts
{
    public static class WaitRingManager
    {
        internal static Action<bool> OnSetWaitRingRequired;
        internal static Func<bool> OnGetWaitRingRequired;
        internal static Func<bool, Task> OnShowBlurBackgroundRequired;

        public static async Task ShowBlurBackground(bool visible)
        {
            if (OnShowBlurBackgroundRequired == null)
            {
                return;
            }

            await OnShowBlurBackgroundRequired(visible);
        }

        static async void ReSendIsWaitRingVisible(bool value)
        {
            await CoreTools.GlobalDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                OnSetWaitRingRequired?.Invoke(value);
            });
        }

        public static bool IsWaitRingVisible
        {
            set
            {
                try
                {
                    if (!CoreTools.GlobalDispatcher.HasThreadAccess)
                    {
                        ReSendIsWaitRingVisible(value);
                        return;
                    }
                    OnSetWaitRingRequired?.Invoke(value);
                }
                catch
                {
                    // Ignore error
                }
            }
            get
            {
                try
                {
                    return OnGetWaitRingRequired?.Invoke() ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
