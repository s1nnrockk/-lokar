#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
namespace TalkClock;
public static class AndroidAlarm
{
    const int AlarmId = 7319;
    public static void Schedule(long triggerAtMillis)
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(AlarmReceiver));
        intent.SetAction("com.sinrok.talkclock.ALARM");
        var pending = PendingIntent.GetBroadcast(context, AlarmId, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService)
            ?? throw new InvalidOperationException("Alarm service is unavailable.");
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            if (!manager.CanScheduleExactAlarms())
                throw new InvalidOperationException("Allow exact alarms in Android settings, then try again.");
            manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
        }
        else manager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pending);
    }
}
[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel("alarms", "Alarms", NotificationImportance.High);
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
        var notification = new Notification.Builder(context, "alarms")
            .SetContentTitle("TalkClock")
            .SetContentText("Your alarm is ringing.")
            .SetSmallIcon(Android.Resource.Drawable.IcLockIdleAlarm)
            .SetAutoCancel(true)
            .Build();
        var notifications = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        notifications?.Notify(7319, notification);
    }
}
#endif