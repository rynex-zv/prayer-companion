namespace MauiWebber;

public interface IMauiWebberLogger {
    void Log(string name, string details);
    void LogException(Exception exception, string context);
}

public sealed class NullMauiWebberLogger : IMauiWebberLogger {
    public static NullMauiWebberLogger Instance { get; } = new();

    private NullMauiWebberLogger() {
    }

    public void Log(string name, string details) {
    }

    public void LogException(Exception exception, string context) {
    }
}
