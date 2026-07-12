using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pray_Ad_Free.Services;

/// <summary>UI-independent observable state used by both native application queries and XAML adapters.</summary>
public abstract class ObservableApplicationService : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
