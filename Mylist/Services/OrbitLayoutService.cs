using System;
using System.Collections.Generic;

namespace MyList.Services;

public static class OrbitLayoutService
{
    public static IReadOnlyList<(double X, double Y)> Compute(int count, double centerX, double centerY, double radiusX, double radiusY)
    {
        var safeCount = Math.Max(count, 1);
        var result = new List<(double, double)>(safeCount);
        for (var i = 0; i < safeCount; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / safeCount;
            result.Add((centerX + radiusX * Math.Cos(angle), centerY + radiusY * Math.Sin(angle)));
        }

        return result;
    }

    public static (double X, double Y) ComputePoint(int index, int count, double centerX, double centerY, double radiusX, double radiusY)
    {
        var safeCount = Math.Max(count, 1);
        var angle = -Math.PI / 2 + 2 * Math.PI * index / safeCount;
        return (centerX + radiusX * Math.Cos(angle), centerY + radiusY * Math.Sin(angle));
    }
}
