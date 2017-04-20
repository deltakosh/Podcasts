using Windows.ApplicationModel.Resources;

namespace Podcasts
{
    public static class StringsHelper
    {
        static readonly ResourceLoader Loader = new ResourceLoader();

        public static string GetValue(string key)
        {
            return Loader.GetString(key);
        }

        public static string About => GetValue("About");
        public static string Cancel => GetValue("Cancel");
        public static string CastFiles => GetValue("CastFiles");
        public static string Confirmation => GetValue("Confirmation");
        public static string Confirm_PodcastDelete => GetValue("Confirm_PodcastDelete");
        public static string Error => GetValue("Error");
        public static string Error_LoadFromFile => GetValue("Error_LoadFromFile");
        public static string Error_SaveToFile => GetValue("Error_SaveToFile");
        public static string Error_UnableToParseRSS => GetValue("Error_UnableToParseRSS");
        public static string OK => GetValue("OK");
        public static string PublishedOn => GetValue("PublishedOn");
        public static string SeeAll => GetValue("SeeAll");
        public static string SeeOnlyUnPlayed => GetValue("SeeOnlyUnPlayed");
        public static string Success_AddPodcastToLibrary => GetValue("Success_AddPodcastToLibrary");
        public static string Success_LoadFromFile => GetValue("Success_LoadFromFile");
        public static string Success_SaveToFile => GetValue("Success_SaveToFile");
        public static string Error_UnableToSyncPodcast => GetValue("Error_UnableToSyncPodcast");
        public static string ExtendReason => GetValue("ExtendReason");
        public static string Error_UnableToDeleteDownload => GetValue("Error_UnableToDeleteDownload");
        public static string Error_NoInternet => GetValue("Error_NoInternet");
        public static string ConnectingMessage => GetValue("ConnectingMessage");
        public static string LoadingMessage => GetValue("LoadingMessage");
        public static string ParsingMessage => GetValue("ParsingMessage");
        public static string OPMLFiles => GetValue("OPMLFiles");
        public static string NoCategory => GetValue("NoCategory");
        public static string Unplayed => GetValue("Unplayed");
        public static string Downloads => GetValue("Downloads");
        public static string PodcastsPlural => GetValue("PodcastsPlural");
        public static string NotificationMessage => GetValue("NotificationMessage");
        public static string Start => GetValue("Start");
        public static string Stop => GetValue("Stop");
        public static string ThankYou => GetValue("ThankYou");
        public static string UnplayedEpisodesAsc => GetValue("UnplayedEpisodesAsc");
        public static string UnplayedEpisodesDesc => GetValue("UnplayedEpisodesDesc");
        public static string Forever => GetValue("Forever");
        public static string MandatoryLocator => GetValue("MandatoryLocator");
        public static string All => GetValue("All");
        public static string StreamingIsDisabled => GetValue("StreamingIsDisabled");
        public static string DataFound => GetValue("DataFound");
        public static string OneDriveCancel => GetValue("OneDriveCancel");
        public static string OneDriveWarning => GetValue("OneDriveWarning");
        public static string OneDriveYes => GetValue("OneDriveYes");
    }
}