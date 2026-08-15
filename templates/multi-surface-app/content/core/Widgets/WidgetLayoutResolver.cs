namespace TemplateApp.Core.Widgets;

public sealed class WidgetLayoutResolver {
    public WidgetRenderTree Resolve(WidgetProfile profile, WidgetProjection projection, WidgetHostCapabilities host) {
        if (projection.Status != "ready") {
            return new(profile.Id, profile.Revision, "error", projection.Error, host.Family, [], [], null);
        }

        var capacity = Math.Max(1, host.MaxItems);
        var items = new List<WidgetRenderItem>();
        var omitted = new List<string>();
        foreach (var key in profile.Projection) {
            if (items.Count >= capacity) { omitted.Add(key); continue; }
            if (host.Surface == "lock-screen" && profile.HidePrivateDataOnLockScreen && key.Contains("location", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (projection.Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
                items.Add(new("text", key, key, value, $"{key}, {value}"));
            } else {
                omitted.Add(key);
            }
        }
        var missingRequired = profile.RequiredProjection.Where(key => omitted.Contains(key, StringComparer.Ordinal)).ToArray();
        if (missingRequired.Length > 0) {
            return new(profile.Id, profile.Revision, "error", "required-content-overflow", host.Family,
                [new("text", "error", "Error", "This widget size cannot fit the required content.", "This widget size cannot fit the required content.")],
                missingRequired,
                null);
        }
        return new(profile.Id, profile.Revision, "ready", "", host.Family, items, omitted, projection.TargetUnixMilliseconds);
    }
}

public interface IWidgetRenderer<out TResult> {
    TResult Render(WidgetRenderTree tree, WidgetHostCapabilities host);
}
