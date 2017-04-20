using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Podcasts
{
    public class RootPage : Page
    {
        StackData stackData;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            UseLayoutRounding = true;            
            base.OnNavigatedTo(e);

            stackData = GlobalStateManager.CurrentShell.GetTopStack();

            if (e.NavigationMode == NavigationMode.Back)
            {
                GlobalStateManager.SelectedMenuIndex = stackData.MenuIndex;
            }

            App.TrackPage(GetType().Name);

            Loaded += RootPage_Loaded;
        }

        private void RootPage_Loaded(object sender, RoutedEventArgs e)
        {
            stackData.MenuIndex = GlobalStateManager.SelectedMenuIndex;
        }

        internal virtual void ClearReferences()
        {

        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back || GlobalStateManager.CurrentShell.IsRootNavigation)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
            }
            Loaded -= RootPage_Loaded;

            ClearReferences();

            base.OnNavigatingFrom(e);
        }

        public void UpdateTopStackFiltersData(string value)
        {
            if (stackData == null)
            {
                return;
            }
            stackData.FiltersData = value;
        }

        public void UpdateTopStackScrollPosition(string value)
        {
            if (stackData == null)
            {
                return;
            }
            stackData.ScrollPosition = value;
        }

        public string GetTopStackScrollPosition()
        {
            return stackData.ScrollPosition;
        }

        public string GetTopStackValueAsString()
        {
            return stackData.Data;
        }

        public string GetTopStackFiltersData()
        {
            return stackData.FiltersData;
        }

        public int[] GetTopStackValueAsIntArray()
        {
            return stackData.IDs;
        }
        public int GetTopStackValueAsInt()
        {
            return stackData.ID;
        }
        public bool IsTopStackValueIntArray()
        {
            return stackData.IDs != null;
        }
        public void SetTopStackValueAsInt(int id)
        {
            stackData.ID = id;
        }

        public bool IsTopStackValueString()
        {
            return !string.IsNullOrEmpty(stackData.Data);
        }

        public bool IsTopStackScrollPosition()
        {
            return !string.IsNullOrEmpty(stackData.ScrollPosition);
        }


        public int GetTopStackMenuIndex()
        {
            return stackData.MenuIndex;
        }
    }
}
