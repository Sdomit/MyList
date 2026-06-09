using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;
using MyList.Models;

namespace MyList.Helpers;

public sealed class TerminalProfileToLucideKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TerminalProfile.Default => PackIconLucideKind.Square,
            TerminalProfile.WindowsTerminal => PackIconLucideKind.Terminal,
            TerminalProfile.PowerShell => PackIconLucideKind.Code,
            TerminalProfile.Cmd => PackIconLucideKind.Terminal,
            _ => PackIconLucideKind.Square,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
