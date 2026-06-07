using System;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class InlineMtabPathViewModel : ViewModelBase
{
    private string _pathText;
    private int _number;
    private double _lineX2;
    private double _lineY2;
    private double _nodeLeft;
    private double _nodeTop;

    public InlineMtabPathViewModel(string pathText)
    {
        _pathText = pathText;
    }

    public string PathText
    {
        get => _pathText;
        set
        {
            if (SetProperty(ref _pathText, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public int Number
    {
        get => _number;
        private set
        {
            if (SetProperty(ref _number, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public string Label
    {
        get
        {
            var trimmed = PathText.Trim().TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return $"folder {Number}";
            }

            var separatorIndex = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            var leaf = separatorIndex >= 0 && separatorIndex < trimmed.Length - 1
                ? trimmed[(separatorIndex + 1)..]
                : trimmed;
            return leaf.Length > 12 ? $"{leaf[..9]}..." : leaf;
        }
    }

    public double LineX2
    {
        get => _lineX2;
        private set => SetProperty(ref _lineX2, value);
    }

    public double LineY2
    {
        get => _lineY2;
        private set => SetProperty(ref _lineY2, value);
    }

    public double NodeLeft
    {
        get => _nodeLeft;
        private set => SetProperty(ref _nodeLeft, value);
    }

    public double NodeTop
    {
        get => _nodeTop;
        private set => SetProperty(ref _nodeTop, value);
    }

    public void UpdateLayout(int number, int count)
    {
        const double centerX = 130;
        const double centerY = 100;
        const double radiusX = 82;
        const double radiusY = 62;

        Number = number;
        var (x, y) = OrbitLayoutService.ComputePoint(number - 1, Math.Max(count, 3), centerX, centerY, radiusX, radiusY);
        LineX2 = x;
        LineY2 = y;
        NodeLeft = LineX2 - 16;
        NodeTop = LineY2 - 16;
    }
}
