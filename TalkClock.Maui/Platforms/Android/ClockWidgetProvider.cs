#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace TalkClock;

[BroadcastReceiver(Name = "com.sinrok.talkclock.ClockWidgetProvider", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/talkclock_widget_info")]
public sealed class ClockWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null) return;
        foreach (var appWidgetId in appWidgetIds)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.talkclock_widget);
            var now = DateTime.Now;
            views.SetTextViewText(Resource.Id.widget_date, now.ToString("dddd, MMMM d"));
            views.SetTextViewText(Resource.Id.widget_caption, "YOUR TIME, IN FLOW");
            var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
            if (launchIntent is null) continue;
            launchIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            var launchPendingIntent = PendingIntent.GetActivity(
                context, appWidgetId, launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            views.SetOnClickPendingIntent(Resource.Id.widget_root, launchPendingIntent);
            appWidgetManager.UpdateAppWidget(appWidgetId, views);
        }
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Android.OS.Bundle? newOptions)
    {
        base.OnAppWidgetOptionsChanged(context, appWidgetManager, appWidgetId, newOptions);
        if (context is not null && appWidgetManager is not null)
            OnUpdate(context, appWidgetManager, new[] { appWidgetId });
    }
}
#endif
