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
        set => SetProperty(ref _text, value);
    }

    public int TargetCount {
        get => _targetCount;
        set => SetProperty(ref _targetCount, value);
    }

    public int StartIndex {
        get => _startIndex;
        set => SetProperty(ref _startIndex, value);
    }
}
