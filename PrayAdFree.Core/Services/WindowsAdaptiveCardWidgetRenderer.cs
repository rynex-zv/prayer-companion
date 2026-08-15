using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WindowsAdaptiveCardWidgetRenderer {
    public string Render(WidgetRenderTree tree) {
        ArgumentNullException.ThrowIfNull(tree);
        var body = new List<WindowsAdaptiveCardTextBlock>();
        if (tree.Status != "ready") {
            if (tree.Texts.Count > 0) {
                foreach (var text in tree.Texts) body.Add(Text(text.Text, ResolveSize(text.Role), text.Required, true));
            } else {
                body.Add(Text(tree.Error, "Medium", true, true));
            }
        } else {
            foreach (var text in tree.Texts) body.Add(Text(text.Text, ResolveSize(text.Role), text.Required, true));
            foreach (var row in tree.Rows) body.Add(Text($"{row.Label}  {row.Value}", "Small", row.Highlighted, true));
        }

        var actions = new List<WindowsAdaptiveCardAction>();
        foreach (var action in tree.Actions) {
            actions.Add(new WindowsAdaptiveCardAction {
                Title = action.Label,
                Url = action.DeepLink,
                Tooltip = action.AccessibilityLabel
            });
        }
        return System.Text.Json.JsonSerializer.Serialize(new WindowsAdaptiveCardDocument {
            Rtl = tree.IsRtl,
            Body = body,
            Actions = actions
        }, CoreJsonContext.Default.WindowsAdaptiveCardDocument);
    }

    private static WindowsAdaptiveCardTextBlock Text(string value, string size, bool emphasized, bool wrap) => new() {
        Text = value,
        Size = size,
        Weight = emphasized ? "Bolder" : "Default",
        Wrap = wrap
    };

    private static string ResolveSize(string role) => role is "title" or "time" or "bearing" ? "Large" : "Default";
}
