using System;
using System.Globalization;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Converters;

public sealed class UiSkinToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            UiSkin.Windows11 => "Windows 11",
            UiSkin.MyList => "MyList",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
