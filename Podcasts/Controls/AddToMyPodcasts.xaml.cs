using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Podcasts.Tools;

namespace Podcasts
{
    public sealed partial class AddToMyPodcasts
    {
        Podcast podcast;

        public event Action OnUpdate;
        private bool firstTime;

        public bool NavigateOnSuccess { get; set; }

        public AddToMyPodcasts()
        {
            InitializeComponent();
        }

        public void SetPodcast(Podcast newPodcast)
        {            
            podcast = newPodcast;
            firstTime = true;

            if (podcast.Category == null)
            {
                Category.Text = "";
                return;
            }
            Category.Text = podcast.Category;
        }

        private async void Category_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Category.Text))
            {
                if (firstTime)
                {
                    firstTime = false;
                    await Task.Delay(500);
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Category.ItemsSource = Library.Categories.DistinctBy(c => c.ToLower()).OrderBy(c => c);
                    Category.IsSuggestionListOpen = true;
                });
            }
        }

        private void Category_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            Validate.IsEnabled = !string.IsNullOrEmpty(sender.Text);
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                Category.ItemsSource = Library.Categories.Where(c => c.ContainsIgnoreCase(sender.Text)).DistinctBy(c => c.ToLower()).OrderBy(c => c);
            }
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            if (podcast.Category == null)
            {
                podcast.Category = Category.Text;
                podcast.AddToLibrary();

                CloseFlyout();

                if (NavigateOnSuccess)
                {
                    GlobalStateManager.CurrentShell.Navigate(typeof (LibraryPage));
                }
            }
            else
            {
                CloseFlyout();

                if (podcast.Category == Category.Text)
                {
                    return;
                }

                podcast.Category = Category.Text;

            }

            OnUpdate?.Invoke();
        }

        void CloseFlyout()
        {
            if (Parent == null)
            {
                return;
            }

            ((Parent as FlyoutPresenter).Parent as Popup).IsOpen = false;
        }

        void AddToMyPodcasts_OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Validate_Click(this, null);
                e.Handled = true;
            }
        }
    }
}
