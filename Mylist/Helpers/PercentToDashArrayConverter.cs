using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MyList.Helpers;

public sealed class PercentToDashArrayConverter : IValueConverter
{
    private const double Circumference = Math.PI * 22.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var score = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0.0,
        };

        score = Math.Clamp(score, 0.0, 1.0);
        var filled = score * Circumference;
        var empty = Circumference - filled;
        return new DoubleCollection { filled, empty };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
