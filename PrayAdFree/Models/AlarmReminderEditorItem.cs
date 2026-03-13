using System.ComponentModel;

namespace Pray_Ad_Free.Models;

public sealed class AlarmReminderEditorItem : INotifyPropertyChanged {
    private string _text;
    private bool _isEnabled;

    public AlarmReminderEditorItem(string id, string text, bool isEnabled, bool isBuiltIn) {
        Id = id;
        _text = text;
        _isEnabled = isEnabled;
        IsBuiltIn = isBuiltIn;
    }

    public string Id { get; }
    public bool IsBuiltIn { get; }

    public string Text {
        get => _text;
        set {
            if (string.Equals(_text, value, StringComparison.Ordinal)) {
                return;
            }

            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public bool IsEnabled {
        get => _isEnabled;
        set {
            if (_isEnabled == value) {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
