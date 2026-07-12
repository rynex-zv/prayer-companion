namespace MauiWebber;

public static class MauiWebberEventHub {
    public static event EventHandler<object>? Published;
    public static void Publish(object value) => Published?.Invoke(null, value);
}
