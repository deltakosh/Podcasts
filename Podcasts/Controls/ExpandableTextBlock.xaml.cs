using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Podcasts
{
    public sealed partial class ExpandableTextBlock : UserControl
    {
        public ExpandableTextBlock()
        {
            this.InitializeComponent();
        }

        private void Expand_OnClick(object sender, EventArgs e)
        {
            var flyout = Resources["Tooltip"] as Flyout;

            flyout?.ShowAt(Expand);
        }

        private void TextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var isTrimmed = !string.IsNullOrEmpty(BackText.Text) && BackText.ActualHeight > ActualHeight;
            Expand.Visibility = isTrimmed ? Visibility.Visible : Visibility.Collapsed;

            Text.TextTrimming = isTrimmed ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        }
    }
}
