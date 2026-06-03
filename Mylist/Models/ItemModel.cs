using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
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
        set => SetProperty(ref _isFavorite, value);
    }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastOpenedDate
    {
        get => _lastOpenedDate;
        set => SetProperty(ref _lastOpenedDate, value);
    }

    public ItemHealthState HealthState
    {
        get => _healthState;
        set
        {
            if (SetProperty(ref _healthState, value))
            {
                OnPropertyChanged(nameof(IsOffline));
            }
        }
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
