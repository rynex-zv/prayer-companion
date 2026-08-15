using PrayAdFree.Core.Models;
using PrayAdFree.Core.Contracts;

namespace PrayAdFree.Core.Services;

public static class WebContractExporter {
    public const int SchemaVersion = 6;

    public static object Export() {
        return new {
            schemaVersion = SchemaVersion,
            contractVersion = AppProtocol.ContractVersion,
            persistenceSchemaVersion = AppProtocol.PersistenceSchemaVersion,
            generatedFrom = "PrayAdFree.Core",
            rpcMethods = RpcMethods,
            rpcContracts = RpcContracts.Select(item => new {
                item.Name,
                kind = char.ToLowerInvariant(item.Kind.ToString()[0]) + item.Kind.ToString()[1..],
                item.Domain
            })
        };
    }

    public static IReadOnlyList<string> RpcMethods { get; } = new[] {
        "app.getShellSnapshot",
        "app.bootstrap",
        "app.getLocalization",
        "app.getLanguageObject",
        "app.setLanguage",
        "app.setTheme",
        "today.getSnapshot",
        "today.refresh",
        "calendar.getSnapshot",
        "calendar.setMonth",
        "calendar.today",
        "calendar.nextMonth",
        "calendar.previousMonth",
        "qibla.getSnapshot",
        "qibla.startSensor",
        "qibla.stopSensor",
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
        "tasbih.addPreset",
        "tasbih.updatePreset",
        "tasbih.removePreset",
        "tasbih.addItem",
        "tasbih.updateItem",
        "tasbih.moveItem",
        "tasbih.removeItem",
        "alarm.getSnapshot",
        "alarm.snooze",
        "alarm.stop",
        "alarm.test",
        "notification.test",
        "permissions.request",
        "permissions.requestAll",
        "location.refresh",
        "location.reverseGeocode",
        "adhan.sound.addCustom",
        "adhan.sound.preview",
        "adhan.sound.stopPreview",
        "adhan.sound.removeCustom",
        "external.openEmail",
        "external.call",
        "external.openUrl",
        "external.reportIssue",
        "settings.getSnapshot",
        "settings.update",
        "widgets.getCatalog",
        "widgets.getProfiles",
        "widgets.createProfile",
        "widgets.updateProfile",
        "widgets.duplicateProfile",
        "widgets.deleteProfile",
        "widgets.getPreview",
        "widgets.getInstalledInstances",
        "widgets.assignProfile",
        "onboarding.getSnapshot",
        "onboarding.complete",
        "automation.writeReports",
        "mauiWebber.getRemoteUrl",
        "mauiWebber.trace",
        "mauiWebber.setRemoteUrl",
        "mauiWebber.clearSiteData",
        "mauiWebber.pullRemote",
        "mauiWebber.useEmbedded"
    };

    public static IReadOnlyList<RpcContract> RpcContracts { get; } = RpcMethods
        .Select(name => new RpcContract(name, Classify(name), name.Split('.')[0]))
        .ToArray();

    public static RpcOperationKind Classify(string name) => name switch {
        "app.bootstrap" or "app.getShellSnapshot" or "app.getLocalization" or "app.getLanguageObject" or
        "today.getSnapshot" or "calendar.getSnapshot" or "qibla.getSnapshot" or
        "tasbih.getSnapshot" or "alarm.getSnapshot" or "settings.getSnapshot" or
        "widgets.getCatalog" or "widgets.getProfiles" or "widgets.getPreview" or "widgets.getInstalledInstances" or
        "onboarding.getSnapshot" or "mauiWebber.getRemoteUrl" => RpcOperationKind.Query,
        "alarm.test" or "notification.test" or
        "permissions.request" or "permissions.requestAll" or
        "location.refresh" or "location.reverseGeocode" or
        "adhan.sound.addCustom" or "adhan.sound.preview" or "adhan.sound.stopPreview" or "adhan.sound.removeCustom" or
        "external.openEmail" or "external.call" or "external.openUrl" or "external.reportIssue" or
        "automation.writeReports" or "mauiWebber.trace" or "mauiWebber.pullRemote" or "mauiWebber.useEmbedded" or "mauiWebber.clearSiteData" => RpcOperationKind.PlatformOperation,
        _ => RpcOperationKind.Command
    };
}
