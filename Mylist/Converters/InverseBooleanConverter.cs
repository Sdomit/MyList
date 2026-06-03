using System;
using System.Globalization;
using System.Windows.Data;

namespace MyList.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : System.Windows.Data.Binding.DoNothing;
}
