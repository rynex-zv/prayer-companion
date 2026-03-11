using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Pages;

public sealed record AdhanSnoozePageModel(
    string CurrentPrayerName,
    string NextPrayerName,
    string RemainingToNextPrayer,
    int MinDelayMinutes,
    int MaxDelayMinutes,
    int InitialDelayMinutes);

public sealed class AdhanSnoozePage : ContentPage {
    private readonly Func<int, Task<bool>> _onConfirm;
    private readonly int _minDelayMinutes;
    private readonly int _maxDelayMinutes;
    private readonly Label _delayLabel;
    private readonly Button _minusButton;
    private readonly Button _plusButton;
    private readonly Label _valueLabel;
    private bool _isSubmitting;
    private int _selectedDelayMinutes;

    public AdhanSnoozePage(AdhanSnoozePageModel model, Func<int, Task<bool>> onConfirm) {
        _onConfirm = onConfirm;
        _minDelayMinutes = model.MinDelayMinutes;
        _maxDelayMinutes = model.MaxDelayMinutes;
        _selectedDelayMinutes = Math.Clamp(model.InitialDelayMinutes, _minDelayMinutes, _maxDelayMinutes);

        Title = LocalizationManager.Translate("SnoozePageTitle");
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#0F172A")
            : Color.FromArgb("#F7F3EA");

        var title = new Label {
            Text = LocalizationManager.Translate("SnoozePageTitle"),
            FontSize = 22,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        };

        var subtitle = new Label {
            Text = string.Format(
                LocalizationManager.Translate("SnoozePageHint"),
                model.CurrentPrayerName),
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#475569")
        };

        var remaining = new Label {
            Text = string.Format(
                LocalizationManager.Translate("SnoozeRemainingToNextPrayer"),
                model.NextPrayerName,
                model.RemainingToNextPrayer),
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#94A3B8") : Color.FromArgb("#334155")
        };

        _delayLabel = new Label {
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#E2E8F0") : Color.FromArgb("#1E293B")
        };

        _minusButton = new Button {
            Text = "-",
            FontSize = 22,
            WidthRequest = 56,
            HeightRequest = 56
        };
        _minusButton.Clicked += (_, _) => ChangeDelay(-1);

        _valueLabel = new Label {
            WidthRequest = 120,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        };

        _plusButton = new Button {
            Text = "+",
            FontSize = 22,
            WidthRequest = 56,
            HeightRequest = 56
        };
        _plusButton.Clicked += (_, _) => ChangeDelay(1);

        var valueRow = new HorizontalStackLayout {
            Spacing = 14,
            HorizontalOptions = LayoutOptions.Center,
            Children = { _minusButton, _valueLabel, _plusButton }
        };

        var confirmButton = new Button {
            Text = LocalizationManager.Translate("SnoozePageConfirm"),
            HeightRequest = 48
        };
        confirmButton.Clicked += async (_, _) => await ConfirmAsync().ConfigureAwait(false);

        var cancelButton = new Button {
            Text = LocalizationManager.Translate("SnoozePageCancel"),
            HeightRequest = 44
        };
        cancelButton.Clicked += async (_, _) => await CloseAsync().ConfigureAwait(false);

        Content = new ScrollView {
            Content = new VerticalStackLayout {
                Spacing = 16,
                Padding = new Thickness(24, 32),
                Children = {
                    title,
                    subtitle,
                    remaining,
                    _delayLabel,
                    valueRow,
                    confirmButton,
                    cancelButton
                }
            }
        };

        UpdateDelayUi();
    }

    private void ChangeDelay(int delta) {
        if (_isSubmitting) {
            return;
        }

        var next = Math.Clamp(_selectedDelayMinutes + delta, _minDelayMinutes, _maxDelayMinutes);
        if (next == _selectedDelayMinutes) {
            return;
        }

        _selectedDelayMinutes = next;
        UpdateDelayUi();
    }

    private void UpdateDelayUi() {
        _valueLabel.Text = _selectedDelayMinutes.ToString();
        _delayLabel.Text = string.Format(
            LocalizationManager.Translate("SnoozeDelayLabel"),
            _selectedDelayMinutes);
        _minusButton.IsEnabled = !_isSubmitting && _selectedDelayMinutes > _minDelayMinutes;
        _plusButton.IsEnabled = !_isSubmitting && _selectedDelayMinutes < _maxDelayMinutes;
    }

    private async Task ConfirmAsync() {
        if (_isSubmitting) {
            return;
        }

        _isSubmitting = true;
        UpdateDelayUi();
        try {
            await _onConfirm(_selectedDelayMinutes).ConfigureAwait(false);
        } finally {
            await CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task CloseAsync() {
        await MainThread.InvokeOnMainThreadAsync(async () => {
            var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navigation != null) {
                await navigation.PopModalAsync();
            }
        });
    }
}
