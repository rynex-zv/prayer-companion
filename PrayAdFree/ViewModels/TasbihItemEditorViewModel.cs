using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class TasbihItemEditorViewModel : ViewModelBase {
    private string _text;
    private int _targetCount;
    private int _startIndex;

    public TasbihItemEditorViewModel(string text, int targetCount) {
        _text = text;
        _targetCount = targetCount;
    }

    public string Text {
        get => _text;
        set {
            if (SetProperty(ref _text, value)) {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText {
        get => LocalizationManager.Translate(_text);
        set => Text = value;
    }

    public int TargetCount {
        get => _targetCount;
        set => SetProperty(ref _targetCount, value);
    }

    public int StartIndex {
        get => _startIndex;
        set => SetProperty(ref _startIndex, value);
    }

    public void RefreshDisplayText() {
        OnPropertyChanged(nameof(DisplayText));
    }
}
