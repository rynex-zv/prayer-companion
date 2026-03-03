using System.ComponentModel;

namespace Pray_Ad_Free.Services;

public sealed class LocalizationResources : INotifyPropertyChanged {
    public static LocalizationResources Instance { get; } = new LocalizationResources();

    private LocalizationResources() {
        LocalizationManager.LanguageChanged += (_, _) => {
            OnPropertyChanged("Item[]");
            OnPropertyChanged(string.Empty);
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => LocalizationManager.Translate(key);

    private void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
