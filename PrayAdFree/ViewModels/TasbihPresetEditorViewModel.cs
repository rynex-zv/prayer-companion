using System.Collections.ObjectModel;
using PrayAdFree.Core.Models;

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
        set => SetProperty(ref _name, value);
    }

    public TasbihRepeatMode RepeatMode {
        get => _repeatMode;
        set => SetProperty(ref _repeatMode, value);
    }

    public ObservableCollection<TasbihItemEditorViewModel> Items { get; }
}
