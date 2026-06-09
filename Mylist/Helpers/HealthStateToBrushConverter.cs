using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Helpers;

public sealed class HealthStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ItemHealthState state
            ? state switch
            {
                ItemHealthState.Healthy => "Token.StatusSuccessBrush",
                ItemHealthState.Offline => "Token.StatusWarningBrush",
                ItemHealthState.Missing => "Token.StatusDangerBrush",
                ItemHealthState.PermissionDenied => "Token.StatusDangerBrush",
                ItemHealthState.Unchecked => "Token.StatusInfoBrush",
                _ => "Token.StatusInfoBrush",
            }
            : "Token.StatusInfoBrush";

        if (System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Brush brush)
        {
            return brush;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
