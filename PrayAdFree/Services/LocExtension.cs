using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Pray_Ad_Free.Services;

[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class LocExtension : IMarkupExtension {
    public string Key { get; set; } = "";

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) {
        return new Binding($"[{Key}]", source: LocalizationResources.Instance);
    }
}
