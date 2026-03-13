using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Pages;

public sealed record AdhanSnoozePageModel(
    string PrayerClock,
    string DelayFromBase,
    string PrayerName,
    string ReminderText,
    int MinDelayMinutes,
    int MaxDelayMinutes,
    int InitialDelayMinutes);

public sealed class AdhanSnoozePage : ContentPage {
    private readonly Func<Task> _onStop;
    private readonly Func<int, Task<bool>> _onSnooze;
    private readonly int _minDelayMinutes;
    private readonly int _maxDelayMinutes;
    private readonly Label _snoozeValueLabel;
    private readonly Button _minusButton;
    private readonly Button _plusButton;
    private readonly Button _snoozeButton;
    private bool _isSubmitting;
    private int _selectedDelayMinutes;

    public AdhanSnoozePage(
        AdhanSnoozePageModel model,
        Func<Task> onStop,
        Func<int, Task<bool>> onSnooze) {
        _onStop = onStop;
        _onSnooze = onSnooze;
        _minDelayMinutes = model.MinDelayMinutes;
        _maxDelayMinutes = model.MaxDelayMinutes;
        _selectedDelayMinutes = Math.Clamp(model.InitialDelayMinutes, _minDelayMinutes, _maxDelayMinutes);

        Title = LocalizationManager.Translate("AlarmScreenTitle");
        Background = new LinearGradientBrush(
            new GradientStopCollection {
                new GradientStop(ThemeColor("#2E8E83", "#0C2032"), 0.0f),
                new GradientStop(ThemeColor("#2A6D7C", "#12304A"), 0.55f),
                new GradientStop(ThemeColor("#5A5D96", "#1C2743"), 1.0f)
            },
            new Point(0, 0),
            new Point(1, 1));

        var clockLabel = new Label {
            Text = model.PrayerClock,
            FontSize = 76,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.White
        };

        var delayLabel = new Label {
            Text = model.DelayFromBase,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#DDF0FF")
        };

        var prayerNameLabel = new Label {
            Text = model.PrayerName,
            FontSize = 32,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.White
        };

        var reminderLabel = new Label {
            Text = model.ReminderText,
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#ECF7FF"),
            IsVisible = !string.IsNullOrWhiteSpace(model.ReminderText),
            Margin = new Thickness(8, 18, 8, 10)
        };

        var actionRow = new Grid {
            ColumnDefinitions = new ColumnDefinitionCollection {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 0,
            Padding = new Thickness(0)
        };

        _snoozeButton = new Button {
            Text = LocalizationManager.Translate("AlarmSnoozeButton"),
            HeightRequest = 60,
            CornerRadius = 28,
            BackgroundColor = Color.FromArgb("#355571"),
            TextColor = Colors.White
        };
        _snoozeButton.Clicked += async (_, _) => await SnoozeAsync();

        var divider = new BoxView {
            WidthRequest = 1,
            Color = Color.FromArgb("#78A5C7"),
            Margin = new Thickness(0, 14)
        };

        var stopButton = new Button {
            Text = LocalizationManager.Translate("AlarmStopButton"),
            HeightRequest = 60,
            CornerRadius = 28,
            BackgroundColor = Color.FromArgb("#355571"),
            TextColor = Colors.White
        };
        stopButton.Clicked += async (_, _) => await StopAsync();

        actionRow.Children.Add(_snoozeButton);
        Grid.SetColumn(_snoozeButton, 0);
        actionRow.Children.Add(divider);
        Grid.SetColumn(divider, 1);
        actionRow.Children.Add(stopButton);
        Grid.SetColumn(stopButton, 2);

        _minusButton = new Button {
            Text = "-",
            WidthRequest = 52,
            HeightRequest = 52,
            CornerRadius = 26,
            BackgroundColor = Color.FromArgb("#40617D"),
            TextColor = Colors.White,
            FontSize = 28
        };
        _minusButton.Clicked += (_, _) => ChangeDelay(-1);

        _snoozeValueLabel = new Label {
            WidthRequest = 88,
            FontSize = 30,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = Colors.White
        };

        _plusButton = new Button {
            Text = "+",
            WidthRequest = 52,
            HeightRequest = 52,
            CornerRadius = 26,
            BackgroundColor = Color.FromArgb("#40617D"),
            TextColor = Colors.White,
            FontSize = 28
        };
        _plusButton.Clicked += (_, _) => ChangeDelay(1);

        var adjustRow = new HorizontalStackLayout {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 10,
            Children = { _minusButton, _snoozeValueLabel, _plusButton }
        };

        var root = new Grid {
            Padding = new Thickness(18, 28),
            RowDefinitions = new RowDefinitionCollection {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Children.Add(clockLabel);
        Grid.SetRow(clockLabel, 0);
        root.Children.Add(delayLabel);
        Grid.SetRow(delayLabel, 1);
        root.Children.Add(prayerNameLabel);
        Grid.SetRow(prayerNameLabel, 2);
        root.Children.Add(reminderLabel);
        Grid.SetRow(reminderLabel, 3);
        root.Children.Add(actionRow);
        Grid.SetRow(actionRow, 4);
        root.Children.Add(adjustRow);
        Grid.SetRow(adjustRow, 5);

        Content = root;
        UpdateDelayUi();
    }

    private static Color ThemeColor(string lightHex, string darkHex) {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return Color.FromArgb(theme == AppTheme.Light ? lightHex : darkHex);
    }

    private void ChangeDelay(int delta) {
        if (_isSubmitting || _maxDelayMinutes < _minDelayMinutes) {
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
        _snoozeValueLabel.Text = $"+{_selectedDelayMinutes}";
        var canSnooze = _maxDelayMinutes >= _minDelayMinutes;
        _minusButton.IsEnabled = canSnooze && !_isSubmitting && _selectedDelayMinutes > _minDelayMinutes;
        _plusButton.IsEnabled = canSnooze && !_isSubmitting && _selectedDelayMinutes < _maxDelayMinutes;
        _snoozeButton.IsEnabled = canSnooze && !_isSubmitting;
    }

    private async Task StopAsync() {
        if (_isSubmitting) {
            return;
        }

        _isSubmitting = true;
        UpdateDelayUi();
        try {
            await _onStop().ConfigureAwait(false);
        } finally {
            await CloseApplicationAsync().ConfigureAwait(false);
        }
    }

    private async Task SnoozeAsync() {
        if (_isSubmitting) {
            return;
        }

        _isSubmitting = true;
        UpdateDelayUi();
        try {
            await _onSnooze(_selectedDelayMinutes).ConfigureAwait(false);
        } finally {
            await CloseApplicationAsync().ConfigureAwait(false);
        }
    }

    private async Task CloseApplicationAsync() => await MainThread.InvokeOnMainThreadAsync(async () => {
        try {
            if (Navigation.ModalStack.Count > 0 && ReferenceEquals(Navigation.ModalStack[^1], this)) {
                await Navigation.PopModalAsync();
            } else {
                var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
                if (navigation?.ModalStack.Count > 0) {
                    await navigation.PopModalAsync();
                }
            }
        } catch {
        }

        try {
            Application.Current?.Quit();
        } catch {
        }
    });
}
