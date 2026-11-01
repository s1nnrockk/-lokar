# Clokar (C#/.NET MAUI rewrite)

A separate C#/.NET MAUI Android implementation. The original Kotlin app remains unchanged.

## Implemented in this port
- Live local clock and date, plus a persistent list of world clocks using IANA time-zone IDs
- Quick timer presets (1, 5, 10, 25, and 60 minutes)
- Multiple alarms with custom labels, enabled/disabled state, selected repeat weekdays, and one-shot scheduling
- Android exact alarm delivery, high-priority notification, five-minute snooze action, and restoration of repeating alarms after reboot
- Countdown timer with pause/resume, saved remaining deadline, and Android background completion notification
- Stopwatch with start/pause/reset and saved lap history
- Native Android resizable home-screen clock widget with a translucent Liquid Glass gradient, live digital time/date, and tap-to-open behavior
- Dark glass-inspired layout

## Add the widget

Install the APK, then long-press an empty area of the Android home screen, open **Widgets**, find **Clokar**, and drag the clock widget onto the home screen. Resize it with the launcher handles. Tap the widget to open Clokar.

## Build
Install the .NET 10 SDK and MAUI Android workload:

```sh
dotnet workload install maui-android
dotnet publish Clokar.Maui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

GitHub Actions builds an APK artifact from this project.

## Not yet ported from Fossify Clock
This is not feature parity with the original app. In particular, custom alarm/ringtone selection, gradual volume increase, vibration preferences, dismiss/full-screen ringing UX, multiple simultaneous timers, advanced analog/digital widget customization, full appearance/settings screens, and every original accessibility/localization option remain to be implemented. Android exact-alarm and notification access must be allowed by the user for reliable background delivery.
