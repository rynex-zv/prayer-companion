using Microsoft.Extensions.DependencyInjection;

namespace Pray_Ad_Free.Services;

public static class ServiceHelper {
    public static T GetService<T>() where T : notnull {
        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? Pray_Ad_Free.App.Services;
        if (services == null) {
            throw new InvalidOperationException("Service provider not available.");
        }

        return services.GetRequiredService<T>();
    }
}
