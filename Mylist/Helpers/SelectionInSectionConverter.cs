using System;
using System.Globalization;
using System.Windows.Data;
using MyList.ViewModels;

namespace MyList.Helpers;

public sealed class SelectionInSectionConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return null;
        }

        var focused = values[0] as IPaletteRow;
        var section = values[1] as PaletteSection;
        if (focused is null || section is null)
        {
            return null;
        }

        return section.Rows.Contains(focused) ? focused : null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
}
