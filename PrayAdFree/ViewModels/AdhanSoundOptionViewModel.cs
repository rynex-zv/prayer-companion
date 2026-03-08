namespace Pray_Ad_Free.ViewModels;

public sealed class AdhanSoundOptionViewModel : ViewModelBase {
    private bool _isSelected;
    private bool _isPlaying;

    public AdhanSoundOptionViewModel(string key, string label, bool canPreview) {
        Key = key;
        Label = label;
        CanPreview = canPreview;
    }

    public string Key { get; }
    public string Label { get; }
    public bool CanPreview { get; }

    public bool IsSelected {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsPlaying {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }
}
