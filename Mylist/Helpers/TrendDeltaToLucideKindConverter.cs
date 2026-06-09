using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

namespace MyList.Helpers;

public sealed class TrendDeltaToLucideKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is int i ? i : 0;
        return delta > 0
            ? PackIconLucideKind.TrendingUp
            : delta < 0
                ? PackIconLucideKind.TrendingDown
                : PackIconLucideKind.Minus;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
