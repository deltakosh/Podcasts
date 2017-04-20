using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;

namespace Podcasts
{
    public static class ImageTools
    {
        public static double AdaptativeScale
        {
            get
            {
                if (!CoreTools.IsRunningOnMobile)
                {
                    return 1.0;
                }

                return DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            }
        }

        public async static Task BlurXaml(FrameworkElement rootElement, float blurAmount, IRandomAccessStream outStream)
        {
            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(rootElement);
                IBuffer buffer = await rtb.GetPixelsAsync();
                byte[] outputArray = buffer.ToArray();

                var device = CanvasDevice.GetSharedDevice(false);
                using (var stream = new InMemoryRandomAccessStream())
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)(rtb.PixelWidth), (uint)(rtb.PixelHeight), 96, 96, outputArray);
                    await encoder.FlushAsync();

                    using (var bitmap = await CanvasBitmap.LoadAsync(device, stream))
                    {
                        var renderWidth = (int)bitmap.Size.Width;
                        var renderHeight = (int)(bitmap.Size.Height);

                        using (var renderer = new CanvasRenderTarget(device, renderWidth, renderHeight, bitmap.Dpi))
                        {
                            using (var ds = renderer.CreateDrawingSession())
                            {
                                using (var blur = new GaussianBlurEffect())
                                {
                                    blur.BlurAmount = blurAmount;
                                    blur.BorderMode = EffectBorderMode.Hard;
                                    blur.Optimization = EffectOptimization.Speed;
                                    blur.Source = bitmap;
                                    ds.DrawImage(blur);
                                }
                            }

                            await renderer.SaveAsync(outStream, CanvasBitmapFileFormat.Bmp);
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

    }
}