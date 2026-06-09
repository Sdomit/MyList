using System;
using System.Globalization;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Helpers;

public sealed class HealthStateToIsDashedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ItemHealthState state && state == ItemHealthState.Unchecked;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
