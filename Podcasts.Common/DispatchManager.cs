using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Podcasts
{
    public static class DispatchManager
    {
        public static void RunOnDispatcher(Action action)
        {
            RunOnDispatcherAsync(action).Wait();
        }       

        public static async Task RunOnDispatcherAsync(Action action)
        {
            if (CoreTools.GlobalDispatcher == null || CoreTools.GlobalDispatcher.HasThreadAccess)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    CoreTools.ShowDebugToast(ex.Message, "RunOnDispatcherAsync");
                }
                return;
            }

            await CoreTools.GlobalDispatcher.AwaitableRunAsync(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    CoreTools.ShowDebugToast(ex.Message, "RunOnDispatcherAsync");
                }
            });
        }
    }
}
