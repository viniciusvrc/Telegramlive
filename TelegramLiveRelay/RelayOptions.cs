namespace TelegramLiveRelay
{
    internal sealed class RelayOptions
    {
        public RelayOptions(
            string serverUrl,
            string streamKey,
            string youtubeUrl,
            string cookiesFilePath,
            string resolutionOption,
            string sizeOption,
            string audioQualityOption,
            bool repeatEnabled,
            string ffmpegPath,
            string ytDlpPath)
        {
            ServerUrl = serverUrl;
            StreamKey = streamKey;
            YoutubeUrl = youtubeUrl;
            CookiesFilePath = cookiesFilePath;
            ResolutionOption = resolutionOption;
            SizeOption = sizeOption;
            AudioQualityOption = audioQualityOption;
            RepeatEnabled = repeatEnabled;
            FfmpegPath = ffmpegPath;
            YtDlpPath = ytDlpPath;
        }

        public string ServerUrl { get; private set; }

        public string StreamKey { get; private set; }

        public string YoutubeUrl { get; private set; }

        public string CookiesFilePath { get; private set; }

        public string ResolutionOption { get; private set; }

        public string SizeOption { get; private set; }

        public string AudioQualityOption { get; private set; }

        public bool RepeatEnabled { get; private set; }

        public string FfmpegPath { get; private set; }

        public string YtDlpPath { get; private set; }
    }
}
