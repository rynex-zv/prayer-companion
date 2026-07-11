using MauiWebber;

namespace Pray_Ad_Free.Services;

public sealed class MauiWebberAppLogger : IMauiWebberLogger {
    private readonly IAppLogger _logger;

    public MauiWebberAppLogger(IAppLogger logger) {
        _logger = logger;
    }

    public void Log(string name, string details) {
        _logger.LogEvent($"MauiWebber.{name}", details);
    }

    public void LogException(Exception exception, string context) {
        _logger.LogException(exception, context);
    }

    public void LogInformation( string name ) {
        _logger.LogEvent( $"MauiWebber.{name}" , "Information" );
    }
}
