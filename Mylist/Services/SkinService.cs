using System;
using System.Linq;
using System.Windows;
using MyList.Models;
using Application = System.Windows.Application;

namespace MyList.Services;

public sealed class SkinService
{
    public void ApplySkin(UiSkin skin)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var source = skin switch
        {
            UiSkin.MyList => "Resources/Skin.MyList.xaml",
            _ => "Resources/Skin.Windows11.xaml"
        };

        var dictionaries = app.Resources.MergedDictionaries;
        var existing = dictionaries.Where(IsSkinDictionary).ToList();
        foreach (var dictionary in existing)
        {
            dictionaries.Remove(dictionary);
        }

        dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
    }

    private static bool IsSkinDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return source.Replace('\\', '/').Contains("Resources/Skin.", StringComparison.OrdinalIgnoreCase);
    }
}
