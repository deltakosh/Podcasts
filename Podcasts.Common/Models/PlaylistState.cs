using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Podcasts
{
    public class PlaylistState: Notifier
    {
        bool isPlaying;
        public bool IsPlaying
        {
            get
            {
                return isPlaying;
            }
            set
            {
                isPlaying = value;
                RaisePropertyChanged(nameof(IsPlaying));
            }
        }

        bool isLoading = true;
        public bool IsLoading
        {
            get
            {
                return isLoading;
            }
            set
            {
                isLoading = value;
                RaisePropertyChanged(nameof(IsLoading));
            }
        }

        bool isStreaming;
        public bool IsStreaming
        {
            get
            {
                return isStreaming;
            }
            set
            {
                isStreaming = value;
                RaisePropertyChanged(nameof(IsStreaming));
            }
        }
    }
}
