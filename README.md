# ATS Now Playing

A tiny overlay for American Truck Simulator (or any borderless-windowed game).
When Spotify starts a new song, a dark translucent card slides in at the top-left
of your primary monitor with the album art, title and artist, stays for 10 seconds,
then fades out. It never takes focus and clicks pass straight through to the game.

## Requirements
- Windows 10 (build 19041+) or Windows 11
- Spotify **desktop app**
- Game set to **Borderless Windowed** (overlays can't draw over exclusive fullscreen)
- To build: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build
```
dotnet build -c Release
```
Output: `bin\Release\net8.0-windows10.0.19041.0\ATSNowPlaying.exe`

Single-file publish (smaller, needs the .NET 8 Desktop Runtime installed):
```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
Fully self-contained (bigger, nothing to install, good for sharing):
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Using it
1. Launch `ATSNowPlaying.exe` when you start playing. A tray icon appears and the
   current song is shown once so you know it's working.
2. It announces each new song automatically. Ads are skipped.
3. Launching the exe again (e.g. pressing the same Stream Deck key) does **not** start
   a second copy. It just replays the card in the running one.
4. Right-click the tray icon -> **Quit** when you're done.

## Stream Deck
Add a **System -> Open** action, point it at `ATSNowPlaying.exe`.
- Not running: the key launches it.
- Already running: the key replays the card on demand.

## Icon
`icon.ico` is used for the exe (Explorer, Stream Deck) and the tray. Swap the file and rebuild to change it.

## Tweaks
In `OverlayWindow.xaml.cs`: `HoldTime` (10 s), `SlideInTime`, `FadeOutTime`, `SlideFromX`.
In `OverlayWindow.xaml`: card position (`Margin`), width, colors, fonts.
In `MediaWatcher.cs`: `DebounceMs` (how long to wait before announcing after rapid skips).
