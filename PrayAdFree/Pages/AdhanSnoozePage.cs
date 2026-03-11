using Pray_Ad_Free.Services;
using Microsoft.Maui.Controls.Shapes;

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
        BackgroundColor = ThemeColor("#EAF4EF", "#08111D");

        var icon = new Border {
            WidthRequest = 78,
            HeightRequest = 78,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 39 },
            BackgroundColor = Color.FromArgb("#D1AD3A"),
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label {
                Text = "?",
                FontSize = 32,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }
        };

        var title = new Label {
            Text = LocalizationManager.Translate("SnoozePageTitle"),
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeColor("#0F172A", "#FFFFFF")
        };

        var subtitle = new Label {
            Text = string.Format(LocalizationManager.Translate("SnoozePageHint"), model.CurrentPrayerName),
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeColor("#46576A", "#B8C7D8")
        };

        var remaining = new Label {
            Text = string.Format(
                LocalizationManager.Translate("SnoozeRemainingToNextPrayer"),
                model.NextPrayerName,
                model.RemainingToNextPrayer),
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeColor("#5F7082", "#8CA0B6")
        };

        _delayLabel = new Label {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemeColor("#0F172A", "#EAF1FA")
        };

        _minusButton = new Button {
            Text = "-",
            FontSize = 22,
            WidthRequest = 56,
            HeightRequest = 56
        };
        _minusButton.Clicked += (_, _) => ChangeDelay(-1);

        _valueLabel = new Label {
            WidthRequest = 140,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 40,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeColor("#0F172A", "#FFFFFF")
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
            HeightRequest = 52,
            BackgroundColor = ThemeColor("#0F172A", "#FFFFFF"),
            TextColor = ThemeColor("#FFFFFF", "#0F172A"),
            FontAttributes = FontAttributes.Bold
        };
        confirmButton.Clicked += async (_, _) => await ConfirmAsync();

        var cancelButton = new Button {
            Text = LocalizationManager.Translate("SnoozePageCancel"),
            HeightRequest = 48,
            BackgroundColor = ThemeColor("#DCE9E2", "#132738"),
            TextColor = ThemeColor("#223341", "#C7D6E6")
        };
        cancelButton.Clicked += async (_, _) => await CloseAsync(navigateToHome: false);

        var contentStack = new VerticalStackLayout {
            Spacing = 16,
            MaximumWidthRequest = 480,
            HorizontalOptions = LayoutOptions.Fill,
            Children = {
                icon,
                title,
                subtitle,
                remaining,
                _delayLabel,
                valueRow,
                confirmButton,
                cancelButton
            }
        };

        var card = new Border {
            Padding = new Thickness(22),
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Stroke = ThemeColor("#BFD4CA", "#20344A"),
            StrokeThickness = 1,
            BackgroundColor = ThemeColor("#FFFFFF", "#102531"),
            Content = contentStack
        };

        var root = new Grid {
            Padding = new Thickness(24, 24, 24, 32),
            RowDefinitions = {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        root.Children.Add(card);
        Grid.SetRow(card, 1);

        Content = root;

        UpdateDelayUi();
    }

    private static Color ThemeColor(string lightHex, string darkHex) {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return Color.FromArgb(theme == AppTheme.Light ? lightHex : darkHex);
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
            await CloseAsync(navigateToHome: true).ConfigureAwait(false);
        }
    }

    private async Task CloseAsync(bool navigateToHome) => await MainThread.InvokeOnMainThreadAsync(async () => {
        var closed = false;

        try {
            if (Navigation.ModalStack.Count > 0 && ReferenceEquals(Navigation.ModalStack[^1], this)) {
                await Navigation.PopModalAsync();
                closed = true;
            } else {
                var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
                if (navigation?.ModalStack.Count > 0) {
                    await navigation.PopModalAsync();
                    closed = true;
                }
            }
        } catch {
        }

        if (navigateToHome && Shell.Current != null) {
            try {
                await Shell.Current.GoToAsync("//today");
                return;
            } catch {
            }
        }

        if (!closed) {
            try {
                Application.Current?.Quit();
            } catch {
            }
        }
    });
}
