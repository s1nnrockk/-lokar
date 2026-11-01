# TalkClock MAUI (C# rewrite)

A separate .NET MAUI Android implementation of the clock app. The original Kotlin app remains untouched while this rewrite is developed.

## Build
Install the .NET 10 SDK and MAUI Android workload, then run:

```sh
dotnet workload install maui-android
dotnet publish TalkClock.Maui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

The GitHub Actions workflow builds an APK and publishes it as an artifact.

## Current implementation
- Live local clock and date
- One exact Android alarm with notification
- In-app countdown timer
- Stopwatch with start/pause/reset
- Dark glass-inspired card UI

Alarm delivery depends on Android exact-alarm and notification permissions. This initial C# port is a new implementation; it does not yet reproduce every advanced feature of the original Fossify app (multiple alarms, repeating days, snooze, custom ringtones, widgets, and lap history).
