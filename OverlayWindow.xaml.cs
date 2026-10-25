using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NowPlayingOverlay
{
    public partial class OverlayWindow : Window
    {
        // ---- Timing (edit these) -------------------------------------------------
        private static readonly TimeSpan SlideInTime = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FadeOutTime = TimeSpan.FromMilliseconds(600);
        private const double SlideFromX = -80;

        private int _version; // lets a newer show cancel an older animation's "hide" callback

        public OverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => MakeClickThrough();
        }

        public void ShowTrack(TrackInfo track)
        {
            _version++;
            int myVersion = _version;

            TitleText.Text = track.Title;
            ArtistText.Text = track.Artist;
            Art.Source = LoadArt(track.ArtBytes);

            if (!IsVisible) Show();
            ReassertTopmost();

            // Reset any running animation
            Card.BeginAnimation(OpacityProperty, null);
            Slide.BeginAnimation(TranslateTransform.XProperty, null);

            // Opacity: fade in -> hold -> fade out
            var fadeEnd = SlideInTime + HoldTime + FadeOutTime;
            var opacity = new DoubleAnimationUsingKeyFrames { Duration = fadeEnd };
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(SlideInTime)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(SlideInTime + HoldTime)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(fadeEnd)));
            opacity.Completed += (_, _) =>
            {
                if (myVersion == _version) Hide();
            };

            // Slide in from the left with easing
            var slide = new DoubleAnimation(SlideFromX, 0, SlideInTime)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            Card.BeginAnimation(OpacityProperty, opacity);
            Slide.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private static ImageSource? LoadArt(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = new MemoryStream(bytes);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        // ---- Win32: click-through, never takes focus, hidden from Alt+Tab ----------
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_NOACTIVATE = 0x08000000L;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            style |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        }

        private void ReassertTopmost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
