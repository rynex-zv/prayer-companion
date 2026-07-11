using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebContractExporter {
    public static object Export() {
        return new {
            schemaVersion = 1,
            generatedFrom = "PrayAdFree.Core",
            rpcMethods = RpcMethods
        };
    }

    public static IReadOnlyList<string> RpcMethods { get; } = new[] {
        "app.getShellSnapshot",
        "app.getLocalization",
        "app.getLanguageObject",
        "app.setLanguage",
        "app.setTheme",
        "app.navigate",
        "app.importState",
        "app.exportState",
        "today.getSnapshot",
        "today.refresh",
        "calendar.getSnapshot",
        "calendar.setMonth",
        "calendar.today",
        "calendar.nextMonth",
        "calendar.previousMonth",
        "qibla.getSnapshot",
        "qibla.updateHeading",
        "qibla.setHeadingMode",
        "qibla.adjustManualHeading",
        "qibla.commitManualHeading",
        "qibla.setDisplayMode",
        "qibla.setVisualFilter",
        "tasbih.getSnapshot",
        "tasbih.increment",
        "tasbih.reset",
        "tasbih.selectPreset",
        "alarm.getSnapshot",
        "alarm.snooze",
        "alarm.stop",
        "settings.getSnapshot",
        "settings.setField",
        "settings.patch",
        "settings.invoke",
        "onboarding.getSnapshot",
        "onboarding.complete",
        "mauiWebber.getRemoteUrl",
        "mauiWebber.setRemoteUrl",
        "mauiWebber.pullRemote",
        "mauiWebber.useEmbedded"
    };
}
