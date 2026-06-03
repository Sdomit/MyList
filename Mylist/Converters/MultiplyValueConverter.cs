using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace MyList.Converters;

public sealed class MultiplyValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var factor = value is double scale && !double.IsNaN(scale) && !double.IsInfinity(scale)
            ? scale
            : 1.0;

        if (targetType == typeof(Thickness))
        {
            return ScaleThickness(parameter, factor);
        }

        var baseValue = ParseDouble(parameter, 0d);
        return baseValue * factor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType == typeof(Thickness))
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        var baseValue = ParseDouble(parameter, 0d);
        if (baseValue == 0d || value is not double result)
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        return result / baseValue;
    }

    private static double ParseDouble(object? parameter, double fallback)
    {
        if (parameter is null)
        {
            return fallback;
        }

        if (parameter is double numericParameter)
        {
            return numericParameter;
        }

        return double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static Thickness ScaleThickness(object? parameter, double factor)
    {
        if (parameter is null)
        {
            return new Thickness();
        }

        var text = parameter.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Thickness();
        }

        var parts = text
            .Split(',')
            .Select(part => ParseDouble(part.Trim(), 0d))
            .ToArray();

        return parts.Length switch
        {
            1 => new Thickness(parts[0] * factor),
            2 => new Thickness(parts[0] * factor, parts[1] * factor, parts[0] * factor, parts[1] * factor),
            4 => new Thickness(parts[0] * factor, parts[1] * factor, parts[2] * factor, parts[3] * factor),
            _ => new Thickness()
        };
    }
}
