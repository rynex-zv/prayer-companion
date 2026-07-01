using MauiWebber;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Pages;

public sealed class TodayWebPage : MauiWebberPage {
    public TodayWebPage(MauiWebberUpdater updater, WebAppRpcHandler handler, IMauiWebberLogger logger)
        : base(updater, handler, logger) {
        Title = LocalizationManager.Translate("AppTitle");
        _ = handler.PreloadAsync();
    }
}
