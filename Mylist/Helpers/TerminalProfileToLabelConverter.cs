using System;
using System.Globalization;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Helpers;

public sealed class TerminalProfileToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TerminalProfile.Default => "Default",
            TerminalProfile.WindowsTerminal => "Terminal",
            TerminalProfile.PowerShell => "PowerShell",
            TerminalProfile.Cmd => "Cmd",
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
