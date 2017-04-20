using System;
using Windows.UI.Xaml.Controls;

namespace Podcasts
{
    public sealed partial class IconButton : UserControl
    {
        public event EventHandler OnClick;
        public IconButton()
        {
            InitializeComponent();
        }

        public string Text
        {
            set
            {
                TextBlock.Text = value;
            }
        }

        public string Glyph
        {
            set
            {
                GlyphIcon.Text = value;
            }
        }

        private void ContentPresenter_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            GlobalScaleTransform.ScaleX = 0.8;
            GlobalScaleTransform.ScaleY = 0.8;
        }

        private void ContentPresenter_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScaleTransform.ScaleX = 1.2;
            ScaleTransform.ScaleY = 1.2;
            Opacity = 0.8;
        }

        private void ContentPresenter_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScaleTransform.ScaleX = 1.0;
            ScaleTransform.ScaleY = 1.0;
            Opacity = 1.0;
        }

        private void ContentPresenter_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            OnClick?.Invoke(this, null);
            e.Handled = true;
            GlobalScaleTransform.ScaleX = 1.0;
            GlobalScaleTransform.ScaleY = 1.0;
        }
    }
}
