using System;
using System.ComponentModel;
using System.Linq.Expressions;
using Windows.UI.Core;

namespace Podcasts
{
    public abstract class Notifier : INotifyPropertyChanged
    {
        public static bool BlockUpdates { get; set; }

        public static CoreDispatcher Dispatcher
        {
            get; set;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected Notifier()
        {
        }


        protected void RaisePropertyChanged(string propertyName)
        {
            if (BlockUpdates || PropertyChanged == null)
            {
                return;
            }

            if (Dispatcher != null && !Dispatcher.HasThreadAccess)
            {
#pragma warning disable CS4014
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        if (PropertyChanged != null)
                        {
                            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                        }
                    }
                    catch
                    {
                        // Ignore error
                    }
                });
#pragma warning restore CS4014
            }
            else
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
