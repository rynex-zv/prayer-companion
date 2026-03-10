using System.ComponentModel;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Models;

public sealed class ReminderOffsetItem : INotifyPropertyChanged {
    private string _label;
    private OptionItem<AdhanReminderAlertType>? _alertType;

    public ReminderOffsetItem(int minutes, string label, OptionItem<AdhanReminderAlertType>? alertType = null) {
        Minutes = minutes;
        _label = label;
        _alertType = alertType;
    }

    public int Minutes { get; }

    public string Label {
        get => _label;
        set {
            if (string.Equals(_label, value, StringComparison.Ordinal)) {
                return;
            }

            _label = value;
            OnPropertyChanged(nameof(Label));
        }
    }

    public OptionItem<AdhanReminderAlertType>? AlertType {
        get => _alertType;
        set {
            if (Equals(_alertType, value)) {
                return;
            }

            _alertType = value;
            OnPropertyChanged(nameof(AlertType));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
