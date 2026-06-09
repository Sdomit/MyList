using System;
using System.Globalization;
using System.Windows.Data;
using MyList.Models;

namespace MyList.Helpers;

public sealed class TrajectoryBarTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TrajectoryBar bar)
        {
            return System.Windows.Data.Binding.DoNothing;
        }

        var when = bar.DayOffset switch
        {
            0 => "Today",
            1 => "Yesterday",
            _ => string.Create(CultureInfo.CurrentCulture, $"{bar.DayOffset} days ago"),
        };

        var opens = bar.AccessCount == 1 ? "1 open" : string.Create(CultureInfo.CurrentCulture, $"{bar.AccessCount} opens");
        return bar.AccessCount == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{when} — no opens")
            : string.Create(CultureInfo.CurrentCulture, $"{when} — {opens}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
