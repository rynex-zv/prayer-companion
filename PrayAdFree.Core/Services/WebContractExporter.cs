using PrayAdFree.Core.Models;
using PrayAdFree.Core.Contracts;

namespace PrayAdFree.Core.Services;

public static class WebContractExporter {
    public const int SchemaVersion = 4;

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
        "onboarding.getSnapshot" or "mauiWebber.getRemoteUrl" => RpcOperationKind.Query,
        "mauiWebber.trace" or "mauiWebber.pullRemote" or "mauiWebber.useEmbedded" or "mauiWebber.clearSiteData" => RpcOperationKind.PlatformOperation,
        "app.importState" or "app.exportState" or "settings.invoke" or "settings.setField" or "settings.patch" => RpcOperationKind.CompatibilityAdapter,
        "app.navigate" => RpcOperationKind.Obsolete,
        _ => RpcOperationKind.Command
    };
}
