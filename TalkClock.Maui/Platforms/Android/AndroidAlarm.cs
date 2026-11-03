#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using System.Text.Json;
namespace TalkClock;
public static class AndroidAlarm
{
    const string AlarmAction = "com.sinrok.talkclock.ALARM";
    const string SnoozeAction = "com.sinrok.talkclock.SNOOZE_REQUEST";
    const string SnoozeFireAction = "com.sinrok.talkclock.SNOOZE_FIRE";
    const string TimerAction = "com.sinrok.talkclock.TIMER_DONE";
    const int TimerRequestCode = 990001;
    static Context Context => Android.App.Application.Context;
    static AlarmManager Manager => (AlarmManager?)Context.GetSystemService(Context.AlarmService)
        ?? throw new InvalidOperationException("Alarm service is unavailable.");

    public static DateTime NextOccurrence(AlarmItem alarm, DateTime? from = null)
    {
        var now = from ?? DateTime.Now;
        if (!TimeSpan.TryParse(alarm.Time, out var time)) time = new TimeSpan(7, 0, 0);
        if (alarm.Days.Count == 0)
        {
            var once = now.Date.Add(time);
            return once > now ? once : once.AddDays(1);
        }
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (alarm.Days.Contains((int)date.DayOfWeek))
            {
                var candidate = date.Add(time);
                if (candidate > now) return candidate;
            }
        }
        return now.Date.AddDays(7).Add(time);
    }
    public static void Schedule(AlarmItem alarm)
    {
        if (!alarm.Enabled) { Cancel(alarm.Id); return; }
        SaveAlarm(alarm);
        var target = NextOccurrence(alarm);
        ScheduleAt(alarm.Id, alarm.Label, alarm.Days, new DateTimeOffset(target).ToUnixTimeMilliseconds(), AlarmAction, alarm.Vibrate, alarm.GradualVolume);
    }
    static void ScheduleAt(int id, string label, List<int> days, long millis, string action, bool vibrate = true, bool gradualVolume = true)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !Manager.CanScheduleExactAlarms())
            throw new InvalidOperationException("Allow exact alarms in Android settings, then try again.");
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(action);
        intent.PutExtra("alarm_id", id);
        intent.PutExtra("alarm_label", label);
        intent.PutExtra("alarm_days", string.Join(",", days));
        intent.PutExtra("alarm_vibrate", vibrate);
        intent.PutExtra("alarm_gradual_volume", gradualVolume);
        var wallTime = DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime;
        intent.PutExtra("alarm_hour", wallTime.Hour);
        intent.PutExtra("alarm_minute", wallTime.Minute);
        var pending = PendingIntent.GetBroadcast(Context, id, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            Manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending);
        else Manager.SetExact(AlarmType.RtcWakeup, millis, pending);
    }
    public static void Cancel(int id)
    {
        var stored = LoadAlarms().Where(a => a.Id != id).ToList();
        Context.GetSharedPreferences("talkclock_native", FileCreationMode.Private)?.Edit()?.PutString("alarms", JsonSerializer.Serialize(stored))?.Apply();
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(AlarmAction);
        var pending = PendingIntent.GetBroadcast(Context, id, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pending is not null) { Manager.Cancel(pending); pending.Cancel(); }
    }
    public static void ScheduleTimer(long triggerAtMillis)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !Manager.CanScheduleExactAlarms())
            throw new InvalidOperationException("Allow exact alarms in Android settings, then try again.");
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(TimerAction);
        var pending = PendingIntent.GetBroadcast(Context, TimerRequestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            Manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
        else Manager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pending);
    }
    public static void CancelTimer()
    {
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(TimerAction);
        var pending = PendingIntent.GetBroadcast(Context, TimerRequestCode, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pending is not null) { Manager.Cancel(pending); pending.Cancel(); }
    }
    public static void Snooze(int id, string label, bool vibrate, bool gradualVolume, int minutes = 5)
        => ScheduleAt(id, label, [], DateTimeOffset.Now.AddMinutes(Math.Clamp(minutes, 1, 60)).ToUnixTimeMilliseconds(), SnoozeFireAction, vibrate, gradualVolume);

    static List<AlarmItem> LoadAlarms()
    {
        try
        {
            var raw = Context.GetSharedPreferences("talkclock_native", FileCreationMode.Private)?.GetString("alarms", "[]") ?? "[]";
            return JsonSerializer.Deserialize<List<AlarmItem>>(raw) ?? [];
        }
        catch { return []; }
    }
    static void SaveAlarm(AlarmItem alarm)
    {
        var alarms = LoadAlarms();
        alarms.RemoveAll(a => a.Id == alarm.Id);
        alarms.Add(alarm);
        Context.GetSharedPreferences("talkclock_native", FileCreationMode.Private)?.Edit()?.PutString("alarms", JsonSerializer.Serialize(alarms))?.Apply();
    }
    public static void CompleteOneShot(int id)
    {
        var stored = LoadAlarms().Where(a => a.Id != id).ToList();
        Context.GetSharedPreferences("talkclock_native", FileCreationMode.Private)?.Edit()?.PutString("alarms", JsonSerializer.Serialize(stored))?.Apply();
    }
    public static void RestoreAlarms()
    {
        foreach (var alarm in LoadAlarms().Where(a => a.Enabled)) Schedule(alarm);
    }
    public static void StopPlayback(Context context) => context.StopService(new Intent(context, typeof(AlarmPlaybackService)));

    public static void StartPlayback(Context context, int id, string label, bool vibrate, bool gradualVolume)
    {
        var serviceIntent = new Intent(context, typeof(AlarmPlaybackService));
        serviceIntent.PutExtra("alarm_id", id);
        serviceIntent.PutExtra("alarm_label", label);
        serviceIntent.PutExtra("alarm_vibrate", vibrate);
        serviceIntent.PutExtra("alarm_gradual_volume", gradualVolume);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) context.StartForegroundService(serviceIntent);
        else context.StartService(serviceIntent);
    }

    public static void ShowNotification(Context context, int id, string label, bool snoozable, bool vibrate = true, bool gradualVolume = true)
    {
        const string channelId = "clokar_alarms_silent";
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var alarmChannel = new NotificationChannel(channelId, "Alarms", NotificationImportance.Low)
            { Description = "Silent alarm controls; alarm audio is handled by Clokar" };
            alarmChannel.SetSound(null, null);
            manager?.CreateNotificationChannel(alarmChannel);
        }
        var builder = new Notification.Builder(context, channelId)
            .SetContentTitle(string.IsNullOrWhiteSpace(label) ? "TalkClock" : label)
            .SetContentText(snoozable ? $"Alarm • tap Snooze for {Preferences.Get("alarm_snooze_minutes", 5)} minutes" : "Timer finished")
            .SetSmallIcon(Android.Resource.Drawable.IcLockIdleAlarm)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryAlarm)
            .SetPriority((int)NotificationPriority.Max);
        if (snoozable)
        {
            var snoozeIntent = new Intent(context, typeof(AlarmReceiver));
            snoozeIntent.SetAction(SnoozeAction);
            snoozeIntent.PutExtra("alarm_id", id);
            snoozeIntent.PutExtra("alarm_label", label);
            snoozeIntent.PutExtra("alarm_vibrate", vibrate);
            snoozeIntent.PutExtra("alarm_gradual_volume", gradualVolume);
            var snoozePending = PendingIntent.GetBroadcast(context, id + 100000, snoozeIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.AddAction(new Notification.Action.Builder(null, $"Snooze {Preferences.Get("alarm_snooze_minutes", 5)} min", snoozePending).Build());
        }
        manager?.Notify(id, builder.Build());
    }
}
[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;
        var id = intent.GetIntExtra("alarm_id", 7319);
        var label = intent.GetStringExtra("alarm_label") ?? "Alarm";
        var action = intent.Action ?? "";
        if (action == "com.sinrok.talkclock.STOP_ALARM")
        {
            AndroidAlarm.StopPlayback(context);
            var notifications = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            notifications?.Cancel(id);
            return;
        }
        if (action == "com.sinrok.talkclock.TIMER_DONE")
        {
            AndroidAlarm.ShowNotification(context, 990001, "Timer finished", false);
            return;
        }
        if (action == "com.sinrok.talkclock.SNOOZE_REQUEST")
        {
            var snoozeVibrate = intent.GetBooleanExtra("alarm_vibrate", true);
            var snoozeGradual = intent.GetBooleanExtra("alarm_gradual_volume", true);
            AndroidAlarm.StopPlayback(context);
            var notifications = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            notifications?.Cancel(id);
            AndroidAlarm.Snooze(id, label, snoozeVibrate, snoozeGradual, Preferences.Get("alarm_snooze_minutes", 5));
            return;
        }
        var snoozable = action != "com.sinrok.talkclock.SNOOZE_FIRE";
        var vibrate = intent.GetBooleanExtra("alarm_vibrate", true);
        var gradualVolume = intent.GetBooleanExtra("alarm_gradual_volume", true);
        AndroidAlarm.StartPlayback(context, id, label, vibrate, gradualVolume);
        AndroidAlarm.ShowNotification(context, id, label, snoozable, vibrate, gradualVolume);
        if (!snoozable) return;
        var rawDays = intent.GetStringExtra("alarm_days") ?? "";
        var days = rawDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var d) ? d : -1).Where(d => d >= 0 && d <= 6).ToList();
        if (days.Count > 0)
        {
            var hour = intent.GetIntExtra("alarm_hour", DateTime.Now.Hour);
            var minute = intent.GetIntExtra("alarm_minute", DateTime.Now.Minute);
            var repeating = new AlarmItem { Id = id, Label = label, Days = days, Time = $"{hour:D2}:{minute:D2}", Vibrate = vibrate, GradualVolume = gradualVolume };
            AndroidAlarm.Schedule(repeating);
        }
        else AndroidAlarm.CompleteOneShot(id);
    }
}
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && intent?.Action == Intent.ActionBootCompleted)
            AndroidAlarm.RestoreAlarms();
    }
}
#endif