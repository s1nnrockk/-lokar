#if ANDROID
using Android.App;
using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace TalkClock;

[Activity(Name = "com.sinrok.clokar.AlarmRingingActivity", Exported = false,
    Theme = "@android:style/Theme.Material.NoActionBar",
    LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]
public sealed class AlarmRingingActivity : Activity
{
    const string StopAction = "com.sinrok.talkclock.STOP_ALARM";
    const string SnoozeAction = "com.sinrok.talkclock.SNOOZE_REQUEST";
    const int NotificationBase = 992001;
    Handler? _handler;
    TextView? _clock;
    TextView? _date;
    TextView? _label;
    TextView? _status;
    FrameLayout? _pulse;
    int _alarmId;
    string _alarmLabel = "Alarm";
    bool _vibrate = true;
    bool _gradual = true;
    bool _finished;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn |
            WindowManagerFlags.KeepScreenOn | WindowManagerFlags.DismissKeyguard);
        Window?.SetStatusBarColor(Color.ParseColor("#080F1B"));
        Window?.SetNavigationBarColor(Color.ParseColor("#080F1B"));
        ReadIntent(Intent);
        BuildScreen();
        _handler = new Handler(Looper.MainLooper!);
        UpdateClock();
        _handler.PostDelayed(ClockTick, 1000);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is null) return;
        SetIntent(intent);
        ReadIntent(intent);
        if (_label is not null) _label.Text = _alarmLabel;
        if (_status is not null) _status.Text = "YOUR ALARM IS RINGING";
    }

    void ReadIntent(Intent? intent)
    {
        _alarmId = intent?.GetIntExtra("alarm_id", 7319) ?? 7319;
        _alarmLabel = intent?.GetStringExtra("alarm_label") ?? "Alarm";
        _vibrate = intent?.GetBooleanExtra("alarm_vibrate", true) ?? true;
        _gradual = intent?.GetBooleanExtra("alarm_gradual_volume", true) ?? true;
    }

    void BuildScreen()
    {
        var root = new FrameLayout(this)
        {
            Background = new GradientDrawable(GradientDrawable.Orientation.TopBottom,
                new[] { Color.ParseColor("#07101D"), Color.ParseColor("#10263B"), Color.ParseColor("#080F1B") })
        };
        var content = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Gravity = GravityFlags.Center,
            LayoutParameters = new FrameLayout.LayoutParams(-1, -1),
            // Padding leaves room for gesture navigation and creates a calm, spacious layout.
        };
        content.SetPadding(Dp(28), Dp(38), Dp(28), Dp(34));

        var brand = Text("C L O K A R", 12, "#8FB6D5", true);
        brand.Gravity = GravityFlags.Center;
        content.AddView(brand, MatchWrap());
        content.AddView(Spacer(26), MatchWrap());

        _pulse = new FrameLayout(this) { LayoutParameters = new LinearLayout.LayoutParams(Dp(220), Dp(220)) { Gravity = GravityFlags.Center } };
        var outer = Circle("#183C5870", "#426E8E", 2);
        var middle = Circle("#1C4562", "#3F7699", 1);
        var inner = Circle("#244B68", "#6C9AB8", 2);
        _pulse.AddView(outer, new FrameLayout.LayoutParams(-1, -1));
        var middleParams = new FrameLayout.LayoutParams(Dp(174), Dp(174), GravityFlags.Center);
        _pulse.AddView(middle, middleParams);
        var innerParams = new FrameLayout.LayoutParams(Dp(126), Dp(126), GravityFlags.Center);
        _pulse.AddView(inner, innerParams);
        var glyph = Text("◷", 58, "#D9EEFF", false);
        glyph.Gravity = GravityFlags.Center;
        _pulse.AddView(glyph, new FrameLayout.LayoutParams(-1, -1));
        content.AddView(_pulse, new LinearLayout.LayoutParams(Dp(220), Dp(220)) { Gravity = GravityFlags.Center });
        content.AddView(Spacer(30), MatchWrap());

        _status = Text("YOUR ALARM IS RINGING", 11, "#8FB6D5", true);
        _status.Gravity = GravityFlags.Center;
        content.AddView(_status, MatchWrap());
        content.AddView(Spacer(10), MatchWrap());
        _clock = Text("07:00", 58, "#F2F8FF", false);
        _clock.Gravity = GravityFlags.Center;
        content.AddView(_clock, MatchWrap());
        _date = Text(DateTime.Now.ToString("dddd, d MMMM"), 14, "#A9C0D3", false);
        _date.Gravity = GravityFlags.Center;
        content.AddView(_date, MatchWrap());
        content.AddView(Spacer(20), MatchWrap());
        _label = Text(_alarmLabel, 20, "#E5F2FC", true);
        _label.Gravity = GravityFlags.Center;
        content.AddView(_label, MatchWrap());
        content.AddView(Spacer(10), MatchWrap());
        var hint = Text("Take a breath. It's time to start.", 13, "#809BB2", false);
        hint.Gravity = GravityFlags.Center;
        content.AddView(hint, MatchWrap());
        content.AddView(Spacer(34), MatchWrap());

        var snooze = ActionButton("Snooze  ·  5 min", "#DCEEFF", "#19364E");
        snooze.Click += (_, _) => SnoozeAlarm();
        content.AddView(snooze, new LinearLayout.LayoutParams(-1, Dp(58)));
        content.AddView(Spacer(12), MatchWrap());
        var dismiss = ActionButton("Dismiss alarm", "#F0F6FC", "#345B78");
        dismiss.Click += (_, _) => DismissAlarm();
        content.AddView(dismiss, new LinearLayout.LayoutParams(-1, Dp(58)));

        root.AddView(content);
        SetContentView(root);
        StartPulse(outer, middle, inner);
    }

    void StartPulse(params View[] rings)
    {
        foreach (var ring in rings)
        {
            var animator = ObjectAnimator.OfPropertyValuesHolder(ring,
                PropertyValuesHolder.OfFloat("scaleX", 1f, 1.07f, 1f),
                PropertyValuesHolder.OfFloat("scaleY", 1f, 1.07f, 1f),
                PropertyValuesHolder.OfFloat("alpha", 0.65f, 1f, 0.65f));
            animator.SetDuration(2600);
            animator.RepeatCount = ValueAnimator.Infinite;
            animator.RepeatMode = ValueAnimatorRepeatMode.Restart;
            animator.StartDelay = rings.Length * 180;
            animator.Start();
        }
    }

    void UpdateClock()
    {
        if (_clock is not null) _clock.Text = DateTime.Now.ToString(Preferences.Get("use_24_hour", true) ? "HH:mm" : "h:mm tt");
        if (_date is not null) _date.Text = DateTime.Now.ToString("dddd, d MMMM");
    }
    void ClockTick()
    {
        if (_finished || IsFinishing) return;
        UpdateClock();
        _handler?.PostDelayed(ClockTick, 1000);
    }

    void SnoozeAlarm()
    {
        if (_finished) return;
        _finished = true;
        AndroidAlarm.StopPlayback(this);
        var notifications = (NotificationManager?)GetSystemService(NotificationService);
        notifications?.Cancel(_alarmId);
        AndroidAlarm.Snooze(_alarmId, _alarmLabel, _vibrate, _gradual);
        Finish();
    }

    void DismissAlarm()
    {
        if (_finished) return;
        _finished = true;
        AndroidAlarm.StopPlayback(this);
        var notifications = (NotificationManager?)GetSystemService(NotificationService);
        notifications?.Cancel(_alarmId);
        Finish();
    }

    TextView Text(string value, float size, string color, bool bold)
    {
        var view = new TextView(this)
        {
            Text = value,
            TextSize = size,
            Typeface = Typeface.Create(bold ? "sans-serif-medium" : "sans-serif-light", TypefaceStyle.Normal)
        };
        view.SetTextColor(Color.ParseColor(color));
        return view;
    }

    Button ActionButton(string title, string textColor, string fill)
    {
        var button = new Button(this) { Text = title, TextSize = 16, AllCaps = false }
        ;
        button.SetTextColor(Color.ParseColor(textColor));
        button.Background = new GradientDrawable { Shape = ShapeType.Rectangle, CornerRadius = Dp(20), Color = ColorStateList.ValueOf(Color.ParseColor(fill)) };
        return button;
    }

    static GradientDrawable Circle(string fill, string stroke, int strokeDp)
    {
        var shape = new GradientDrawable();
        shape.SetShape(ShapeType.Oval);
        shape.SetColor(Color.ParseColor(fill));
        shape.SetStroke(DpStatic(strokeDp), Color.ParseColor(stroke));
        return shape;
    }
    View Spacer(int height) => new View(this) { LayoutParameters = new LinearLayout.LayoutParams(1, Dp(height)) };
    LinearLayout.LayoutParams MatchWrap() => new(-1, -2);
    int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
    static int DpStatic(float value) => (int)(value * Android.App.Application.Context.Resources!.DisplayMetrics!.Density + 0.5f);

    protected override void OnDestroy()
    {
        _finished = true;
        _handler?.RemoveCallbacksAndMessages(null);
        base.OnDestroy();
    }
}
#endif
