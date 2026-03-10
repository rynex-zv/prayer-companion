using System;
using System.Collections.Generic;

namespace PrayAdFree.Core.Services;

public static class WindowsNotificationActionParser {
    public const string StopActionToken = "stop_adhan";
    public const string ControlSourceToken = "adhan_control";
    public const string ControlNotificationTag = "adhan_playback_control";

    public static bool ShouldStopAdhan(string? argument, IReadOnlyDictionary<string, string?>? arguments = null) {
        var normalized = (argument ?? string.Empty).Trim();
        if (normalized.Length > 0) {
            if (normalized.Equals(ControlSourceToken, StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals($"source={ControlSourceToken}", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains($"source={ControlSourceToken}", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(StopActionToken, StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals($"action={StopActionToken}", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains($"action={StopActionToken}", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        if (arguments == null || arguments.Count == 0) {
            return false;
        }

        if (arguments.TryGetValue("source", out var sourceValue) &&
            sourceValue != null &&
            sourceValue.Equals(ControlSourceToken, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (arguments.TryGetValue("action", out var actionValue) &&
            actionValue != null &&
            actionValue.Equals(StopActionToken, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }
}
