using System.Diagnostics;
using Microsoft.Maui.Dispatching;
namespace TalkClock;
public partial class MainPage : ContentPage
{
    readonly Stopwatch _stopwatch = new();
    IDispatcherTimer? _uiTimer;
    IDispatcherTimer? _countdownTimer;
    DateTime _timerEnd;
    bool _timerRunning;
    public MainPage()
    {
        InitializeComponent();
        AlarmTimePicker.Time = new TimeSpan(7, 0, 0);
        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        _uiTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            ClockLabel.Text = now.ToString("HH:mm:ss");
            DateLabel.Text = now.ToString("dddd, MMMM d");
            StopwatchLabel.Text = _stopwatch.Elapsed.ToString(@"mm:ss.ff");
            if (_timerRunning)
            {
                var remaining = _timerEnd - DateTime.Now;
                if (remaining <= TimeSpan.Zero)
                {
                    _timerRunning = false;
                    _countdownTimer?.Stop();
                    TimerStatus.Text = "Finished";
                    TimerButton.Text = "Start timer";
                    DisplayAlert("Timer", "Your timer has finished.", "OK");
                }
                else TimerStatus.Text = remaining.ToString(@"hh:mm:ss");
            }
        };
        _uiTimer.Start();
    }
    async void OnSetAlarmClicked(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var time = AlarmTimePicker.Time;
            var now = DateTime.Now;
            var target = now.Date.Add(time);
            if (target <= now) target = target.AddDays(1);
            var millis = new DateTimeOffset(target).ToUnixTimeMilliseconds();
            AndroidAlarm.Schedule(millis);
            AlarmStatus.Text = $"Alarm set for {target:ddd, HH:mm}";
        }
        catch (Exception ex) { await DisplayAlert("Alarm unavailable", ex.Message, "OK"); }
#else
        await DisplayAlert("Alarm", "Alarm scheduling is available on Android.", "OK");
#endif
    }
    void OnTimerClicked(object sender, EventArgs e)
    {
        if (_timerRunning)
        {
            _timerRunning = false;
            TimerButton.Text = "Resume";
            return;
        }
        if (TimerButton.Text == "Resume")
        {
            var parts = TimerStatus.Text.Split(':');
            if (TimeSpan.TryParse(TimerStatus.Text, out var remain)) _timerEnd = DateTime.Now.Add(remain);
            else _timerEnd = DateTime.Now.AddMinutes(5);
        }
        else
        {
            int.TryParse(TimerMinutes.Text, out var mins);
            int.TryParse(TimerSeconds.Text, out var secs);
            var duration = TimeSpan.FromMinutes(Math.Clamp(mins, 0, 999)).Add(TimeSpan.FromSeconds(Math.Clamp(secs, 0, 59)));
            if (duration <= TimeSpan.Zero) { TimerStatus.Text = "Enter a duration"; return; }
            _timerEnd = DateTime.Now.Add(duration);
        }
        _timerRunning = true;
        TimerButton.Text = "Pause";
    }
    void OnStopwatchClicked(object sender, EventArgs e)
    {
        if (_stopwatch.IsRunning) { _stopwatch.Stop(); StopwatchButton.Text = "Resume"; }
        else { _stopwatch.Start(); StopwatchButton.Text = "Pause"; }
    }
    void OnStopwatchResetClicked(object sender, EventArgs e)
    {
        _stopwatch.Reset();
        StopwatchLabel.Text = "00:00.00";
        StopwatchButton.Text = "Start";
    }
}