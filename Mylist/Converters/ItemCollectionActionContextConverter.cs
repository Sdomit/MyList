using System;
using System.Globalization;
using System.Windows.Data;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Converters;

public sealed class ItemCollectionActionContextConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not ItemModel item)
        {
            return null;
        }

        var collection = values.Length > 1 ? values[1] as CollectionViewModel : null;
        return new ItemCollectionActionContext(item, collection);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
