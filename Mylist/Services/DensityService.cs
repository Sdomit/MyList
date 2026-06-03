using System;
using System.Linq;
using System.Windows;
using MyList.Models;
using Application = System.Windows.Application;

namespace MyList.Services;

public sealed class DensityService
{
    public void ApplyDensity(UiDensity density)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var source = density switch
        {
            UiDensity.Compact => "Resources/Density.Compact.xaml",
            UiDensity.Spacious => "Resources/Density.Spacious.xaml",
            _ => "Resources/Density.Comfortable.xaml"
        };

        var dictionaries = app.Resources.MergedDictionaries;
        var existing = dictionaries.Where(IsDensityDictionary).ToList();
        foreach (var dictionary in existing)
        {
            dictionaries.Remove(dictionary);
        }

        dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
    }

    private static bool IsDensityDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return source.Replace('\\', '/').Contains("Resources/Density.", StringComparison.OrdinalIgnoreCase);
    }
}
