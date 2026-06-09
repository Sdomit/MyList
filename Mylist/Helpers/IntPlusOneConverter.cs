using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyList.Helpers;

public sealed class IntPlusOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int index || index < 0)
        {
            return targetType == typeof(Visibility) ? Visibility.Collapsed : (object)string.Empty;
        }

        if (targetType == typeof(Visibility))
        {
            return index < 9 ? Visibility.Visible : Visibility.Collapsed;
        }

        return index < 9 ? (index + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
