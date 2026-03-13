using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsAlarmRemindersPage : ContentPage {
    public SettingsAlarmRemindersPage() : this(ServiceHelper.GetService<AlarmRemindersViewModel>()) {
    }

    public SettingsAlarmRemindersPage(AlarmRemindersViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
