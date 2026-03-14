using Pray_Ad_Free.ViewModels;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Models;

public sealed class AppPermissionItemViewModel : ViewModelBase {
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _roleText = string.Empty;
    private string _fallbackText = string.Empty;
    private string _statusText = string.Empty;
    private string _actionText = string.Empty;
    private bool _isGranted;
    private bool _isCritical;

    public AppPermissionItemViewModel(AppPermissionKind kind) {
        Kind = kind;
    }

    public AppPermissionKind Kind { get; }

    public string Title {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string RoleText {
        get => _roleText;
        set => SetProperty(ref _roleText, value);
    }

    public string FallbackText {
        get => _fallbackText;
        set => SetProperty(ref _fallbackText, value);
    }

    public string StatusText {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ActionText {
        get => _actionText;
        set => SetProperty(ref _actionText, value);
    }

    public bool IsGranted {
        get => _isGranted;
        set => SetProperty(ref _isGranted, value);
    }

    public bool IsCritical {
        get => _isCritical;
        set => SetProperty(ref _isCritical, value);
    }
}
