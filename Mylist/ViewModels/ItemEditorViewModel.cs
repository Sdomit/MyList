using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class ItemEditorViewModel : ViewModelBase
{
    private readonly IconService _iconService;
    private ItemModel? _item;
    private bool _isOpen;
    private string _name = string.Empty;
    private string _path = string.Empty;
    private ItemType _type;
    private string? _iconPath;
    private bool _useSystemIcon = true;
    private string _tags = string.Empty;
    private bool _isFavorite;
    private string _launchArguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private bool _runAsAdmin;
    private TerminalProfile _terminalProfile;

    public ItemEditorViewModel(IconService iconService)
    {
        _iconService = iconService;
        ItemTypes = new ObservableCollection<ItemType>(Enum.GetValues<ItemType>());
        TerminalProfiles = new ObservableCollection<TerminalProfile>(Enum.GetValues<TerminalProfile>());
        SaveCommand = new RelayCommand(Save, () => _item is not null);
        CancelCommand = new RelayCommand(Cancel);
    }

    public event EventHandler<ItemModel>? Saved;

    public ObservableCollection<ItemType> ItemTypes { get; }

    public ObservableCollection<TerminalProfile> TerminalProfiles { get; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public ItemModel? Item => _item;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
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

    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set => SetProperty(ref _runAsAdmin, value);
    }

    public TerminalProfile TerminalProfile
    {
        get => _terminalProfile;
        set => SetProperty(ref _terminalProfile, value);
    }

    public void BeginEdit(ItemModel item)
    {
        _item = item;
        Name = item.Name;
        Path = item.Path;
        Type = item.Type;
        IconPath = item.IconPath;
        UseSystemIcon = item.UseSystemIcon;
        Tags = string.Join(", ", item.Tags);
        IsFavorite = item.IsFavorite;
        LaunchArguments = item.LaunchProfile.Arguments;
        WorkingDirectory = item.LaunchProfile.WorkingDirectory;
        RunAsAdmin = item.LaunchProfile.RunAsAdmin;
        TerminalProfile = item.LaunchProfile.TerminalProfile;
        IsOpen = true;
        OnPropertyChanged(nameof(Item));
        CommandManager.InvalidateRequerySuggested();
    }

    public void Close()
    {
        IsOpen = false;
    }

    private void Save()
    {
        if (_item is null)
        {
            return;
        }

        _item.Name = Name;
        _item.Path = Path;
        _item.Type = Type;
        _item.IconPath = IconPath;
        _item.UseSystemIcon = UseSystemIcon;
        _item.IsFavorite = IsFavorite;
        _item.Tags.Clear();
        foreach (var tag in Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _item.Tags.Add(tag);
        }

        _item.LaunchProfile.Arguments = LaunchArguments;
        _item.LaunchProfile.WorkingDirectory = WorkingDirectory;
        _item.LaunchProfile.RunAsAdmin = RunAsAdmin;
        _item.LaunchProfile.TerminalProfile = TerminalProfile;

        _iconService.QueueIconRefresh(_item);

        IsOpen = false;
        Saved?.Invoke(this, _item);
    }

    private void Cancel()
    {
        IsOpen = false;
    }
}
