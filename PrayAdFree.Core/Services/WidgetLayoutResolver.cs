using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WidgetLayoutResolver {
    public WidgetRenderTree Resolve(WidgetProfile profile, WidgetProjection projection, WidgetHostCapabilities host) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(host);

        var capacity = ResolveCapacity(profile.Density, host);
        var texts = new List<WidgetRenderText>();
        var rows = new List<WidgetRenderRow>();
        var actions = new List<WidgetRenderAction>();
        var omitted = new List<string>();
        var warnings = new List<string>();
        long? countdownTarget = null;
        double? progress = null;

        if (!host.SupportsFullColor) warnings.Add("host-forces-tinted-rendering");
        if (!host.SupportsBackgroundColor && !profile.Style.FollowAppTheme) warnings.Add("host-ignores-background-color");
        if (!host.SupportsBackgroundOpacity && profile.Style.BackgroundOpacity < 100) warnings.Add("host-ignores-background-opacity");
        if (projection.Status != "ready") {
            var errorText = string.IsNullOrWhiteSpace(projection.Error)
                ? WidgetProjectionFactory.Text("dataUnavailable", projection.Language)
                : projection.Error;
            var errorTexts = new List<WidgetRenderText> {
                new("error", errorText, "title", true, errorText)
            };
            if (projection.GeneratedAtUnixMilliseconds > 0) {
                var lastUpdate = $"{WidgetProjectionFactory.Text("lastUpdate", projection.Language)}: {DateTimeOffset.FromUnixTimeMilliseconds(projection.GeneratedAtUnixMilliseconds):g}";
                errorTexts.Add(new("lastUpdate", lastUpdate, "caption", false, lastUpdate));
            }
            return new WidgetRenderTree {
                ProfileId = profile.Id,
                ProfileRevision = profile.Revision,
                Status = "error",
                Error = projection.Error,
                IsRtl = projection.IsRtl,
                Family = host.Family,
                Style = profile.Style,
                Texts = errorTexts,
                Warnings = warnings
            };
        }

        foreach (var field in profile.Projection) {
            var usedBefore = texts.Count + rows.Count;
            var intentionallySuppressed = false;
            switch (field) {
                case "nextPrayerName":
                    AddText(texts, field, projection.NextPrayerName, "title", true);
                    break;
                case "nextPrayerTime":
                    AddText(texts, field, projection.NextPrayerTime, "time", true);
                    break;
                case "countdown":
                    countdownTarget = projection.NextPrayerAtUnixMilliseconds;
                    AddText(texts, field, WidgetProjectionFactory.Text("countdown", projection.Language), "countdown", false);
                    break;
                case "followingPrayer": {
                    var following = projection.PrayerRows.SkipWhile(item => !item.IsNext).Skip(1).FirstOrDefault();
                    if (following != null) AddRow(rows, field, following.Name, following.Time, false);
                    break;
                }
                case "prayerRows":
                    foreach (var row in projection.PrayerRows.Take(Math.Max(1, capacity - texts.Count - rows.Count))) AddRow(rows, $"prayer-{row.Id}", row.Name, row.Time, row.IsNext);
                    break;
                case "imsak": AddRow(rows, field, WidgetProjectionFactory.Text("imsak", projection.Language), projection.ImsakTime, false); break;
                case "iftar": AddRow(rows, field, WidgetProjectionFactory.Text("iftar", projection.Language), projection.IftarTime, false); break;
                case "fastingCountdown":
                    countdownTarget = projection.FastingTargetAtUnixMilliseconds;
                    AddText(texts, field, projection.FastingTargetName, "countdown", false);
                    break;
                case "hijriDate": AddText(texts, field, projection.HijriDate, "date", false); break;
                case "gregorianDate": AddText(texts, field, projection.GregorianDate, "date", false); break;
                case "location":
                    intentionallySuppressed = host.Surface == WidgetSurface.LockScreen && profile.Privacy.HideLocationOnLockScreen;
                    if (!intentionallySuppressed) AddText(texts, field, projection.LocationTitle, "caption", false);
                    break;
                case "locationSource":
                    intentionallySuppressed = host.Surface == WidgetSurface.LockScreen && profile.Privacy.HideLocationSourceOnLockScreen;
                    if (!intentionallySuppressed) AddText(texts, field, projection.LocationSource, "caption", false);
                    break;
                case "tasbihPreset": AddText(texts, field, projection.TasbihPresetName, "caption", false); break;
                case "tasbihText": AddText(texts, field, projection.TasbihText, "title", false); break;
                case "tasbihCount": AddText(texts, field, projection.TasbihCount.ToString(), "count", true); break;
                case "tasbihProgress":
                    progress = projection.TasbihTarget <= 0 ? 0 : Math.Clamp((double)projection.TasbihCount / projection.TasbihTarget, 0, 1);
                    AddText(texts, field, $"{projection.TasbihCount}/{projection.TasbihTarget}", "progress", false);
                    break;
                case "tasbihIncrement": AddAction(actions, host, "tasbih.increment", "+1", "prayadfree://tasbih/increment", WidgetProjectionFactory.Text("increment", projection.Language)); break;
                case "tasbihReset": AddAction(actions, host, "tasbih.reset", WidgetProjectionFactory.Text("reset", projection.Language), "prayadfree://tasbih/reset", WidgetProjectionFactory.Text("reset", projection.Language)); break;
                case "qiblaBearing": AddText(texts, field, $"{projection.QiblaBearingDegrees}°", "bearing", true); break;
                case "openQibla": AddAction(actions, host, "qibla.open", WidgetProjectionFactory.Text("qibla", projection.Language), "prayadfree://qibla", WidgetProjectionFactory.Text("qibla", projection.Language)); break;
            }
            if (texts.Count + rows.Count > capacity) {
                while (texts.Count + rows.Count > capacity) {
                    if (rows.Count > 0) rows.RemoveAt(rows.Count - 1); else texts.RemoveAt(texts.Count - 1);
                }
                omitted.Add(field);
            } else if (!intentionallySuppressed && usedBefore == texts.Count + rows.Count && field is not "tasbihIncrement" and not "tasbihReset" and not "openQibla") {
                omitted.Add(field);
            }
        }

        if (countdownTarget.HasValue && !host.SupportsLiveCountdown) warnings.Add("countdown-updates-at-host-schedule");
        var required = WidgetProfileService.Catalog.Single(item => item.Template == profile.Template).RequiredProjection;
        var missingRequired = required.Where(item => omitted.Contains(item, StringComparer.Ordinal)).ToArray();
        if (missingRequired.Length > 0) {
            var message = projection.Language == "ar"
                ? "حجم الأداة لا يتسع للعناصر المطلوبة."
                : "This widget size cannot fit the required content.";
            return new WidgetRenderTree {
                ProfileId = profile.Id,
                ProfileRevision = profile.Revision,
                Status = "error",
                Error = "required-content-overflow",
                IsRtl = projection.IsRtl,
                Family = host.Family,
                Style = profile.Style,
                Texts = [new WidgetRenderText("error", message, "title", true, message)],
                OmittedProjection = missingRequired,
                Warnings = warnings
            };
        }
        return new WidgetRenderTree {
            ProfileId = profile.Id,
            ProfileRevision = profile.Revision,
            IsRtl = projection.IsRtl,
            Family = host.Family,
            Style = profile.Style,
            Texts = texts,
            Rows = rows,
            Actions = actions.Take(host.MaxActions).ToArray(),
            CountdownTargetUnixMilliseconds = countdownTarget,
            Progress = progress,
            OmittedProjection = omitted.Distinct().ToArray(),
            Warnings = warnings
        };
    }

    private static int ResolveCapacity(WidgetDensity density, WidgetHostCapabilities host) {
        var familyCapacity = host.Family switch {
            WidgetFamily.Inline or WidgetFamily.Circular or WidgetFamily.Tiny => 2,
            WidgetFamily.Compact or WidgetFamily.Small => 4,
            WidgetFamily.Rectangular or WidgetFamily.Medium => 7,
            _ => 12
        };
        var densityCapacity = density switch {
            WidgetDensity.Compact => 4,
            WidgetDensity.Standard => 7,
            WidgetDensity.Detailed => 12,
            _ => familyCapacity
        };
        return Math.Max(1, Math.Min(host.MaxTextItems, Math.Min(familyCapacity, densityCapacity)));
    }

    private static void AddText(List<WidgetRenderText> target, string key, string? value, string role, bool required) {
        if (!string.IsNullOrWhiteSpace(value)) target.Add(new(key, value, role, required, value));
    }
    private static void AddRow(List<WidgetRenderRow> target, string key, string label, string value, bool highlighted) => target.Add(new(key, label, value, highlighted, $"{label}, {value}"));
    private static void AddAction(List<WidgetRenderAction> target, WidgetHostCapabilities host, string id, string label, string link, string accessibility) {
        if (host.IsAuthenticated && target.Count < host.MaxActions) target.Add(new(id, label, link, accessibility));
    }
}
