using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyList.Helpers;

public sealed class TrendDeltaToHealthBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is int i ? i : 0;
        var key = delta > 0
            ? "Token.MyList.HealthOkBrush"
            : delta < 0
                ? "Token.MyList.HealthBadBrush"
                : "Token.MyList.HealthUnknownBrush";

        if (System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Brush brush)
        {
            return brush;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
