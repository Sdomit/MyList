using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Helpers;

public sealed class KindToBadgeLetterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ContentKind.File => "F",
            ContentKind.Folder => "D",
            ContentKind.Mtab => "M",
            ContentKind.Clip => "C",
            ContentKind.Action => "A",
            _ => DependencyProperty.UnsetValue,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
