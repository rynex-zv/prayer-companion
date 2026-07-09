namespace PrayAdFree.Core.Services;

public static class WebRpcErrorFormatter {
    public static string Clean(string? message) =>
        string.IsNullOrWhiteSpace(message) ? "Unknown web core error." : message.Split('\n')[0].Trim();
}
