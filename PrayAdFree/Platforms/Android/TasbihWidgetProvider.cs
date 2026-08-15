#if ANDROID
using Android.Appwidget;
using Android.App;
using Android.Content;
using Android.Widget;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

#if PRAY_WIDGETS
[BroadcastReceiver(Enabled = true, Exported = true, Label = "@string/widget_tasbih_label")]
#else
[BroadcastReceiver(Enabled = false, Exported = false, Label = "@string/widget_tasbih_label")]
#endif
[IntentFilterAttribute([AppWidgetManager.ActionAppwidgetUpdate, ActionIncrement, ActionReset, ActionPresetPrevious, ActionPresetNext])]
[MetaData("android.appwidget.provider", Resource = "@xml/tasbih_widget_info")]
public sealed class TasbihWidgetProvider : AppWidgetProvider {
    internal const string ActionIncrement = "com.rynex.prayer.widget.TASBIH_INCREMENT";
    internal const string ActionReset = "com.rynex.prayer.widget.TASBIH_RESET";
    internal const string ActionPresetPrevious = "com.rynex.prayer.widget.TASBIH_PRESET_PREVIOUS";
    internal const string ActionPresetNext = "com.rynex.prayer.widget.TASBIH_PRESET_NEXT";
    private const string ExtraAppWidgetId = "appWidgetId";
    private static readonly TasbihProgressCalculator ProgressCalculator = new();

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) {
        if (context == null || appWidgetManager == null || appWidgetIds == null) {
            return;
        }

        UpdateWidgets(context, appWidgetManager, appWidgetIds);
    }

    public override void OnEnabled(Context? context) {
        if (context == null) {
            return;
        }

        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) {
            return;
        }

        var ids = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(TasbihWidgetProvider)))) ?? [];
        UpdateWidgets(context, manager, ids);
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, global::Android.OS.Bundle? newOptions) {
        base.OnAppWidgetOptionsChanged(context, appWidgetManager, appWidgetId, newOptions);
        if (context == null || appWidgetManager == null) {
            return;
        }

        UpdateWidgets(context, appWidgetManager, [appWidgetId]);
    }

    public override void OnDeleted(Context? context, int[]? appWidgetIds) {
        if (context == null || appWidgetIds == null) {
            return;
        }

        var store = AndroidWidgetEnvironment.CreateTasbihStateStore();
        var profiles = AndroidWidgetEnvironment.CreateWidgetProfileService();
        foreach (var appWidgetId in appWidgetIds) {
            store.Remove(appWidgetId);
            profiles.Unassign($"android:tasbih:{appWidgetId}");
        }
    }

    public override void OnReceive(Context? context, Intent? intent) {
        base.OnReceive(context, intent);
        if (context == null || intent == null) {
            return;
        }

        if (intent.Action is not (ActionIncrement or ActionReset or ActionPresetPrevious or ActionPresetNext)) {
            return;
        }

        var appWidgetId = intent.GetIntExtra(ExtraAppWidgetId, AppWidgetManager.InvalidAppwidgetId);
        if (appWidgetId == AppWidgetManager.InvalidAppwidgetId) {
            return;
        }

        HandleAction(context, appWidgetId, intent.Action);
    }

    internal static void UpdateWidgets(Context context, AppWidgetManager manager, int[] appWidgetIds) {
        var settings = AndroidWidgetEnvironment.LoadSettings();
        AndroidWidgetEnvironment.InitializeLocalizationAsync(settings).GetAwaiter().GetResult();
        var store = AndroidWidgetEnvironment.CreateTasbihStateStore();

        foreach (var appWidgetId in appWidgetIds) {
            var projectionStarted = System.Diagnostics.Stopwatch.StartNew();
            var state = EnsureState(store, settings, appWidgetId);
            var preset = ResolvePreset(settings, state.PresetIndex);
            var snapshot = ProgressCalculator.BuildSnapshot(preset, state.Count);
            var language = string.Equals(settings.Language, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
            var projection = new WidgetProjection {
                GeneratedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Language = language,
                IsRtl = language == "ar",
                Status = snapshot.IsEmpty ? "error" : "ready",
                Error = snapshot.IsEmpty ? LocalizationManager.Translate("Tasbih_Empty") : "",
                TasbihPresetName = TasbihTextResolver.Translate(preset?.Name ?? ""),
                TasbihText = snapshot.IsEmpty ? "" : TasbihTextResolver.Translate(snapshot.CurrentText),
                TasbihCount = state.Count,
                TasbihTarget = snapshot.TotalTarget
            };
            AndroidSharedWidgetRenderer.UpdateWidget(context, manager, appWidgetId, projection, WidgetTemplateKind.Tasbih, "tasbih", projectionStarted.ElapsedMilliseconds);
        }
    }

    private static void HandleAction(Context context, int appWidgetId, string action) {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) {
            return;
        }

        var settings = AndroidWidgetEnvironment.LoadSettings();
        AndroidWidgetEnvironment.InitializeLocalizationAsync(settings).GetAwaiter().GetResult();
        var store = AndroidWidgetEnvironment.CreateTasbihStateStore();
        var state = EnsureState(store, settings, appWidgetId);
        var preset = ResolvePreset(settings, state.PresetIndex);

        state = action switch {
            ActionIncrement when preset != null => new TasbihWidgetState {
                AppWidgetId = state.AppWidgetId,
                PresetIndex = state.PresetIndex,
                Count = ProgressCalculator.GetNextCount(preset, state.Count),
                LastUpdatedUtc = DateTime.UtcNow
            },
            ActionReset => new TasbihWidgetState {
                AppWidgetId = state.AppWidgetId,
                PresetIndex = state.PresetIndex,
                Count = 0,
                LastUpdatedUtc = DateTime.UtcNow
            },
            ActionPresetPrevious when state.Count == 0 => new TasbihWidgetState {
                AppWidgetId = state.AppWidgetId,
                PresetIndex = ShiftPresetIndex(settings, state.PresetIndex, -1),
                Count = 0,
                LastUpdatedUtc = DateTime.UtcNow
            },
            ActionPresetNext when state.Count == 0 => new TasbihWidgetState {
                AppWidgetId = state.AppWidgetId,
                PresetIndex = ShiftPresetIndex(settings, state.PresetIndex, 1),
                Count = 0,
                LastUpdatedUtc = DateTime.UtcNow
            },
            _ => state
        };

        store.Save(state);
        UpdateWidgets(context, manager, [appWidgetId]);
    }

    private static TasbihWidgetState EnsureState(TasbihWidgetStateStore store, AppSettings settings, int appWidgetId) {
        return store.GetOrCreate(appWidgetId, () => new TasbihWidgetState {
            AppWidgetId = appWidgetId,
            PresetIndex = Math.Clamp(settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, settings.Tasbih.Presets.Count - 1)),
            Count = 0,
            LastUpdatedUtc = DateTime.UtcNow
        });
    }

    private static TasbihPresetSettings? ResolvePreset(AppSettings settings, int presetIndex) {
        if (settings.Tasbih.Presets.Count == 0) {
            return null;
        }

        var index = Math.Clamp(presetIndex, 0, settings.Tasbih.Presets.Count - 1);
        return settings.Tasbih.Presets[index];
    }

    private static int ShiftPresetIndex(AppSettings settings, int currentIndex, int delta) {
        if (settings.Tasbih.Presets.Count == 0) {
            return 0;
        }

        var normalized = Math.Clamp(currentIndex, 0, settings.Tasbih.Presets.Count - 1);
        var next = normalized + delta;
        if (next < 0) {
            return settings.Tasbih.Presets.Count - 1;
        }

        if (next >= settings.Tasbih.Presets.Count) {
            return 0;
        }

        return next;
    }

    private static PendingIntent BuildActionPendingIntent(Context context, int appWidgetId, string action) {
        var intent = new Intent(context, typeof(TasbihWidgetProvider));
        intent.SetAction(action);
        intent.PutExtra(ExtraAppWidgetId, appWidgetId);
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        var requestCode = action switch {
            ActionIncrement => (appWidgetId * 10) + 1,
            ActionReset => (appWidgetId * 10) + 2,
            ActionPresetPrevious => (appWidgetId * 10) + 3,
            ActionPresetNext => (appWidgetId * 10) + 4,
            _ => (appWidgetId * 10) + 9
        };
        return PendingIntent.GetBroadcast(context, requestCode, intent, flags)!;
    }
}
#endif
