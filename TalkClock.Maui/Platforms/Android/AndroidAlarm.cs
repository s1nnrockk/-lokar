#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
namespace TalkClock;
public static class AndroidAlarm
{
    const string AlarmAction = "com.sinrok.talkclock.ALARM";
    const string SnoozeAction = "com.sinrok.talkclock.SNOOZE_REQUEST";\n    const string SnoozeFireAction = "com.sinrok.talkclock.SNOOZE_FIRE";\n    const string TimerAction = "com.sinrok.talkclock.TIMER_DONE";\n    const int TimerRequestCode = 990001;
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
        var target = NextOccurrence(alarm);
        ScheduleAt(alarm.Id, alarm.Label, alarm.Days, new DateTimeOffset(target).ToUnixTimeMilliseconds(), AlarmAction);
    }
    static void ScheduleAt(int id, string label, List<int> days, long millis, string action)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !Manager.CanScheduleExactAlarms())
            throw new InvalidOperationException("Allow exact alarms in Android settings, then try again.");
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(action);
        intent.PutExtra("alarm_id", id);
        intent.PutExtra("alarm_label", label);
        intent.PutExtra("alarm_days", string.Join(",", days));\n        var wallTime = DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime;\n        intent.PutExtra("alarm_hour", wallTime.Hour);\n        intent.PutExtra("alarm_minute", wallTime.Minute);
        var pending = PendingIntent.GetBroadcast(Context, id, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            Manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending);
        else Manager.SetExact(AlarmType.RtcWakeup, millis, pending);
    }
    public static void Cancel(int id)
    {
        var intent = new Intent(Context, typeof(AlarmReceiver));
        intent.SetAction(AlarmAction);
        var pending = PendingIntent.GetBroadcast(Context, id, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pending is not null) { Manager.Cancel(pending); pending.Cancel(); }
    }
    public static void Snooze(int id, string label)
        => ScheduleAt(id, label, [], DateTimeOffset.Now.AddMinutes(5).ToUnixTimeMilliseconds(), SnoozeFireAction);
    public static void ShowNotification(Context context, int id, string label, bool snoozable)
    {
        const string channelId = "alarms";
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            manager?.CreateNotificationChannel(new NotificationChannel(channelId, "Alarms", NotificationImportance.High) { Description = "Scheduled alarm notifications" });
        var builder = new Notification.Builder(context, channelId)
            .SetContentTitle(string.IsNullOrWhiteSpace(label) ? "TalkClock" : label)
            .SetContentText(snoozable ? "Alarm is ringing • tap Snooze for 5 minutes" : "Snoozed alarm")
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
            var snoozePending = PendingIntent.GetBroadcast(context, id + 100000, snoozeIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.AddAction(new Notification.Action.Builder(null, "Snooze 5 min", snoozePending).Build());
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
        var action = intent.Action ?? "";\n        if (action == "com.sinrok.talkclock.TIMER_DONE")\n        {\n            AndroidAlarm.ShowNotification(context, 990001, "Timer finished", false);\n            return;\n        }
        if (action == "com.sinrok.talkclock.SNOOZE")
        {
            AndroidAlarm.ShowNotification(context, id, label, false);
            AndroidAlarm.Snooze(id, label);
            return;
        }
        AndroidAlarm.ShowNotification(context, id, label, true);
        var rawDays = intent.GetStringExtra("alarm_days") ?? "";
        var days = rawDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var d) ? d : -1).Where(d => d >= 0 && d <= 6).ToList();
        if (days.Count > 0)
        {
            var repeating = new AlarmItem { Id = id, Label = label, Days = days, Time = DateTime.Now.ToString("HH:mm") };
            // Use the original scheduled wall-clock time carried in the intent.
            var hour = intent.GetIntExtra("alarm_hour", DateTime.Now.Hour);
            var minute = intent.GetIntExtra("alarm_minute", DateTime.Now.Minute);
            repeating.Time = $"{hour:D2}:{minute:D2}";
            AndroidAlarm.Schedule(repeating);
        }
    }
}
#endif