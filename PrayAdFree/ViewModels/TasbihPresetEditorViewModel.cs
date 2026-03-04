using System.Collections.ObjectModel;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class TasbihPresetEditorViewModel : ViewModelBase {
    private string _name;
    private TasbihRepeatMode _repeatMode;

    public TasbihPresetEditorViewModel(string name, TasbihRepeatMode repeatMode, IEnumerable<TasbihItemEditorViewModel> items) {
        _name = name;
        _repeatMode = repeatMode;
        Items = new ObservableCollection<TasbihItemEditorViewModel>(items);
    }

    public string Name {
        get => _name;
        set {
            if (SetProperty(ref _name, value)) {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName {
        get => LocalizationManager.Translate(_name);
        set => Name = value;
    }

    public TasbihRepeatMode RepeatMode {
        get => _repeatMode;
        set => SetProperty(ref _repeatMode, value);
    }

    public ObservableCollection<TasbihItemEditorViewModel> Items { get; }

    public void RefreshDisplayName() {
        OnPropertyChanged(nameof(DisplayName));
    }
}
