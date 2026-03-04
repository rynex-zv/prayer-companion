using System.Globalization;
using Microsoft.Maui.Controls;

namespace Pray_Ad_Free.Converters;

public sealed class SelectionMatchConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values.Length < 2) {
            return false;
        }

        var selected = values[0];
        var current = values[1];
        if (selected == null || current == null) {
            return false;
        }

        return Equals(selected, current);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}
