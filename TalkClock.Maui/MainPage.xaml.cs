using System.Diagnostics;
using System.Text.Json;
namespace TalkClock;
public partial class MainPage : ContentPage
{
    const string AlarmsKey = "alarms_json";
    const string LapsKey = "stopwatch_laps";
    const string WorldClocksKey = "world_clock_zones";
    readonly List<string> _worldZones = [];
    readonly Dictionary<string, Label> _worldTimeLabels = new(StringComparer.OrdinalIgnoreCase);
    readonly Stopwatch _stopwatch = new();
    readonly List<TimeSpan> _laps = [];
    bool _lapsOldestFirst;
    List<AlarmItem> _alarms = [];
    IDispatcherTimer? _uiTimer;
    DateTime _timerEnd;
    TimeSpan _timerRemaining;
    bool _timerRunning;
    int _lastClockSecond = -1;

    public MainPage()
    {
        InitializeComponent();
        _alarms = LoadAlarms();
        LoadLaps();
        _lapsOldestFirst = Preferences.Get("laps_oldest_first", false);
        LoadWorldClocks();
        RenderWorldClocks();
        RestoreTimer();
        RenderAlarms();
        RenderLaps();
        UpdateLapSortButton();
        GlassSwitch.IsToggled = Preferences.Get("liquid_glass", true);
        HourFormatSwitch.IsToggled = Preferences.Get("use_24_hour", true);
        KeepAwakeSwitch.IsToggled = Preferences.Get("keep_awake", false);
        AlarmVibrateSwitch.IsToggled = Preferences.Get("alarm_vibrate_default", true);
        AlarmGradualSwitch.IsToggled = Preferences.Get("alarm_gradual_default", true);
        DefaultVibrateSwitch.IsToggled = Preferences.Get("alarm_vibrate_default", true);
        DefaultGradualSwitch.IsToggled = Preferences.Get("alarm_gradual_default", true);
        SecondsSwitch.IsToggled = Preferences.Get("show_seconds", true);
        ThemePicker.SelectedIndex = Math.Clamp(Preferences.Get("clokar_theme", 0), 0, 2);
        AccentPicker.SelectedIndex = Math.Clamp(Preferences.Get("clokar_accent", 0), 0, 5);
        SnoozePicker.SelectedIndex = Math.Clamp(Preferences.Get("alarm_snooze_minutes", 5) switch { 10 => 1, 15 => 2, 20 => 3, _ => 0 }, 0, 3);
        TextScalePicker.SelectedIndex = Math.Clamp(Preferences.Get("accessibility_text_scale", 0), 0, 2);
        HighContrastSwitch.IsToggled = Preferences.Get("accessibility_high_contrast", false);
        ReduceMotionSwitch.IsToggled = Preferences.Get("accessibility_reduce_motion", false);
        HapticsSwitch.IsToggled = Preferences.Get("accessibility_haptics", true);
        ApplyKeepAwake();
        ApplyGlass();
        ApplyTheme();
        ApplyAccent();
        ApplyAccessibility();
        ShowTab(0);
        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        _uiTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Second != _lastClockSecond)
            {
                _lastClockSecond = now.Second;
                ClockLabel.Text = now.ToString(ClockFormat());
                DateLabel.Text = now.ToString("dddd, MMMM d");
                UpdateWorldClockLabels(now);
            }
            if (_stopwatch.IsRunning) StopwatchLabel.Text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.ff");
            if (_timerRunning)
            {
                _timerRemaining = _timerEnd - now;
                if (_timerRemaining <= TimeSpan.Zero)
                {
                    _timerRunning = false;
                    _timerRemaining = TimeSpan.Zero;
                    TimerStatus.Text = "00:00:00";
                    TimerButton.Text = "Start timer";
                    Preferences.Remove("timer_end_utc");
#if ANDROID
                    AndroidAlarm.CancelTimer();
#endif
                    MainThread.BeginInvokeOnMainThread(async () => await DisplayAlert("Timer", "Your timer has finished.", "OK"));
                }
                else TimerStatus.Text = _timerRemaining.ToString(@"hh\:mm\:ss");
            }
        };
        _uiTimer.Start();
    }

    void LoadWorldClocks()
    {
        try
        {
            _worldZones.AddRange(JsonSerializer.Deserialize<List<string>>(Preferences.Get(WorldClocksKey, "[\"Europe/London\"]")) ?? []);
        }
        catch { _worldZones.Add("Europe/London"); }
        for (var i = _worldZones.Count - 1; i >= 0; i--)
            if (!TryGetTimeZone(_worldZones[i], out _)) _worldZones.RemoveAt(i);
    }

    static bool TryGetTimeZone(string id, out TimeZoneInfo zone)
    {
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { zone = TimeZoneInfo.Utc; return false; }
    }

    void SaveWorldClocks() => Preferences.Set(WorldClocksKey, JsonSerializer.Serialize(_worldZones));

    async void OnAddWorldClockClicked(object sender, EventArgs e)
    {
        var id = WorldZoneEntry.Text?.Trim() ?? "";
        if (!TryGetTimeZone(id, out _))
        {
            await DisplayAlert("Unknown time zone", "Enter a valid IANA time-zone ID, for example Asia/Tokyo.", "OK");
            return;
        }
        if (_worldZones.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            await DisplayAlert("Already added", "That time zone is already on your list.", "OK");
            return;
        }
        _worldZones.Add(id);
        SaveWorldClocks();
        RenderWorldClocks();
        WorldZoneEntry.Text = "";
    }

    void RenderWorldClocks()
    {
        WorldClockList.Clear();
        _worldTimeLabels.Clear();
        foreach (var id in _worldZones)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(8, 6) };
            row.Add(new Label { Text = id, TextColor = Color.FromArgb("#C9DCEB"), FontSize = 12, VerticalOptions = LayoutOptions.Center }, 0);
            var time = new Label { Text = "--:--", TextColor = Colors.White, FontSize = 18, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
            row.Add(time, 1);
            _worldTimeLabels[id] = time;
            var remove = new Button { Text = "×", AutomationProperties = { Name = $"Remove world clock {id}" }, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#263B50"), Padding = new Thickness(10, 2), VerticalOptions = LayoutOptions.Center };
            remove.Clicked += (_, _) =>
            {
                _worldZones.Remove(id);
                SaveWorldClocks();
                RenderWorldClocks();
            };
            row.Add(remove, 2);
            WorldClockList.Children.Add(new Border { Stroke = Color.FromArgb("#2B4359"), StrokeShape = new RoundRectangle(14), Padding = 2, Content = row });
        }
        if (_worldZones.Count == 0)
            WorldClockList.Children.Add(new Label { Text = "Add a city by entering its time-zone ID above.", TextColor = Color.FromArgb("#829AAF"), FontSize = 12 });
        UpdateWorldClockLabels(DateTime.Now);
    }

    void UpdateWorldClockLabels(DateTime localNow)
    {
        foreach (var id in _worldZones)
            if (_worldTimeLabels.TryGetValue(id, out var label) && TryGetTimeZone(id, out var zone))
                label.Text = TimeZoneInfo.ConvertTime(localNow, zone).ToString(ClockFormat());
    }

    void ShowTab(int tab)
    {
        WorldClockSection.IsVisible = tab == 0;
        AlarmSection.IsVisible = tab == 1;
        TimerSection.IsVisible = tab == 2;
        StopwatchSection.IsVisible = tab == 3;
        SettingsSection.IsVisible = tab == 4;
        // Keep the clock header visible as a compact home anchor on every tab.
        for (var i = 0; i < BottomNav.Children.Count; i++)
            if (BottomNav.Children[i] is Button b)
            {
                b.BackgroundColor = i == tab ? Color.FromArgb("#1B3044") : Colors.Transparent;
                var accent = Preferences.Get("clokar_accent", 0) switch
                {
                    1 => "#B9A2FF", 2 => "#8DE6C0", 3 => "#FF9DB5",
                    4 => "#FFC978", 5 => "#9BE7F2", _ => "#A7DFFF"
                };
                b.TextColor = i == tab ? Color.FromArgb(accent) : Color.FromArgb("#91A8BC");
            }
        if (tab == 0) { AlarmSection.IsVisible = false; TimerSection.IsVisible = false; StopwatchSection.IsVisible = false; }
    }
    void OnClockTabClicked(object sender, EventArgs e) => ShowTab(0);
    void OnAlarmTabClicked(object sender, EventArgs e) => ShowTab(1);
    void OnTimerTabClicked(object sender, EventArgs e) => ShowTab(2);
    void OnStopwatchTabClicked(object sender, EventArgs e) => ShowTab(3);
    void OnSettingsTabClicked(object sender, EventArgs e) => ShowTab(4);

    void OnGlassToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("liquid_glass", e.Value);
        ApplyGlass();
    }
    void OnHourFormatToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("use_24_hour", e.Value);
        _lastClockSecond = -1;
        UpdateWorldClockLabels(DateTime.Now);
    }
    string ClockFormat()
    {
        var use24 = Preferences.Get("use_24_hour", true);
        var seconds = Preferences.Get("show_seconds", true);
        return (use24 ? "HH:mm" : "h:mm") + (seconds ? ":ss" : "") + (use24 ? "" : " tt");
    }
    void OnSecondsToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("show_seconds", e.Value);
        _lastClockSecond = -1;
        ClockLabel.Text = DateTime.Now.ToString(ClockFormat());
        UpdateWorldClockLabels(DateTime.Now);
    }
    void OnDefaultVibrateToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("alarm_vibrate_default", e.Value);
        if (AlarmVibrateSwitch is not null) AlarmVibrateSwitch.IsToggled = e.Value;
    }
    void OnDefaultGradualToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("alarm_gradual_default", e.Value);
        if (AlarmGradualSwitch is not null) AlarmGradualSwitch.IsToggled = e.Value;
    }
    void OnSnoozeChanged(object sender, EventArgs e)
    {
        var minutes = SnoozePicker.SelectedIndex switch { 1 => 10, 2 => 15, 3 => 20, _ => 5 };
        Preferences.Set("alarm_snooze_minutes", minutes);
    }
    void OnThemeChanged(object sender, EventArgs e)
    {
        if (ThemePicker.SelectedIndex < 0) return;
        Preferences.Set("clokar_theme", ThemePicker.SelectedIndex);
        ApplyTheme();
    }
    void OnAccentChanged(object sender, EventArgs e)
    {
        if (AccentPicker.SelectedIndex < 0) return;
        Preferences.Set("clokar_accent", AccentPicker.SelectedIndex);
        ApplyAccent();
    }
    void ApplyTheme()
    {
        var theme = Preferences.Get("clokar_theme", 0);
        var page = theme switch { 1 => "#050812", 2 => "#101114", _ => "#070D18" };
        BackgroundColor = Color.FromArgb(page);
        ApplyGlass();
    }
    void ApplyAccent()
    {
        var accent = Preferences.Get("clokar_accent", 0) switch
        {
            1 => "#B9A2FF", 2 => "#8DE6C0", 3 => "#FF9DB5",
            4 => "#FFC978", 5 => "#9BE7F2", _ => "#A7DFFF"
        };
        var color = Color.FromArgb(accent);
        ClockLabel.TextColor = color;
        CreateAlarmButton.BackgroundColor = color;
        TimerButton.BackgroundColor = color;
        StopwatchButton.BackgroundColor = color;
        foreach (var control in new Switch[] { GlassSwitch, HourFormatSwitch, SecondsSwitch, DefaultVibrateSwitch, DefaultGradualSwitch, KeepAwakeSwitch, AlarmVibrateSwitch, AlarmGradualSwitch })
            control.OnColor = color;
        foreach (var control in new CheckBox[] { Day0, Day1, Day2, Day3, Day4, Day5, Day6 })
            control.Color = color;
        foreach (var child in BottomNav.Children)
            if (child is Button button && button.Text.Contains("Clock", StringComparison.Ordinal))
                button.TextColor = color;
    }
    void OnTextScaleChanged(object sender, EventArgs e)
    {
        if (TextScalePicker.SelectedIndex < 0) return;
        Preferences.Set("accessibility_text_scale", TextScalePicker.SelectedIndex);
        ApplyAccessibility();
    }
    void OnHighContrastToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("accessibility_high_contrast", e.Value);
        ApplyAccessibility();
    }
    void OnReduceMotionToggled(object sender, ToggledEventArgs e)
        => Preferences.Set("accessibility_reduce_motion", e.Value);
    void OnHapticsToggled(object sender, ToggledEventArgs e)
        => Preferences.Set("accessibility_haptics", e.Value);

    void ApplyAccessibility()
    {
        var scale = Preferences.Get("accessibility_text_scale", 0) switch { 1 => 1.18, 2 => 1.35, _ => 1.0 };
        ClockLabel.FontSize = 48 * scale;
        DateLabel.FontSize = 13 * scale;
        AlarmStatus.FontSize = 12 * scale;
        TimerStatus.FontSize = 38 * scale;
        StopwatchLabel.FontSize = 36 * scale;
        CreateAlarmButton.FontSize = 14 * scale;
        TimerButton.FontSize = 14 * scale;
        StopwatchButton.FontSize = 14 * scale;
        foreach (var child in BottomNav.Children)
            if (child is Button button) button.FontSize = 10 * scale;

        var contrast = Preferences.Get("accessibility_high_contrast", false);
        var primary = Color.FromArgb(contrast ? "#FFFFFF" : "#F2F7FC");
        var secondary = Color.FromArgb(contrast ? "#E0ECF7" : "#B5C9D9");
        var outline = Color.FromArgb(contrast ? "#91B9D8" : "#2B4359");
        ClockLabel.TextColor = contrast ? Colors.White : Color.FromArgb(Preferences.Get("clokar_accent", 0) switch
        {
            1 => "#B9A2FF", 2 => "#8DE6C0", 3 => "#FF9DB5",
            4 => "#FFC978", 5 => "#9BE7F2", _ => "#A7DFFF"
        });
        DateLabel.TextColor = secondary;
        TimerStatus.TextColor = primary;
        StopwatchLabel.TextColor = primary;
        AlarmStatus.TextColor = secondary;
        foreach (var border in new[] { WorldClockSection, AlarmSection, TimerSection, StopwatchSection, SettingsSection })
            border.Stroke = contrast ? outline : Color.FromArgb(Preferences.Get("liquid_glass", true) ? "#54758F" : "#2B4359");
    }

    void PerformTouchFeedback()
    {
        if (!Preferences.Get("accessibility_haptics", true)) return;
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }

    void OnKeepAwakeToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("keep_awake", e.Value);
        ApplyKeepAwake();
    }
    void ApplyKeepAwake() => DeviceDisplay.KeepScreenOn = Preferences.Get("keep_awake", false);
    void ApplyGlass()
    {
        var glass = Preferences.Get("liquid_glass", true);
        foreach (var border in new[] { WorldClockSection, AlarmSection, TimerSection, StopwatchSection, SettingsSection })
        {
            border.Stroke = glass ? Color.FromArgb("#54758F") : Color.FromArgb("#2B4359");
            var theme = Preferences.Get("clokar_theme", 0);
            var top = theme switch { 1 => "#18243A", 2 => "#292A30", _ => "#1B3044" };
            var bottom = theme switch { 1 => "#0D1422", 2 => "#17181D", _ => "#0D1927" };
            border.Background = glass
                ? new LinearGradientBrush(new GradientStopCollection { new GradientStop(Color.FromArgb(top), 0), new GradientStop(Color.FromArgb(bottom), 1) }, new Point(0,0), new Point(1,1))
                : new SolidColorBrush(Color.FromArgb(theme switch { 1 => "#0D1422", 2 => "#1A1B20", _ => "#101D2B" }));
        }
    }

    List<AlarmItem> LoadAlarms()
    {
        try { return JsonSerializer.Deserialize<List<AlarmItem>>(Preferences.Get(AlarmsKey, "[]")) ?? []; }
        catch { return []; }
    }
    void SaveAlarms() => Preferences.Set(AlarmsKey, JsonSerializer.Serialize(_alarms));

    async void OnSetAlarmClicked(object sender, EventArgs e)
    {
        PerformTouchFeedback();
        Preferences.Set("alarm_vibrate_default", AlarmVibrateSwitch.IsToggled);
        Preferences.Set("alarm_gradual_default", AlarmGradualSwitch.IsToggled);
        DefaultVibrateSwitch.IsToggled = AlarmVibrateSwitch.IsToggled;
        DefaultGradualSwitch.IsToggled = AlarmGradualSwitch.IsToggled;
        var days = new[] { Day0, Day1, Day2, Day3, Day4, Day5, Day6 }
            .Select((c, i) => (c.IsChecked, i)).Where(x => x.IsChecked).Select(x => x.i).ToList();
        var alarm = new AlarmItem
        {
            Id = _alarms.Count == 0 ? 1 : _alarms.Max(a => a.Id) + 1,
            Time = AlarmTimePicker.Time.ToString(@"hh\:mm"),
            Label = string.IsNullOrWhiteSpace(AlarmLabelEntry.Text) ? "Alarm" : AlarmLabelEntry.Text.Trim(),
            Days = days,
            Vibrate = AlarmVibrateSwitch.IsToggled,
            GradualVolume = AlarmGradualSwitch.IsToggled
        };
#if ANDROID
        try
        {
            AndroidAlarm.Schedule(alarm);
            _alarms.Add(alarm);
            SaveAlarms();
            RenderAlarms();
            AlarmStatus.Text = $"Next alarm: {AndroidAlarm.NextOccurrence(alarm):ddd, HH:mm}";
            foreach (var c in new[] { Day0, Day1, Day2, Day3, Day4, Day5, Day6 }) c.IsChecked = false;
        }
        catch (Exception ex) { await DisplayAlert("Alarm unavailable", ex.Message, "OK"); }
#else
        _alarms.Add(alarm); SaveAlarms(); RenderAlarms();
        await DisplayAlert("Alarm", "Alarm saved. Android is required to schedule system alarms.", "OK");
#endif
    }

    void RenderAlarms()
    {
        AlarmList.Clear();
        foreach (var alarm in _alarms.OrderBy(a => a.Time))
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, Padding = 10 };
            var text = new VerticalStackLayout { Spacing = 2 };
            text.Children.Add(new Label { Text = alarm.Time, FontSize = 24, TextColor = Colors.White, FontAttributes = FontAttributes.Bold });
            text.Children.Add(new Label { Text = $"{alarm.Label} • {alarm.RepeatText} • {(alarm.Vibrate ? "Vibration" : "Silent")} • {(alarm.GradualVolume ? "Soft start" : "Full volume")}", FontSize = 11, TextColor = Color.FromArgb("#91A8C0") });
            row.Add(text, 0);
            var enabled = new Switch { IsToggled = alarm.Enabled, AutomationProperties = { Name = $"Enable alarm {alarm.Label}" }, OnColor = Color.FromArgb("#A7DFFF"), VerticalOptions = LayoutOptions.Center };
            enabled.Toggled += (_, args) =>
            {
                alarm.Enabled = args.Value;
#if ANDROID
                if (args.Value) AndroidAlarm.Schedule(alarm); else AndroidAlarm.Cancel(alarm.Id);
#endif
                SaveAlarms();
            };
            row.Add(enabled, 1);
            var delete = new Button { Text = "×", AutomationProperties = { Name = $"Delete alarm {alarm.Label}" }, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#263B50"), Padding = new Thickness(12, 4), VerticalOptions = LayoutOptions.Center };
            delete.Clicked += (_, _) =>
            {
#if ANDROID
                AndroidAlarm.Cancel(alarm.Id);
#endif
                _alarms.Remove(alarm); SaveAlarms(); RenderAlarms();
            };
            row.Add(delete, 2);
            AlarmList.Children.Add(new Border { Stroke = Color.FromArgb("#2B4359"), StrokeShape = new RoundRectangle(16), Padding = 2, Content = row });
        }
        if (_alarms.Count == 0) AlarmList.Children.Add(new Label { Text = "No alarms yet", TextColor = Color.FromArgb("#91A8C0"), FontSize = 12 });
    }

    void RestoreTimer()
    {
        var raw = Preferences.Get("timer_end_utc", "");
        if (long.TryParse(raw, out var ticks))
        {
            var end = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            _timerRemaining = end - DateTime.Now;
            if (_timerRemaining > TimeSpan.Zero)
            {
                _timerEnd = end; _timerRunning = true; TimerButton.Text = "Pause";
                TimerStatus.Text = _timerRemaining.ToString(@"hh\:mm\:ss");
            }
            else Preferences.Remove("timer_end_utc");
        }
    }

    void OnTimerPresetClicked(object sender, EventArgs e)
    {
        if (sender is not Button button) return;
        var value = button.Text.TrimEnd('m');
        if (!int.TryParse(value, out var minutes)) return;
        TimerMinutes.Text = minutes.ToString();
        TimerSeconds.Text = "0";
        if (!_timerRunning)
        {
            _timerRemaining = TimeSpan.FromMinutes(minutes);
            TimerStatus.Text = _timerRemaining.ToString(@"hh\:mm\:ss");
            TimerButton.Text = "Start timer";
        }
    }

    void OnTimerClicked(object sender, EventArgs e)
    {
        PerformTouchFeedback();
        if (_timerRunning)
        {
            _timerRemaining = _timerEnd - DateTime.Now;
            _timerRunning = false;
            TimerButton.Text = "Resume";
            Preferences.Remove("timer_end_utc");
#if ANDROID
            AndroidAlarm.CancelTimer();
#endif
            return;
        }
        if (TimerButton.Text != "Resume")
        {
            int.TryParse(TimerMinutes.Text, out var mins);
            int.TryParse(TimerSeconds.Text, out var secs);
            _timerRemaining = TimeSpan.FromMinutes(Math.Clamp(mins, 0, 999)).Add(TimeSpan.FromSeconds(Math.Clamp(secs, 0, 59)));
            if (_timerRemaining <= TimeSpan.Zero) { TimerStatus.Text = "Enter a duration"; return; }
        }
        _timerEnd = DateTime.Now.Add(_timerRemaining);
        _timerRunning = true;
        TimerButton.Text = "Pause";
        Preferences.Set("timer_end_utc", _timerEnd.ToUniversalTime().Ticks.ToString());
#if ANDROID
        try { AndroidAlarm.ScheduleTimer(new DateTimeOffset(_timerEnd).ToUnixTimeMilliseconds()); }
        catch (Exception ex) { MainThread.BeginInvokeOnMainThread(async () => await DisplayAlert("Timer scheduling", ex.Message, "OK")); }
#endif
    }

    void OnStopwatchClicked(object sender, EventArgs e)
    {
        PerformTouchFeedback();
        if (_stopwatch.IsRunning) { _stopwatch.Stop(); StopwatchButton.Text = "Resume"; }
        else { _stopwatch.Start(); StopwatchButton.Text = "Pause"; }
    }
    void OnStopwatchResetClicked(object sender, EventArgs e)
    {
        PerformTouchFeedback();
        _stopwatch.Reset(); _laps.Clear(); SaveLaps(); RenderLaps();
        StopwatchLabel.Text = "00:00:00.00"; StopwatchButton.Text = "Start";
    }
    void OnLapClicked(object sender, EventArgs e)
    {
        PerformTouchFeedback();
        if (!_stopwatch.IsRunning) return;
        _laps.Add(_stopwatch.Elapsed); SaveLaps(); RenderLaps();
    }
    void LoadLaps()
    {
        try { _laps.AddRange(JsonSerializer.Deserialize<List<TimeSpan>>(Preferences.Get(LapsKey, "[]")) ?? []); } catch { }
    }
    void SaveLaps() => Preferences.Set(LapsKey, JsonSerializer.Serialize(_laps));
    void OnLapSortClicked(object sender, EventArgs e)
    {
        _lapsOldestFirst = !_lapsOldestFirst;
        Preferences.Set("laps_oldest_first", _lapsOldestFirst);
        UpdateLapSortButton();
        RenderLaps();
    }

    void UpdateLapSortButton() => LapSortButton.Text = _lapsOldestFirst ? "Oldest first ↑" : "Newest first ↓";

    void RenderLaps()
    {
        LapList.Clear();
        var indices = _lapsOldestFirst ? Enumerable.Range(0, _laps.Count) : Enumerable.Range(0, _laps.Count).Reverse();
        foreach (var i in indices)
        {
            var previous = i == 0 ? TimeSpan.Zero : _laps[i - 1];
            var split = _laps[i] - previous;
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(10, 7) };
            row.Add(new Label { Text = $"LAP {i + 1}", TextColor = Color.FromArgb("#91A8C0"), FontSize = 11, VerticalOptions = LayoutOptions.Center }, 0);
            row.Add(new Label { Text = $"Split {split:mm\\:ss\\.ff}  ·  Total {_laps[i]:hh\\:mm\\:ss\\.ff}", TextColor = Colors.White, FontSize = 12, VerticalOptions = LayoutOptions.Center }, 1);
            LapList.Children.Add(new Border { Stroke = Color.FromArgb("#2B4359"), StrokeShape = new RoundRectangle(12), Padding = 2, Content = row });
        }
        if (_laps.Count == 0) LapList.Children.Add(new Label { Text = "Your lap times will appear here.", TextColor = Color.FromArgb("#829AAF"), FontSize = 12 });
    }
}
