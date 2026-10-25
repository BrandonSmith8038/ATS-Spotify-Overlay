using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace NowPlayingOverlay
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "ATSNowPlaying_SingleInstance";
        private const string ShowEventName = "ATSNowPlaying_ShowNow";

        private Mutex? _mutex;
        private EventWaitHandle? _showEvent;
        private Forms.NotifyIcon? _tray;
        private OverlayWindow? _overlay;
        private MediaWatcher? _watcher;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ---- Single instance -------------------------------------------------
            // A second launch (e.g. pressing the Stream Deck key again) doesn't start
            // another copy. It just tells the running copy to replay the card, then exits.
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
                Shutdown();
                return;
            }

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            var listener = new Thread(() =>
            {
                while (_showEvent.WaitOne())
                    Dispatcher.BeginInvoke(new Action(async () => await ShowCurrentAsync()));
            })
            { IsBackground = true, Name = "ShowNowListener" };
            listener.Start();

            // ---- Overlay + tray --------------------------------------------------
            _overlay = new OverlayWindow();
            CreateTrayIcon();

            _watcher = new MediaWatcher();
            _watcher.TrackChanged += track =>
                Dispatcher.BeginInvoke(new Action(() => _overlay?.ShowTrack(track)));

            await _watcher.StartAsync();

            // Show whatever is playing right now so you can see it's working.
            await ShowCurrentAsync();
        }

        private async System.Threading.Tasks.Task ShowCurrentAsync()
        {
            if (_watcher == null || _overlay == null) return;
            var track = await _watcher.GetCurrentAsync();
            if (track != null) _overlay.ShowTrack(track);
        }

        private void CreateTrayIcon()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show now playing", null, async (_, _) => await ShowCurrentAsync());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => Shutdown());

            _tray = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "ATS Now Playing",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += async (_, _) => await ShowCurrentAsync();
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
                if (stream != null)
                    return new Icon(stream, Forms.SystemInformation.SmallIconSize); // picks the sharpest size for your DPI
            }
            catch { }
            return SystemIcons.Application;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            _watcher?.Dispose();
            _showEvent?.Dispose();
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
