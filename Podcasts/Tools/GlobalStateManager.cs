using System;

namespace Podcasts
{
    public static class GlobalStateManager
    {
        internal static Action<int> OnSelectedMenuIndexChanged;
        internal static Func<int> OnGetSelectedMenuIndexRequired;

        public static Shell CurrentShell { get; set; }

        public static int SelectedMenuIndex
        {
            set
            {
                OnSelectedMenuIndexChanged?.Invoke(value);
            }
            get
            {
                return OnGetSelectedMenuIndexRequired != null ? OnGetSelectedMenuIndexRequired() : -1;
            }
        }
    }
}
