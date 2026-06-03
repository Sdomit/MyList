using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MyList.Helpers;
using MyList.Models;

namespace MyList.Services;

public sealed class DuplicateDetectionService
{
    public IReadOnlyList<DuplicateGroup> BuildGroups(IEnumerable<ItemModel> items)
    {
        var map = new Dictionary<string, List<ItemModel>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var key = PathNormalizationHelper.GetPathKey(item.Path);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<ItemModel>();
                map[key] = list;
            }

            list.Add(item);
        }

        var groups = map.Values
            .Where(group => group.Count > 1)
            .Select(group =>
            {
                var sorted = group.OrderBy(item => item.CreatedDate).ToList();
                return new DuplicateGroup
                {
                    PathKey = PathNormalizationHelper.GetPathKey(sorted[0].Path) ?? string.Empty,
                    DisplayPath = sorted[0].Path,
                    Items = new ObservableCollection<ItemModel>(sorted),
                    SelectedSurvivor = sorted[0]
                };
            })
            .OrderBy(group => group.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups;
    }
}
