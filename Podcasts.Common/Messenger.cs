using System;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Podcasts
{
    public static class Messenger
    {
        public static async Task<bool> QuestionAsync(string message, string okLabel = null, string cancelLabel = null)
        {
            try
            {
                MessageDialog md = new MessageDialog(message, StringsHelper.Confirmation);

                if (okLabel == null)
                {
                    okLabel = StringsHelper.OK;
                }

                if (cancelLabel == null)
                {
                    cancelLabel = StringsHelper.Cancel;
                }

                bool? result = null;
                md.Commands.Add(new UICommand(okLabel, cmd => result = true));
                md.Commands.Add(new UICommand(cancelLabel, cmd => result = false));

                await md.ShowAsync();
                return result == true;
            }
            catch
            {
                return false;
            }
        }

        public static void Notify(string title, string message, string arguments, string image)
        {
            try
            {
                ToastTemplateType toastTemplate = ToastTemplateType.ToastImageAndText03;
                XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);

                XmlNodeList toastTextElements = toastXml.GetElementsByTagName("text");
                toastTextElements[0].AppendChild(toastXml.CreateTextNode(title));
                toastTextElements[1].AppendChild(toastXml.CreateTextNode(message));

                XmlNodeList toastImageAttributes = toastXml.GetElementsByTagName("image");
                ((XmlElement)toastImageAttributes[0]).SetAttribute("src", image ?? "ms-appx:///Assets/icon.png");

                ((XmlElement)toastXml.SelectSingleNode("/toast")).SetAttribute("launch", arguments);

                ToastNotification toast = new ToastNotification(toastXml);

                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            catch
            {
                // Ignore error
            }
        }

        public static void Notify(string message, string arguments)
        {
            try
            {
                ToastTemplateType toastTemplate = ToastTemplateType.ToastImageAndText01;
                XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);

                XmlNodeList toastTextElements = toastXml.GetElementsByTagName("text");
                toastTextElements[0].AppendChild(toastXml.CreateTextNode(message));

                XmlNodeList toastImageAttributes = toastXml.GetElementsByTagName("image");
                ((XmlElement)toastImageAttributes[0]).SetAttribute("src", "ms-appx:///Assets/icon.png");

                ((XmlElement)toastXml.SelectSingleNode("/toast")).SetAttribute("launch", arguments);

                ToastNotification toast = new ToastNotification(toastXml);

                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            catch
            {
                // Ignore error
            }
        }

        public static async Task ErrorAsync(string message)
        {
            try
            {
                MessageDialog md = new MessageDialog(message, StringsHelper.Error);

                md.Commands.Add(new UICommand(StringsHelper.OK));

                await md.ShowAsync();
            }
            catch
            {
                // Ignore error
            }
        }
    }
}
