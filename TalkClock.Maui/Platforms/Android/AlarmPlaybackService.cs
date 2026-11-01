#if ANDROID
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;

namespace TalkClock;

[Service(Enabled = true, Exported = false, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public sealed class AlarmPlaybackService : Service
{
    const int NotificationId = 992001;
    const string ChannelId = "clokar_alarm_playback_silent";
    Handler? _handler;
    MediaPlayer? _player;
    Vibrator? _vibrator;
    int _rampStep;

    public override void OnCreate()
    {
        base.OnCreate();
        _handler = new Handler(Looper.MainLooper!);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var label = intent?.GetStringExtra("alarm_label") ?? "Alarm";
        var vibrate = intent?.GetBooleanExtra("alarm_vibrate", true) ?? true;
        var gradual = intent?.GetBooleanExtra("alarm_gradual_volume", true) ?? true;
        CreateChannel();

        var stopIntent = new Intent(this, typeof(AlarmReceiver));
        stopIntent.SetAction("com.sinrok.talkclock.STOP_ALARM");
        stopIntent.PutExtra("alarm_id", intent?.GetIntExtra("alarm_id", 7319) ?? 7319);
        var stopPending = PendingIntent.GetBroadcast(this, NotificationId, stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var notificationBuilder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);        var ringingIntent = new Intent(this, typeof(AlarmRingingActivity));
        ringingIntent.PutExtra("alarm_id", intent?.GetIntExtra("alarm_id", 7319) ?? 7319);
        ringingIntent.PutExtra("alarm_label", label);
        ringingIntent.PutExtra("alarm_vibrate", vibrate);
        ringingIntent.PutExtra("alarm_gradual_volume", gradual);
        ringingIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        var fullScreenPending = PendingIntent.GetActivity(this, intent?.GetIntExtra("alarm_id", 7319) ?? 7319,
            ringingIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var notification = notificationBuilder
            .SetContentTitle(string.IsNullOrWhiteSpace(label) ? "Clokar alarm" : label)
            .SetContentText("Alarm is ringing")
            .SetSmallIcon(Android.Resource.Drawable.IcLockIdleAlarm)
            .SetOngoing(true)
            .SetCategory(Notification.CategoryAlarm)
            .SetFullScreenIntent(fullScreenPending, true)
            .AddAction(new Notification.Action.Builder(null, "Dismiss", stopPending).Build())
            .Build();
        StartForeground(NotificationId, notification);

        StopAudio();
        try
        {
            var alarmUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);
            if (alarmUri is not null)
            {
                _player = new MediaPlayer();
                _player.SetWakeMode(this, WakeMode.Partial);
                _player.SetDataSource(this, alarmUri);
                _player.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Alarm)
                    .SetContentType(AudioContentType.Sonification)
                    .Build());
                _player.Looping = true;
                _player.Prepare();
                var initialVolume = gradual ? 0.04f : 1f;
                _player.SetVolume(initialVolume, initialVolume);
                _player.Start();
                if (gradual) ScheduleVolumeStep();
            }
        }
        catch { StopAudio(); }

        if (vibrate)
        {
            _vibrator = (Vibrator?)GetSystemService(VibratorService);
            if (_vibrator is not null)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    _vibrator.Vibrate(VibrationEffect.CreateWaveform(new long[] { 0, 650, 350 }, 0));
                else
                {
#pragma warning disable CS0618
                    _vibrator.Vibrate(new long[] { 0, 650, 350 }, 0);
#pragma warning restore CS0618
                }
            }
        }
        return StartCommandResult.Sticky;
    }

    void CreateChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        var channel = new NotificationChannel(ChannelId, "Clokar alarm playback", NotificationImportance.High)
        {
            Description = "Ongoing alarm sound and vibration controls",
            LockscreenVisibility = NotificationVisibility.Public
        };
        channel.SetSound(null, null);
        manager?.CreateNotificationChannel(channel);
    }

    void ScheduleVolumeStep()
    {
        _handler?.PostDelayed(() =>
        {
            if (_player?.IsPlaying != true) return;
            _rampStep = Math.Min(_rampStep + 1, 15);
            var volume = 0.04f + (0.96f * _rampStep / 15f);
            _player.SetVolume(volume, volume);
            if (_rampStep < 15) ScheduleVolumeStep();
        }, 2000);
    }

    void StopAudio()
    {
        _handler?.RemoveCallbacksAndMessages(null);
        try { _player?.Stop(); } catch { }
        _player?.Release();
        _player = null;
        _rampStep = 0;
    }

    public override void OnDestroy()
    {
        StopAudio();
        _vibrator?.Cancel();
        _vibrator = null;
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;
}
#endif
