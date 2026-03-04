namespace Pray_Ad_Free.Services;

public interface IAppLogger {
    void LogException(Exception exception, string context);
    void LogEvent(string name, string details);
}
