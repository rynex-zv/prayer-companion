namespace Pray_Ad_Free.ViewModels;

public sealed class AdhanSoundOptionViewModel : ViewModelBase {
    private bool _isSelected;
    private bool _isPlaying;

    public AdhanSoundOptionViewModel(string key, string label, bool canPreview, bool isCustom) {
        Key = key;
        Label = label;
        CanPreview = canPreview;
        IsCustom = isCustom;
    }

    public string Key { get; }
    public string Label { get; }
    public bool CanPreview { get; }
    public bool IsCustom { get; }

    public bool IsSelected {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsPlaying {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }
}
