using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Podcasts
{
    public sealed partial class BrowseControl : UserControl
    {
        public event Action<string, string, string> OnUpdate;

        public BrowseControl()
        {
            InitializeComponent();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            CloseFlyout();
            OnUpdate?.Invoke(URL.Text, Login.Text, Password.Password);
        }

        private void URL_TextChanged(object sender, TextChangedEventArgs e)
        {
            Validate.IsEnabled = !string.IsNullOrEmpty(URL.Text);
        }

        void CloseFlyout()
        {
            if (Parent == null)
            {
                return;
            }

            ((Parent as FlyoutPresenter).Parent as Popup).IsOpen = false;
        }

        public void Reset()
        {
            URL.Text = "";
            Login.Text = "";
            Password.Password = "";
        }
    }
}
