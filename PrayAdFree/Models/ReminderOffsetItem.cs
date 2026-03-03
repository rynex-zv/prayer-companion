namespace Pray_Ad_Free.Models;

public sealed class ReminderOffsetItem {
    public ReminderOffsetItem(int minutes, string label) {
        Minutes = minutes;
        Label = label;
    }

    public int Minutes { get; }
    public string Label { get; }
}
