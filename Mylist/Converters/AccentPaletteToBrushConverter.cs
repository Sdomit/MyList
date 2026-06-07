using System;
using System.Globalization;
using MyList.Models;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using IValueConverter = System.Windows.Data.IValueConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MyList.Converters;

public sealed class AccentPaletteToBrushConverter : IValueConverter
{
    private static readonly Brush[] Swatches =
    {
        Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06))),
        Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))),
        Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))),
        Freeze(new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED))),
        Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x94, 0x88))),
        Freeze(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C))),
        Freeze(new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x48))),
        Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AccentPalette accent)
        {
            var index = (int)accent;
            if (index >= 0 && index < Swatches.Length)
            {
                return Swatches[index];
            }
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static Brush Freeze(SolidColorBrush brush)
    {
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
