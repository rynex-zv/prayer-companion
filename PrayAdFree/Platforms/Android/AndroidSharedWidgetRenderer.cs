#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidSharedWidgetRenderer {
    private static readonly int[] TextIds = [Resource.Id.widget_shared_text_1, Resource.Id.widget_shared_text_2, Resource.Id.widget_shared_text_3, Resource.Id.widget_shared_text_4];
    private static readonly int[] RowIds = [Resource.Id.widget_shared_row_1, Resource.Id.widget_shared_row_2, Resource.Id.widget_shared_row_3, Resource.Id.widget_shared_row_4, Resource.Id.widget_shared_row_5, Resource.Id.widget_shared_row_6];
    private static readonly int[] RowLabelIds = [Resource.Id.widget_shared_row_1_label, Resource.Id.widget_shared_row_2_label, Resource.Id.widget_shared_row_3_label, Resource.Id.widget_shared_row_4_label, Resource.Id.widget_shared_row_5_label, Resource.Id.widget_shared_row_6_label];
    private static readonly int[] RowValueIds = [Resource.Id.widget_shared_row_1_value, Resource.Id.widget_shared_row_2_value, Resource.Id.widget_shared_row_3_value, Resource.Id.widget_shared_row_4_value, Resource.Id.widget_shared_row_5_value, Resource.Id.widget_shared_row_6_value];

    public static void UpdateWidgets(Context context, AppWidgetManager manager, int[] ids, WidgetProjection projection, WidgetTemplateKind defaultTemplate, string providerKey, long projectionBuildMs = 0) {
        foreach (var id in ids) UpdateWidget(context, manager, id, projection, defaultTemplate, providerKey, projectionBuildMs);
    }

    public static void UpdateWidget(Context context, AppWidgetManager manager, int id, WidgetProjection projection, WidgetTemplateKind defaultTemplate, string providerKey, long projectionBuildMs = 0) {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var options = manager.GetAppWidgetOptions(id);
        var capabilities = AndroidWidgetEnvironment.ResolveCapabilities(options);
        var profiles = AndroidWidgetEnvironment.CreateWidgetProfileService();
        var instanceId = $"android:{providerKey}:{id}";
        var assignment = profiles.Snapshot().Assignments.FirstOrDefault(item => item.InstanceId == instanceId);
        var profile = assignment == null
            ? profiles.Snapshot().Profiles.Single(item => item.Template == defaultTemplate && item.IsBuiltIn)
            : profiles.Find(assignment.ProfileId);
        if (assignment == null || assignment.Family != capabilities.Family || assignment.Surface != capabilities.Surface || assignment.ProfileId != profile.Id) {
            profiles.Assign(new WidgetInstanceAssignment {
                InstanceId = instanceId,
                ProfileId = profile.Id,
                Platform = WidgetPlatform.Android,
                Surface = capabilities.Surface,
                Family = capabilities.Family,
                MinWidthDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMinWidth, 0) ?? 0,
                MaxWidthDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMaxWidth, 0) ?? 0,
                MinHeightDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMinHeight, 0) ?? 0,
                MaxHeightDp = options?.GetInt(AppWidgetManager.OptionAppwidgetMaxHeight, 0) ?? 0
            });
        }

        var layoutStarted = started.ElapsedMilliseconds;
        var tree = new WidgetLayoutResolver().Resolve(profile, projection, capabilities);
        var layoutDuration = started.ElapsedMilliseconds - layoutStarted;
        var renderStarted = started.ElapsedMilliseconds;
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_shared);
        Bind(context, views, tree, id);
        manager.UpdateAppWidget(id, views);
        AndroidWidgetFileLog.Write(id, profile, capabilities, projectionBuildMs, layoutDuration, started.ElapsedMilliseconds - renderStarted, started.ElapsedMilliseconds);
    }

    private static void Bind(Context context, RemoteViews views, WidgetRenderTree tree, int id) {
        var primary = ParseColor(tree.Style.PrimaryTextColor);
        var secondary = ParseColor(tree.Style.SecondaryTextColor);
        var background = ParseColor(tree.Style.BackgroundColor, tree.Style.BackgroundOpacity);
        views.SetInt(Resource.Id.widget_shared_root, "setBackgroundColor", background);
        var textSize = tree.Style.TextScale switch { WidgetTextScale.Small => 11f, WidgetTextScale.Large => 17f, WidgetTextScale.ExtraLarge => 20f, _ => 14f };

        for (var index = 0; index < TextIds.Length; index++) {
            var item = tree.Texts.ElementAtOrDefault(index);
            views.SetViewVisibility(TextIds[index], item == null ? ViewStates.Gone : ViewStates.Visible);
            if (item == null) continue;
            views.SetTextViewText(TextIds[index], item.Text);
            views.SetTextColor(TextIds[index], item.Required ? primary : secondary);
            views.SetTextViewTextSize(TextIds[index], (int)global::Android.Util.ComplexUnitType.Sp, item.Role is "title" or "time" or "bearing" ? textSize + 4 : textSize);
            views.SetContentDescription(TextIds[index], item.AccessibilityLabel);
        }

        for (var index = 0; index < RowIds.Length; index++) {
            var row = tree.Rows.ElementAtOrDefault(index);
            views.SetViewVisibility(RowIds[index], row == null ? ViewStates.Gone : ViewStates.Visible);
            if (row == null) continue;
            views.SetTextViewText(RowLabelIds[index], row.Label);
            views.SetTextViewText(RowValueIds[index], row.Value);
            views.SetTextColor(RowLabelIds[index], row.Highlighted ? ParseColor(tree.Style.AccentColor) : primary);
            views.SetTextColor(RowValueIds[index], row.Highlighted ? ParseColor(tree.Style.AccentColor) : primary);
            views.SetContentDescription(RowIds[index], row.AccessibilityLabel);
        }

        if (tree.CountdownTargetUnixMilliseconds is > 0 && tree.Status == "ready") {
            var remaining = tree.CountdownTargetUnixMilliseconds.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            views.SetViewVisibility(Resource.Id.widget_shared_countdown, ViewStates.Visible);
            views.SetChronometer(Resource.Id.widget_shared_countdown, SystemClock.ElapsedRealtime() + Math.Max(0, remaining), null, true);
            if (OperatingSystem.IsAndroidVersionAtLeast(24)) {
                views.SetChronometerCountDown(Resource.Id.widget_shared_countdown, true);
            }
        } else views.SetViewVisibility(Resource.Id.widget_shared_countdown, ViewStates.Gone);

        if (tree.Progress.HasValue) {
            views.SetViewVisibility(Resource.Id.widget_shared_progress, ViewStates.Visible);
            views.SetProgressBar(Resource.Id.widget_shared_progress, 1000, (int)Math.Round(tree.Progress.Value * 1000), false);
        } else views.SetViewVisibility(Resource.Id.widget_shared_progress, ViewStates.Gone);

        BindActions(context, views, tree.Actions, id);
    }

    private static void BindActions(Context context, RemoteViews views, IReadOnlyList<WidgetRenderAction> actions, int id) {
        var actionIds = new[] { Resource.Id.widget_shared_action_1, Resource.Id.widget_shared_action_2 };
        views.SetViewVisibility(Resource.Id.widget_shared_actions, actions.Count == 0 ? ViewStates.Gone : ViewStates.Visible);
        for (var index = 0; index < actionIds.Length; index++) {
            var action = actions.ElementAtOrDefault(index);
            views.SetViewVisibility(actionIds[index], action == null ? ViewStates.Gone : ViewStates.Visible);
            if (action == null) continue;
            views.SetTextViewText(actionIds[index], action.Label);
            views.SetContentDescription(actionIds[index], action.AccessibilityLabel);
            views.SetOnClickPendingIntent(actionIds[index], BuildPendingIntent(context, action, id, index));
        }
    }

    private static PendingIntent BuildPendingIntent(Context context, WidgetRenderAction action, int id, int index) {
        Intent intent;
        if (action.Id is "tasbih.increment" or "tasbih.reset") {
            intent = new Intent(context, typeof(TasbihWidgetProvider));
            intent.SetAction(action.Id == "tasbih.increment" ? TasbihWidgetProvider.ActionIncrement : TasbihWidgetProvider.ActionReset);
            intent.PutExtra("appWidgetId", id);
            return PendingIntent.GetBroadcast(context, (id * 10) + index + 1, intent, PendingFlags())!;
        }
        var uri = global::Android.Net.Uri.Parse(action.DeepLink) ?? throw new InvalidOperationException("Widget action deep link is invalid.");
        intent = new Intent(Intent.ActionView, uri, context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(context, (id * 10) + index + 1, intent, PendingFlags())!;
    }

    private static PendingIntentFlags PendingFlags() => OperatingSystem.IsAndroidVersionAtLeast(23)
        ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
        : PendingIntentFlags.UpdateCurrent;

    private static global::Android.Graphics.Color ParseColor(string argb) => global::Android.Graphics.Color.ParseColor(argb);
    private static global::Android.Graphics.Color ParseColor(string argb, int opacity) {
        var color = global::Android.Graphics.Color.ParseColor(argb);
        return global::Android.Graphics.Color.Argb((byte)Math.Clamp(opacity * 255 / 100, 0, 255), color.R, color.G, color.B);
    }
}

internal static class AndroidWidgetFileLog {
    private static readonly object Sync = new();
    public static void Write(int instanceId, WidgetProfile profile, WidgetHostCapabilities host, long projectionMs, long layoutMs, long renderMs, long totalMs) {
        var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "PrayAdFree", "logs", "widgets-android.log");
        lock (Sync) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, System.Text.Json.JsonSerializer.Serialize(new {
                at = DateTimeOffset.UtcNow,
                platform = "android",
                instanceId,
                profileId = profile.Id,
                family = host.Family.ToString(),
                widthDp = host.WidthDp,
                heightDp = host.HeightDp,
                revision = profile.Revision,
                projectionMs,
                layoutMs,
                renderMs,
                totalMs
            }) + System.Environment.NewLine);
        }
    }
}
#endif
