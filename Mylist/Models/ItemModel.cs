using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using MyList.Helpers;

namespace MyList.Models;

public sealed class ItemModel : ObservableObject
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private ItemType _type;
    private string? _iconPath;
    private bool _useSystemIcon = true;
    private bool _isFavorite;
    private bool _isPinned;
    private DateTime? _pinnedAtUtc;
    private DateTime _lastOpenedDate;
    private ItemHealthState _healthState;
    private ImageSource? _icon;
    private ItemLaunchProfile _launchProfile = new();
    private bool _isClipboardText;
    private bool _isClipboardImage;
    private bool _isActionItem;
    private ActionKind _actionKind;
    private string _clipboardContent = string.Empty;
    private string _clipboardImageAssetPath = string.Empty;
    private int _clipboardImagePixelWidth;
    private int _clipboardImagePixelHeight;
    private string _actionContent = string.Empty;
    private bool _isMtab;
    private ObservableCollection<string> _tags = new();
    private ObservableCollection<Guid> _mtabMemberIds = new();
    private ObservableCollection<string> _mtabPaths = new();
    private string _mtabSearchHint = string.Empty;
    private string? _searchContentCache;
    private PointCollection? _trajectoryPointsCache;
    private IReadOnlyList<TrajectoryBar>? _trajectoryBarsCache;
    private int[]? _last14DayBucketsCache;

    public ItemModel()
    {
        _tags.CollectionChanged += OnTagsChanged;
        _mtabMemberIds.CollectionChanged += OnMtabMemberIdsChanged;
        _mtabPaths.CollectionChanged += OnMtabPathsChanged;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                InvalidateSearchContent();
            }
        }
    }

    public string Path
    {
        get => _path;
        set
        {
            if (SetProperty(ref _path, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public ItemType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public bool UseSystemIcon
    {
        get => _useSystemIcon;
        set => SetProperty(ref _useSystemIcon, value);
    }

    public ObservableCollection<string> Tags
    {
        get => _tags;
        set
        {
            if (ReferenceEquals(_tags, value))
            {
                return;
            }

            _tags.CollectionChanged -= OnTagsChanged;
            _tags = value ?? new ObservableCollection<string>();
            _tags.CollectionChanged += OnTagsChanged;
            OnPropertyChanged();
            InvalidateSearchContent();
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteDisplay));
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                if (value && _pinnedAtUtc is null)
                {
                    PinnedAtUtc = DateTime.UtcNow;
                }
                else if (!value)
                {
                    PinnedAtUtc = null;
                }
            }
        }
    }

    public DateTime? PinnedAtUtc
    {
        get => _pinnedAtUtc;
        set => SetProperty(ref _pinnedAtUtc, value);
    }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastOpenedDate
    {
        get => _lastOpenedDate;
        set
        {
            if (SetProperty(ref _lastOpenedDate, value))
            {
                RecordHistoryEntry(value);
                RaiseTrajectoryChanged();
                OnPropertyChanged(nameof(LastOpenedDisplay));
            }
        }
    }

    [JsonIgnore]
    public string FavoriteDisplay => _isFavorite ? "Yes" : "No";

    [JsonIgnore]
    public string LastOpenedDisplay =>
        _lastOpenedDate == DateTime.MinValue ? "Never" : _lastOpenedDate.ToString("g");

    public List<DateTime> OpenedHistory { get; set; } = new();

    public ItemHealthState HealthState
    {
        get => _healthState;
        set
        {
            if (SetProperty(ref _healthState, value))
            {
                OnPropertyChanged(nameof(IsOffline));
                OnPropertyChanged(nameof(HealthScore));
            }
        }
    }

    [JsonIgnore]
    public ContentKind Kind
    {
        get
        {
            if (_isActionItem) return ContentKind.Action;
            if (_isMtab) return ContentKind.Mtab;
            if (_isClipboardText || _isClipboardImage) return ContentKind.Clip;
            return _type == ItemType.Folder ? ContentKind.Folder : ContentKind.File;
        }
    }

    [JsonIgnore]
    public double HealthScore => HealthState switch
    {
        ItemHealthState.Healthy => 1.0,
        ItemHealthState.Offline => 0.6,
        ItemHealthState.Missing => 0.3,
        ItemHealthState.PermissionDenied => 0.3,
        ItemHealthState.Unchecked => 0.5,
        _ => 0.5,
    };

    [JsonIgnore]
    public PointCollection TrajectoryPoints
    {
        get
        {
            if (_trajectoryPointsCache is not null)
            {
                return _trajectoryPointsCache;
            }

            var buckets = GetLast14DayBuckets();
            var max = Math.Max(buckets.Max(), 1);
            const double width = 60.0;
            const double height = 16.0;
            const double topPadding = 2.0;
            const double drawHeight = height - 4.0;
            var points = new PointCollection(buckets.Length);
            var stepDenominator = Math.Max(buckets.Length - 1, 1);
            for (var i = 0; i < buckets.Length; i++)
            {
                var x = i * (width / stepDenominator);
                var y = topPadding + drawHeight - (buckets[i] / (double)max) * drawHeight;
                points.Add(new System.Windows.Point(x, y));
            }

            points.Freeze();
            _trajectoryPointsCache = points;
            return points;
        }
    }

    [JsonIgnore]
    public IReadOnlyList<TrajectoryBar> TrajectoryBars
    {
        get
        {
            if (_trajectoryBarsCache is not null)
            {
                return _trajectoryBarsCache;
            }

            var buckets = ComputeDailyBuckets(20);
            var max = Math.Max(buckets.Max(), 1);
            var bars = new List<TrajectoryBar>(buckets.Length);
            var lastIndex = buckets.Length - 1;
            for (var i = 0; i < buckets.Length; i++)
            {
                var ratio = buckets[i] / (double)max;
                var bucketHeight = 4 + ratio * 44;
                var recencyOpacity = 0.30 + 0.70 * (i / (double)Math.Max(lastIndex, 1));
                bars.Add(new TrajectoryBar(bucketHeight, recencyOpacity)
                {
                    DayOffset = lastIndex - i,
                    AccessCount = buckets[i],
                });
            }

            _trajectoryBarsCache = bars;
            return bars;
        }
    }

    private void RecordHistoryEntry(DateTime value)
    {
        if (value == default)
        {
            return;
        }

        if (OpenedHistory.Count > 0 && Math.Abs((OpenedHistory[^1] - value).TotalSeconds) < 1)
        {
            return;
        }

        OpenedHistory.Add(value);
        const int historyCap = 200;
        if (OpenedHistory.Count > historyCap)
        {
            OpenedHistory.RemoveRange(0, OpenedHistory.Count - historyCap);
        }
    }

    private void RaiseTrajectoryChanged()
    {
        _trajectoryPointsCache = null;
        _trajectoryBarsCache = null;
        _last14DayBucketsCache = null;
        OnPropertyChanged(nameof(TrajectoryPoints));
        OnPropertyChanged(nameof(TrajectoryBars));
        OnPropertyChanged(nameof(OpenedHistory));
        OnPropertyChanged(nameof(TrendDelta));
    }

    [JsonIgnore]
    public int TrendDelta
    {
        get
        {
            var buckets = GetLast14DayBuckets();
            var last7 = 0;
            var prior7 = 0;
            for (var i = 0; i < buckets.Length; i++)
            {
                if (i >= buckets.Length - 7)
                {
                    last7 += buckets[i];
                }
                else
                {
                    prior7 += buckets[i];
                }
            }

            return last7 - prior7;
        }
    }

    private int[] GetLast14DayBuckets()
    {
        return _last14DayBucketsCache ??= ComputeDailyBuckets(14);
    }

    private int[] ComputeDailyBuckets(int days)
    {
        var buckets = new int[days];
        if (OpenedHistory.Count == 0)
        {
            return buckets;
        }

        var todayUtc = DateTime.UtcNow.Date;
        var oldestBucketDate = todayUtc.AddDays(-(days - 1));
        foreach (var entry in OpenedHistory)
        {
            var entryDate = entry.ToUniversalTime().Date;
            if (entryDate < oldestBucketDate || entryDate > todayUtc)
            {
                continue;
            }

            var bucketIndex = (int)(entryDate - oldestBucketDate).TotalDays;
            if (bucketIndex >= 0 && bucketIndex < buckets.Length)
            {
                buckets[bucketIndex]++;
            }
        }

        return buckets;
    }

    public bool IsOffline
    {
        get => HealthState == ItemHealthState.Offline;
        set => HealthState = value ? ItemHealthState.Offline : ItemHealthState.Healthy;
    }

    public ItemLaunchProfile LaunchProfile
    {
        get => _launchProfile;
        set => SetProperty(ref _launchProfile, value ?? new ItemLaunchProfile());
    }

    public bool IsClipboardText
    {
        get => _isClipboardText;
        set
        {
            if (SetProperty(ref _isClipboardText, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public bool IsClipboardImage
    {
        get => _isClipboardImage;
        set
        {
            if (SetProperty(ref _isClipboardImage, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public bool IsActionItem
    {
        get => _isActionItem;
        set
        {
            if (SetProperty(ref _isActionItem, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public ActionKind ActionKind
    {
        get => _actionKind;
        set
        {
            if (SetProperty(ref _actionKind, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public string ClipboardContent
    {
        get => _clipboardContent;
        set
        {
            if (SetProperty(ref _clipboardContent, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public string ClipboardImageAssetPath
    {
        get => _clipboardImageAssetPath;
        set
        {
            if (SetProperty(ref _clipboardImageAssetPath, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public int ClipboardImagePixelWidth
    {
        get => _clipboardImagePixelWidth;
        set
        {
            if (SetProperty(ref _clipboardImagePixelWidth, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public int ClipboardImagePixelHeight
    {
        get => _clipboardImagePixelHeight;
        set
        {
            if (SetProperty(ref _clipboardImagePixelHeight, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public string ActionContent
    {
        get => _actionContent;
        set
        {
            if (SetProperty(ref _actionContent, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public bool IsMtab
    {
        get => _isMtab;
        set
        {
            if (SetProperty(ref _isMtab, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
                InvalidateSearchContent();
            }
        }
    }

    public ObservableCollection<Guid> MtabMemberIds
    {
        get => _mtabMemberIds;
        set
        {
            if (ReferenceEquals(_mtabMemberIds, value))
            {
                return;
            }

            _mtabMemberIds.CollectionChanged -= OnMtabMemberIdsChanged;
            _mtabMemberIds = value ?? new ObservableCollection<Guid>();
            _mtabMemberIds.CollectionChanged += OnMtabMemberIdsChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPath));
            InvalidateSearchContent();
        }
    }

    public ObservableCollection<string> MtabPaths
    {
        get => _mtabPaths;
        set
        {
            if (ReferenceEquals(_mtabPaths, value))
            {
                return;
            }

            _mtabPaths.CollectionChanged -= OnMtabPathsChanged;
            _mtabPaths = value ?? new ObservableCollection<string>();
            _mtabPaths.CollectionChanged += OnMtabPathsChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPath));
            InvalidateSearchContent();
        }
    }

    [JsonIgnore]
    public string MtabSearchHint
    {
        get => _mtabSearchHint;
        set
        {
            if (SetProperty(ref _mtabSearchHint, value))
            {
                InvalidateSearchContent();
            }
        }
    }

    public string DisplayPath
    {
        get
        {
            if (IsMtab)
            {
                var count = MtabPaths.Count > 0 ? MtabPaths.Count : MtabMemberIds.Count;
                return count == 1 ? "1 folder" : $"{count} folders";
            }

            if (IsClipboardImage)
            {
                return ClipboardImagePixelWidth > 0 && ClipboardImagePixelHeight > 0
                    ? $"Clipboard image - {ClipboardImagePixelWidth}x{ClipboardImagePixelHeight}"
                    : "Clipboard image";
            }

            if (IsActionItem)
            {
                return ActionKind switch
                {
                    ActionKind.Command => "Command",
                    ActionKind.Batch => "Batch script",
                    ActionKind.PowerShell when LaunchProfile.RunAsAdmin => "PowerShell (Admin)",
                    ActionKind.PowerShell => "PowerShell script",
                    _ => "Action item"
                };
            }

            if (!IsClipboardText)
            {
                return Path;
            }

            var preview = ClipboardContent?
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(preview))
            {
                return "Clipboard text item";
            }

            return preview.Length > 96 ? $"{preview[..93]}..." : preview;
        }
    }

    public string SearchContent => _searchContentCache ??= BuildSearchContent();

    private void OnMtabMemberIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayPath));
        InvalidateSearchContent();
    }

    private void OnMtabPathsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayPath));
        InvalidateSearchContent();
    }

    private void OnTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateSearchContent();
    }

    private string BuildSearchContent()
    {
        return IsClipboardImage
            ? $"{Name} clipboard image {ClipboardImagePixelWidth}x{ClipboardImagePixelHeight} {string.Join(' ', Tags)}"
            : IsActionItem
            ? $"{Name} {ActionKind} {ActionContent} {LaunchProfile?.WorkingDirectory} {string.Join(' ', Tags)}"
            : IsClipboardText
            ? $"{Name} {ClipboardContent} {string.Join(' ', Tags)}"
            : IsMtab
                ? $"{Name} {MtabSearchHint} {string.Join(' ', Tags)}"
                : $"{Name} {Path} {string.Join(' ', Tags)}";
    }

    private void InvalidateSearchContent()
    {
        _searchContentCache = null;
        OnPropertyChanged(nameof(SearchContent));
    }

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
}
