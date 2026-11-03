# Clokar (C#/.NET MAUI rewrite)

A separate C#/.NET MAUI Android implementation. The original Kotlin app remains unchanged.

## Implemented in this port
- Live local clock and date, plus a persistent list of world clocks using IANA time-zone IDs
- Quick timer presets (1, 5, 10, 25, and 60 minutes)
- Multiple alarms with custom labels, enabled/disabled state, selected repeat weekdays, and one-shot scheduling
- Android exact alarm delivery, high-priority notification, configurable snooze action, and restoration of repeating alarms after reboot
- Countdown timer with pause/resume, saved remaining deadline, and Android background completion notification
- Stopwatch with start/pause/reset and saved lap history
- Native Android resizable home-screen clock widget with a translucent Liquid Glass gradient, live digital time/date, and tap-to-open behavior
- Redesigned Clokar interface with glass-style cards, compact bottom navigation and full settings screen
- Appearance settings: Liquid Glass toggle, three dark themes and six accent colors, saved locally
- Clock settings: 12/24-hour format and optional seconds on local and world clocks
- Alarm defaults: vibration, gradual volume and configurable 5/10/15/20-minute snooze
- Accessibility settings: adjustable text scale, high-contrast colors, reduced motion on the alarm screen, and optional haptic touch feedback
- Screen-reader names for bottom navigation, alarm controls, and world-clock removal actions
- Device setting to keep the screen awake

## Clokar AI

The floating AI panel uses the hosted LLM7.io OpenAI-compatible endpoint with model `gpt-oss:20b`. It is not local and does not require a key in the app. Prompts are sent to the provider; avoid entering personal or sensitive information. The provider's free quota and availability can change.

The assistant can read the current device time locally and start the existing timer from natural-language requests. General questions are sent to the hosted model.

## Add the widget

Install the APK, then long-press an empty area of the Android home screen, open **Widgets**, find **Clokar**, and drag the clock widget onto the home screen. Resize it with the launcher handles. Tap the widget to open Clokar.

## Build
Install the .NET 10 SDK and MAUI Android workload:

```sh
dotnet workload install maui-android
dotnet publish Clokar.Maui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

GitHub Actions builds an APK artifact from this project.

## Current limitations
Custom ringtone selection, multiple simultaneous timers, advanced widget layout customization, and full localization parity and system-wide dynamic type integration are not included yet. Android exact-alarm and notification access must be allowed by the user for reliable background delivery.
