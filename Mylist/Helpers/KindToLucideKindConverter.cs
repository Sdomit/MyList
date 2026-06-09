using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MahApps.Metro.IconPacks;
using MyList.Models;

namespace MyList.Helpers;

public sealed class KindToLucideKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ContentKind.File => PackIconLucideKind.File,
            ContentKind.Folder => PackIconLucideKind.Folder,
            ContentKind.Mtab => PackIconLucideKind.Link,
            ContentKind.Clip => PackIconLucideKind.ClipboardCheck,
            ContentKind.Action => PackIconLucideKind.Zap,
            _ => DependencyProperty.UnsetValue,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
