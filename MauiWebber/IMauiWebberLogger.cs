using System.Diagnostics;

namespace MauiWebber;

public interface IMauiWebberLogger {
    void Log(string name, string details);
    void LogException(Exception exception, string context);
    void LogInformation(string name);
}

public sealed class NullMauiWebberLogger : IMauiWebberLogger {
    public static NullMauiWebberLogger Instance { get; } = new();

    private NullMauiWebberLogger() {
    }

    public void Log( string name , string details ) {
        Debug.WriteLine( $"[{name}] {details}" );
    }

    public void LogException( Exception exception , string context ) {
        Debug.WriteLine(
            $"[{context}] {exception.GetType().Name}: {exception.Message}" );

        Debug.WriteLine( exception.StackTrace );
    }

    public void LogInformation( string name ) {
        Debug.WriteLine( $"[INFO] {name}" );
    }
}
