using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsAlarmRemindersPageA : ContentPage {
    public SettingsAlarmRemindersPageA() : this(ServiceHelper.GetService<AlarmRemindersViewModel>()) {
    }

    public SettingsAlarmRemindersPageA(AlarmRemindersViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
