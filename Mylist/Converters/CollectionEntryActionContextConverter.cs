using System;
using System.Globalization;
using System.Windows.Data;
using MyList.ViewModels;

namespace MyList.Converters;

public sealed class CollectionEntryActionContextConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var entry = values.Length > 0 ? values[0] as CollectionEntryViewModel : null;
        var collection = values.Length > 1 ? values[1] as CollectionViewModel : null;
        return new CollectionEntryActionContext(entry, collection);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
