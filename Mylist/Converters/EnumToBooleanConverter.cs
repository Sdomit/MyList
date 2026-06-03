using System;
using System.Globalization;
using System.Windows.Data;

namespace MyList.Converters;

public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is null)
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        if (value is bool isChecked && !isChecked)
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Enum.TryParse(enumType, parameter.ToString(), out var parsed)
            ? parsed
            : System.Windows.Data.Binding.DoNothing;
    }
}
