namespace NowPlayingOverlay
{
    public sealed class TrackInfo
    {
        public string Title { get; init; } = "";
        public string Artist { get; init; } = "";
        public string Album { get; init; } = "";
        public byte[]? ArtBytes { get; init; }

        public string Key => $"{Artist}\u001f{Title}";
    }
}
