using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;

namespace Podcasts
{
    public static class SleepTimer
    {
        static DispatcherTimer Timer;
        public static DateTime StartDate
        {
            get; set;
        }

        public static event Action OnTick;

        public static bool IsStarted
        {
            get
            {
                return Timer != null;
            }
        }

        public static void Start()
        {
            if (IsStarted)
            {
                return;
            }
            StartDate = DateTime.Now;
            Timer = new DispatcherTimer();

            Timer.Interval = TimeSpan.FromSeconds(1);
            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        private static void Timer_Tick(object sender, object e)
        {
            var diff = DateTime.Now.Subtract(StartDate);
            var total = LocalSettings.Instance.SleepTimerDuration;

            if (diff.TotalMinutes > total)
            {
                MediaPlayerHost.Pause(false);
                Dispose();
            }
            OnTick?.Invoke();
        }

        public static void Stop()
        {
            if (!IsStarted)
            {
                return;
            }
            Dispose();
        }

        public static void Dispose()
        {
            if (Timer == null)
            {
                return;
            }
            Timer.Stop();
            Timer.Tick -= Timer_Tick;
            Timer = null;
        }
    }
}
