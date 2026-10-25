using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace NowPlayingOverlay
{
    /// <summary>
    /// Watches the Windows media session (the same data behind the volume-key popup)
    /// and raises TrackChanged when Spotify starts a new song.
    /// </summary>
    public sealed class MediaWatcher : IDisposable
    {
        private const int DebounceMs = 700;

        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _session;
        private CancellationTokenSource? _debounce;
        private string? _lastAnnouncedKey;

        public event Action<TrackInfo>? TrackChanged;

        public async Task StartAsync()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += (_, _) => AttachSession();
            AttachSession();

            // Treat whatever is already playing as "already announced" for auto-detection;
            // App shows it once explicitly at startup.
            var current = await GetCurrentAsync();
            _lastAnnouncedKey = current?.Key;
        }

        /// <summary>Reads the current Spotify track (regardless of play state). Null if none or an ad.</summary>
        public async Task<TrackInfo?> GetCurrentAsync()
        {
            var session = _session;
            if (session == null) return null;
            return await ReadAsync(session);
        }

        private void AttachSession()
        {
            if (_manager == null) return;

            var spotify = _manager.GetSessions().FirstOrDefault(s =>
                s.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));

            if (ReferenceEquals(spotify, _session)) return;

            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnChanged;
                _session.PlaybackInfoChanged -= OnChanged;
            }

            _session = spotify;

            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnChanged;
                _session.PlaybackInfoChanged += OnChanged;
                ScheduleCheck();
            }
        }

        private void OnChanged(GlobalSystemMediaTransportControlsSession sender, object args) => ScheduleCheck();

        // Spotify fires several events per track change (and again when art loads),
        // and skipping quickly fires a burst. Wait for things to settle before checking.
        private void ScheduleCheck()
        {
            _debounce?.Cancel();
            var cts = new CancellationTokenSource();
            _debounce = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMs, cts.Token);
                    await CheckAsync();
                }
                catch (OperationCanceledException) { }
                catch { /* never crash the watcher */ }
            });
        }

        private async Task CheckAsync()
        {
            var session = _session;
            if (session == null) return;

            // Only announce while actually playing. If a new song is queued while paused,
            // it gets announced when playback resumes (PlaybackInfoChanged re-triggers this).
            var status = session.GetPlaybackInfo()?.PlaybackStatus;
            if (status != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) return;

            var track = await ReadAsync(session);
            if (track == null) return; // ad or nothing usable: skip showing

            if (track.Key == _lastAnnouncedKey) return;
            _lastAnnouncedKey = track.Key;

            TrackChanged?.Invoke(track);
        }

        private static async Task<TrackInfo?> ReadAsync(GlobalSystemMediaTransportControlsSession session)
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (props == null) return null;

            string title = props.Title?.Trim() ?? "";
            string artist = props.Artist?.Trim() ?? "";

            if (IsAd(title, artist)) return null;

            byte[]? art = null;
            try
            {
                if (props.Thumbnail != null)
                {
                    using var raStream = await props.Thumbnail.OpenReadAsync();
                    using var stream = raStream.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    if (ms.Length > 0) art = ms.ToArray();
                }
            }
            catch { /* art is optional */ }

            return new TrackInfo
            {
                Title = title,
                Artist = artist,
                Album = props.AlbumTitle?.Trim() ?? "",
                ArtBytes = art
            };
        }

        // Heuristic: Spotify reports ads with a generic title and no real artist.
        // (Premium shouldn't ever hit this, but it's here as a safety net.)
        private static bool IsAd(string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            if (title.Equals("Advertisement", StringComparison.OrdinalIgnoreCase)) return true;
            if (title.Equals("Spotify", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(artist)) return true;
            return false;
        }

        public void Dispose()
        {
            _debounce?.Cancel();
            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnChanged;
                _session.PlaybackInfoChanged -= OnChanged;
            }
        }
    }
}
