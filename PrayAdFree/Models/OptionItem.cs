namespace Pray_Ad_Free.Models;

public sealed class OptionItem<T> {
    public OptionItem(T value, string label) {
        Value = value;
        Label = label;
    }

    public T Value { get; }
    public string Label { get; }
}
