# Clokar

**Clokar** is an offline-first clock app built with .NET MAUI for Android. This branch contains the C# rewrite and its Liquid Glass-inspired interface.

## Features

- Live local clock with 12/24-hour formatting
- Multiple world clocks using IANA time-zone IDs
- Alarms with labels, repeat-day selection, enable/disable, and delete controls
- Per-alarm vibration toggle and a 30-second gradual alarm-volume ramp
- Animated full-screen alarm screen with live time, pulsing glass rings, Snooze, and Dismiss controls
- Alarm playback continues through a foreground service and can be dismissed or snoozed from notifications
- Timer with quick presets, custom minutes/seconds, pause/resume, and Android scheduling
- Stopwatch with persistent lap history, split-time display, and newest-first / oldest-first sorting
- Liquid Glass appearance toggle
- Keep-screen-awake setting
- Android home-screen clock widget with translucent glass-style background, live time/date, resizing, and tap-to-open behavior
- Local preferences; no account or cloud service required

## Android home-screen widget

1. Install the Clokar Android build.
2. Long-press an empty area of your Android home screen.
3. Open **Widgets**, find **Clokar**, and drag the clock widget onto the home screen.
4. Resize it using the launcher's widget handles if desired.

The widget uses Android's TextClock so the displayed time updates through the system rather than relying on frequent app wakeups.

## Build

Open TalkClock.Maui/TalkClock.Maui.csproj with a compatible .NET 10 SDK and .NET MAUI Android workload, then build the net10.0-android target.

```bash
dotnet workload install maui-android
dotnet build TalkClock.Maui/TalkClock.Maui.csproj -f net10.0-android
```

## Project status

This is an active C# port. The original Android implementation remains in the repository for reference while feature parity and device behavior are being completed. Source updates do not by themselves mean an APK has been built or tested on a device.
