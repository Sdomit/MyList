using System.Windows;

namespace MyList.Helpers;

public static class Spacing
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.RegisterAttached(
            "Value",
            typeof(double),
            typeof(Spacing),
            new FrameworkPropertyMetadata(0d));

    public static void SetValue(DependencyObject element, double value)
    {
        element.SetValue(ValueProperty, value);
    }

    public static double GetValue(DependencyObject element)
    {
        return (double)element.GetValue(ValueProperty);
    }
}
