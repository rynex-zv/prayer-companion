using MauiWebber;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Pages;

public sealed class TodayWebPage : MauiWebberPage {
    public TodayWebPage(MauiWebberUpdater updater, TodayWebRpcHandler handler, IMauiWebberLogger logger)
        : base(updater, handler, logger) {
        Title = LocalizationManager.Translate("Today");
        _ = handler.PreloadAsync();
    }
}
