using System.Diagnostics;
using System.Text.Json;
namespace TalkClock;
public partial class MainPage : ContentPage
{
    const string AlarmsKey = "alarms_json";
    const string LapsKey = "stopwatch_laps";
    readonly Stopwatch _stopwatch = new();
    readonly List<TimeSpan> _laps = [];
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
        RestoreTimer();
        RenderAlarms();
        RenderLaps();
        GlassSwitch.IsToggled = Preferences.Get("liquid_glass", true);
        HourFormatSwitch.IsToggled = Preferences.Get("use_24_hour", true);
        KeepAwakeSwitch.IsToggled = Preferences.Get("keep_awake", false);
        ApplyKeepAwake();
        ApplyGlass();
        ShowTab(0);
        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        _uiTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Second != _lastClockSecond)
            {
                _lastClockSecond = now.Second;
                ClockLabel.Text = now.ToString("HH:mm:ss");
                DateLabel.Text = now.ToString("dddd, MMMM d");
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

    void ShowTab(int tab)
    {
        AlarmSection.IsVisible = tab == 1;
        TimerSection.IsVisible = tab == 2;
        StopwatchSection.IsVisible = tab == 3;
        SettingsSection.IsVisible = tab == 4;
        // Keep the clock header visible as a compact home anchor on every tab.
        for (var i = 0; i < BottomNav.Children.Count; i++)
            if (BottomNav.Children[i] is Button b)
            {
                b.BackgroundColor = i == tab ? Color.FromArgb("#1B3044") : Colors.Transparent;
                b.TextColor = i == tab ? Color.FromArgb("#A7DFFF") : Color.FromArgb("#91A8BC");
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
        foreach (var border in new[] { AlarmSection, TimerSection, StopwatchSection, SettingsSection })
        {
            border.Stroke = glass ? Color.FromArgb("#54758F") : Color.FromArgb("#2B4359");
            border.Background = glass ? new LinearGradientBrush(new GradientStopCollection { new GradientStop(Color.FromArgb("#1B3044"), 0), new GradientStop(Color.FromArgb("#0D1927"), 1) }, new Point(0,0), new Point(1,1)) : new SolidColorBrush(Color.FromArgb("#101D2B"));
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
        var days = new[] { Day0, Day1, Day2, Day3, Day4, Day5, Day6 }
            .Select((c, i) => (c.IsChecked, i)).Where(x => x.IsChecked).Select(x => x.i).ToList();
        var alarm = new AlarmItem
        {
            Id = _alarms.Count == 0 ? 1 : _alarms.Max(a => a.Id) + 1,
            Time = AlarmTimePicker.Time.ToString(@"hh\:mm"),
            Label = string.IsNullOrWhiteSpace(AlarmLabelEntry.Text) ? "Alarm" : AlarmLabelEntry.Text.Trim(),
            Days = days
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
            text.Children.Add(new Label { Text = $"{alarm.Label} • {alarm.RepeatText}", FontSize = 12, TextColor = Color.FromArgb("#91A8C0") });
            row.Add(text, 0);
            var enabled = new Switch { IsToggled = alarm.Enabled, OnColor = Color.FromArgb("#A7DFFF"), VerticalOptions = LayoutOptions.Center };
            enabled.Toggled += (_, args) =>
            {
                alarm.Enabled = args.Value;
#if ANDROID
                if (args.Value) AndroidAlarm.Schedule(alarm); else AndroidAlarm.Cancel(alarm.Id);
#endif
                SaveAlarms();
            };
            row.Add(enabled, 1);
            var delete = new Button { Text = "×", TextColor = Colors.White, BackgroundColor = Color.FromArgb("#263B50"), Padding = new Thickness(12, 4), VerticalOptions = LayoutOptions.Center };
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

    void OnTimerClicked(object sender, EventArgs e)
    {
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
        if (_stopwatch.IsRunning) { _stopwatch.Stop(); StopwatchButton.Text = "Resume"; }
        else { _stopwatch.Start(); StopwatchButton.Text = "Pause"; }
    }
    void OnStopwatchResetClicked(object sender, EventArgs e)
    {
        _stopwatch.Reset(); _laps.Clear(); SaveLaps(); RenderLaps();
        StopwatchLabel.Text = "00:00:00.00"; StopwatchButton.Text = "Start";
    }
    void OnLapClicked(object sender, EventArgs e)
    {
        if (!_stopwatch.IsRunning) return;
        _laps.Add(_stopwatch.Elapsed); SaveLaps(); RenderLaps();
    }
    void LoadLaps()
    {
        try { _laps.AddRange(JsonSerializer.Deserialize<List<TimeSpan>>(Preferences.Get(LapsKey, "[]")) ?? []); } catch { }
    }
    void SaveLaps() => Preferences.Set(LapsKey, JsonSerializer.Serialize(_laps));
    void RenderLaps()
    {
        LapList.Clear();
        for (var i = _laps.Count - 1; i >= 0; i--)
            LapList.Children.Add(new Label { Text = $"Lap {i + 1}     {_laps[i]:hh\:mm\:ss\.ff}", TextColor = Color.FromArgb("#91A8C0"), FontSize = 12 });
    }
}