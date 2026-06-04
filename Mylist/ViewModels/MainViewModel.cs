using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;
using MyList.Views;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace MyList.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly IconService _iconService;
    private readonly LauncherService _launcherService;
    private readonly ManagedActionRunnerService _managedActionRunnerService;
    private readonly ExplorerTabAutomationService _explorerTabAutomationService;
    private readonly ClipboardAssetService _clipboardAssetService;
    private readonly DensityService _densityService;
    private readonly ActionHistoryService _historyService;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _clipboardFeedbackTimer;
    private readonly DispatcherTimer _searchDebounceTimer;

    private CollectionViewModel? _selectedCollection;
    private CollectionViewModel? _bulkTargetCollection;
    private ItemModel? _selectedItem;
    private SearchQuery _appliedSearchQuery = new();
    private string _searchText = string.Empty;
    private bool _isSettingsOpen;
    private bool _isClipboardReviewPaneOpen;
    private bool _isDuplicateManagerPaneOpen;
    private bool _isAddFormOpen;
    private bool _isRefreshingClipboardReview;
    private bool _clipboardHasImmediateAsset;
    private string _clipboardButtonText = "Add Clipboard";
    private string _clipboardReviewStatusText = "Copy paths, then refresh to review them.";
    private string _inlineAddValue = string.Empty;
    private string _inlineMtabStatusText = "Edit folder paths inline, then save.";
    private CollectionViewModel? _selectedClipboardTargetCollection;
    private ClipboardImportHistoryEntry? _selectedClipboardHistoryEntry;
    private string _searchStatusText = string.Empty;
    private NetworkCheckService? _networkCheckService;

    private bool _isQuickEditOpen;
    private string _quickEditName = string.Empty;
    private string _quickEditPath = string.Empty;
    private string _quickEditTags = string.Empty;
    private string _statusText = "Ready";

    public MainViewModel(
        AppData appData,
        StorageService storageService,
        IconService iconService,
        LauncherService launcherService,
        ManagedActionRunnerService managedActionRunnerService,
        ExplorerTabAutomationService explorerTabAutomationService,
        ClipboardAssetService clipboardAssetService,
        ThemeService themeService,
        DensityService densityService)
    {
        AppData = appData;
        RuntimeStatus.DebugMode = appData.AppSettings.EnableDebugMode;
        _storageService = storageService;
        _iconService = iconService;
        _launcherService = launcherService;
        _managedActionRunnerService = managedActionRunnerService;
        _explorerTabAutomationService = explorerTabAutomationService;
        _clipboardAssetService = clipboardAssetService;
        _densityService = densityService;
        _historyService = new ActionHistoryService();

        Dispatcher = Application.Current.Dispatcher;

        AllItems = appData.Items;
        Collections = new ObservableCollection<CollectionViewModel>(
            appData.Collections.Select(c => new CollectionViewModel(c, BuildCollectionEntries(c))));

        if (Collections.Count == 0)
        {
            var mainCollection = new CollectionModel { Name = "Main" };
            AppData.Collections.Add(mainCollection);
            Collections.Add(new CollectionViewModel(mainCollection, BuildCollectionEntries(mainCollection)));
        }

        SelectedItems = new ObservableCollection<ItemModel>();
        ClipboardTargetCollections = new ObservableCollection<CollectionViewModel>();
        ClipboardWillAddEntries = new ObservableCollection<ClipboardReviewEntry>();
        ClipboardWillLinkEntries = new ObservableCollection<ClipboardReviewEntry>();
        ClipboardSkippedEntries = new ObservableCollection<ClipboardReviewEntry>();
        ClipboardHistoryEntries = new ObservableCollection<ClipboardImportHistoryEntry>();
        InlineMtabPaths = new ObservableCollection<InlineMtabPathViewModel>();
        Diagnostics = new DiagnosticsViewModel();
        DuplicateManager = new DuplicateManagerViewModel(this);

        Settings = new SettingsViewModel(
            appData.AppSettings,
            themeService,
            _densityService,
            QueueSave,
            settings => HotkeyChanged?.Invoke(this, settings),
            ExportDataAsync,
            ImportDataAsync,
            RestoreBackupAsync,
            OpenDuplicateManagerPane,
            ToggleDiagnostics,
            new ExplorerIntegrationService());
        Settings.PropertyChanged += OnSettingsPropertyChanged;

        Editor = new ItemEditorViewModel(_iconService);
        Editor.Saved += (_, _) =>
        {
            RefreshSmartCollections();
            UpdateStatusCounts();
            QueueSave();
        };

        AddFileCommand = new RelayCommand(AddFileItem);
        AddFolderCommand = new RelayCommand(AddFolderItem);
        AddFromClipboardCommand = new RelayCommand(AddFromClipboard);
        OpenClipboardReviewPaneCommand = new RelayCommand(OpenClipboardReviewPane);
        OpenDuplicateManagerPaneCommand = new RelayCommand(OpenDuplicateManagerPane);
        CloseToolPaneCommand = new RelayCommand(CloseToolPane);
        RefreshClipboardReviewCommand = new RelayCommand(RefreshClipboardReview);
        ApplyClipboardReviewCommand = new RelayCommand(ApplyClipboardReviewPane, () => CanApplyClipboardReview);
        ReapplyClipboardHistoryCommand = new RelayCommand(ReapplyClipboardHistory, () => SelectedClipboardHistoryEntry is not null);
        OpenInlineAddCommand = new RelayCommand(OpenInlineAdd);
        CancelInlineAddCommand = new RelayCommand(CancelInlineAdd);
        SaveInlineAddCommand = new RelayCommand(SaveInlineAdd, () => !string.IsNullOrWhiteSpace(InlineAddValue));
        AddInlineMtabPathCommand = new RelayCommand(AddInlineMtabPath);
        RemoveInlineMtabPathCommand = new RelayCommand<InlineMtabPathViewModel?>(RemoveInlineMtabPath);
        SaveInlineMtabCommand = new RelayCommand(SaveInlineMtab, () => SelectedItem?.IsMtab == true);
        ReloadInlineMtabCommand = new RelayCommand(() => LoadInlineMtabDraft(SelectedItem), () => SelectedItem?.IsMtab == true);
        NewActionCommand = new RelayCommand(OpenNewActionDialog);
        NewMtabCommand = new RelayCommand(OpenNewMtabDialog);
        AddSeparatorCommand = new RelayCommand(AddSeparator, CanAddSeparator);

        OpenItemCommand = new RelayCommand<ItemModel?>(OpenItem);
        OpenNewWindowCommand = new RelayCommand<ItemModel?>(OpenInNewWindow);
        OpenInTerminalCommand = new RelayCommand<ItemModel?>(OpenInTerminal);
        CopyPathCommand = new RelayCommand<ItemModel?>(CopyPath);
        RemoveItemFromCurrentCollectionCommand = new RelayCommand<ItemCollectionActionContext?>(RemoveItemFromCurrentCollection, CanRemoveItemFromCurrentCollection);
        RemoveSeparatorCommand = new RelayCommand<CollectionEntryActionContext?>(RemoveSeparator, CanRemoveSeparator);
        DeleteItemGloballyCommand = new RelayCommand<ItemModel?>(DeleteItemGlobally, CanDeleteItemGlobally);
        EditItemCommand = new RelayCommand<ItemModel?>(EditItem);
        EditMtabCommand = new RelayCommand<ItemModel?>(EditMtab, item => item?.IsMtab == true);
        RetryHealthCheckCommand = new AsyncRelayCommand<ItemModel?>(RetryHealthCheckAsync);

        OpenAllCommand = new RelayCommand<CollectionViewModel?>(OpenAllItems);
        ToggleViewModeCommand = new RelayCommand(ToggleViewMode);
        ToggleCollectionCommand = new RelayCommand(ToggleCollection);
        ToggleThemeCommand = new RelayCommand(() => Settings.IsDarkMode = !Settings.IsDarkMode);
        ToggleSettingsCommand = new RelayCommand(ToggleSettings);
        CloseSettingsCommand = new RelayCommand(CloseSettings);
        AddCollectionCommand = new RelayCommand(AddCollection);
        DeleteCollectionCommand = new RelayCommand<CollectionViewModel?>(DeleteCollection);
        RenameCollectionCommand = new RelayCommand<CollectionViewModel?>(RenameCollection);

        UndoCommand = new RelayCommand(Undo, () => _historyService.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _historyService.CanRedo);

        OpenSelectedCommand = new RelayCommand(OpenSelectedItems, () => SelectedItems.Count > 0);
        RemoveSelectedFromCurrentCollectionCommand = new RelayCommand(RemoveSelectedItemsFromCurrentCollection, CanRemoveSelectedItemsFromCurrentCollection);
        DeleteSelectedGloballyCommand = new RelayCommand(DeleteSelectedItemsGlobally, () => SelectedItems.Count > 0);
        MoveSelectedToCollectionCommand = new RelayCommand(MoveSelectedToCollection, () => SelectedItems.Count > 0 && BulkTargetCollection is not null);
        AddTagToSelectedCommand = new RelayCommand(AddTagToSelected, () => SelectedItems.Count > 0);
        ToggleFavoriteSelectedCommand = new RelayCommand(ToggleFavoriteSelected, () => SelectedItems.Count > 0);

        BeginQuickEditCommand = new RelayCommand(BeginQuickEdit, () => SelectedItem is not null);
        QuickEditItemCommand = new RelayCommand<ItemModel?>(BeginQuickEditForItem);
        SaveQuickEditCommand = new RelayCommand(SaveQuickEdit, () => IsQuickEditOpen && SelectedItem is not null);
        CancelQuickEditCommand = new RelayCommand(CancelQuickEdit, () => IsQuickEditOpen);

        _historyService.StateChanged += (_, _) => CommandManager.InvalidateRequerySuggested();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await _storageService.SaveAsync(AppData);
        };

        _clipboardFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _clipboardFeedbackTimer.Tick += (_, _) =>
        {
            _clipboardFeedbackTimer.Stop();
            ClipboardButtonText = "Add Clipboard";
        };

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ApplySearch();
        };

        var repairedFolderNames = RepairGeneratedFolderNames();
        _densityService.ApplyDensity(Settings.UiDensity);
        RefreshAllIcons();
        RefreshSmartCollections();
        ApplySearch();

        if (repairedFolderNames)
        {
            QueueSave();
        }

        SelectedCollection = Collections.FirstOrDefault();
        BulkTargetCollection = Collections.FirstOrDefault(collection => !collection.IsSmart);
        UpdateStatusCounts();
    }

    public event EventHandler<HotkeySettings>? HotkeyChanged;

    public AppData AppData { get; }

    public Dispatcher Dispatcher { get; }

    public ObservableCollection<ItemModel> AllItems { get; }

    public ObservableCollection<CollectionViewModel> Collections { get; }

    public ObservableCollection<ItemModel> SelectedItems { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public CollectionViewModel? SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (SetProperty(ref _selectedCollection, value))
            {
                CloseToolPane();
                OnPropertyChanged(nameof(CanOpenAll));
                ApplySearchToCollection(value);
                UpdateStatusCounts();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public CollectionViewModel? BulkTargetCollection
    {
        get => _bulkTargetCollection;
        set
        {
            if (SetProperty(ref _bulkTargetCollection, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ItemModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                LoadInlineMtabDraft(value);
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public SettingsViewModel Settings { get; }

    public ItemEditorViewModel Editor { get; }

    public DuplicateManagerViewModel DuplicateManager { get; }

    public ObservableCollection<CollectionViewModel> ClipboardTargetCollections { get; }

    public ObservableCollection<ClipboardReviewEntry> ClipboardWillAddEntries { get; }

    public ObservableCollection<ClipboardReviewEntry> ClipboardWillLinkEntries { get; }

    public ObservableCollection<ClipboardReviewEntry> ClipboardSkippedEntries { get; }

    public ObservableCollection<ClipboardImportHistoryEntry> ClipboardHistoryEntries { get; }

    public ObservableCollection<InlineMtabPathViewModel> InlineMtabPaths { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleSearchApply();
            }
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public bool IsClipboardReviewPaneOpen => _isClipboardReviewPaneOpen;

    public bool IsDuplicateManagerPaneOpen => _isDuplicateManagerPaneOpen;

    public bool IsCollectionPaneVisible => !_isClipboardReviewPaneOpen && !_isDuplicateManagerPaneOpen;

    public bool IsAddFormOpen
    {
        get => _isAddFormOpen;
        private set => SetProperty(ref _isAddFormOpen, value);
    }

    public string InlineAddValue
    {
        get => _inlineAddValue;
        set
        {
            if (SetProperty(ref _inlineAddValue, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public CollectionViewModel? SelectedClipboardTargetCollection
    {
        get => _selectedClipboardTargetCollection;
        set
        {
            if (SetProperty(ref _selectedClipboardTargetCollection, value) && !_isRefreshingClipboardReview)
            {
                RefreshClipboardReview();
            }
        }
    }

    public ClipboardImportHistoryEntry? SelectedClipboardHistoryEntry
    {
        get => _selectedClipboardHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedClipboardHistoryEntry, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ClipboardReviewStatusText
    {
        get => _clipboardReviewStatusText;
        private set => SetProperty(ref _clipboardReviewStatusText, value);
    }

    public int ClipboardApplyCount => ClipboardWillAddEntries.Count + ClipboardWillLinkEntries.Count;

    public bool CanApplyClipboardReview => ClipboardApplyCount > 0 || _clipboardHasImmediateAsset;

    public string ClipboardApplyButtonText => ClipboardApplyCount > 0
        ? $"Add {ClipboardApplyCount} Item{(ClipboardApplyCount == 1 ? string.Empty : "s")}"
        : _clipboardHasImmediateAsset
            ? "Save Clipboard Item"
            : "No Valid Paths";

    public string ClipboardReviewSummaryText =>
        $"Will add {ClipboardWillAddEntries.Count}, link {ClipboardWillLinkEntries.Count}, skip {ClipboardSkippedEntries.Count}.";

    public string ClipboardWillAddHeader => $"Will Add ({ClipboardWillAddEntries.Count})";

    public string ClipboardWillLinkHeader => $"Will Link Existing ({ClipboardWillLinkEntries.Count})";

    public string ClipboardSkippedHeader => $"Skipped ({ClipboardSkippedEntries.Count})";

    public string InlineMtabStatusText
    {
        get => _inlineMtabStatusText;
        private set => SetProperty(ref _inlineMtabStatusText, value);
    }

    public string ClipboardButtonText
    {
        get => _clipboardButtonText;
        private set => SetProperty(ref _clipboardButtonText, value);
    }

    public string SearchStatusText
    {
        get => _searchStatusText;
        private set
        {
            if (SetProperty(ref _searchStatusText, value))
            {
                OnPropertyChanged(nameof(HasSearchStatus));
            }
        }
    }

    public bool HasSearchStatus => !string.IsNullOrWhiteSpace(SearchStatusText);

    public bool IsQuickEditOpen
    {
        get => _isQuickEditOpen;
        set
        {
            if (SetProperty(ref _isQuickEditOpen, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string QuickEditName
    {
        get => _quickEditName;
        set => SetProperty(ref _quickEditName, value);
    }

    public string QuickEditPath
    {
        get => _quickEditPath;
        set => SetProperty(ref _quickEditPath, value);
    }

    public string QuickEditTags
    {
        get => _quickEditTags;
        set => SetProperty(ref _quickEditTags, value);
    }

    public int VisibleItemCount { get; private set; }

    public int OfflineItemCount { get; private set; }

    public int FavoriteItemCount { get; private set; }

    public bool IsBulkToolbarVisible => SelectedItems.Count > 1;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool CanOpenAll => SelectedCollection is not null;

    public ICommand AddFileCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand AddFromClipboardCommand { get; }
    public ICommand OpenClipboardReviewPaneCommand { get; }
    public ICommand OpenDuplicateManagerPaneCommand { get; }
    public ICommand CloseToolPaneCommand { get; }
    public ICommand RefreshClipboardReviewCommand { get; }
    public ICommand ApplyClipboardReviewCommand { get; }
    public ICommand ReapplyClipboardHistoryCommand { get; }
    public ICommand OpenInlineAddCommand { get; }
    public ICommand CancelInlineAddCommand { get; }
    public ICommand SaveInlineAddCommand { get; }
    public ICommand AddInlineMtabPathCommand { get; }
    public ICommand RemoveInlineMtabPathCommand { get; }
    public ICommand SaveInlineMtabCommand { get; }
    public ICommand ReloadInlineMtabCommand { get; }
    public ICommand NewActionCommand { get; }
    public ICommand NewMtabCommand { get; }
    public ICommand AddSeparatorCommand { get; }
    public ICommand OpenItemCommand { get; }
    public ICommand OpenNewWindowCommand { get; }
    public ICommand OpenInTerminalCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand RemoveItemFromCurrentCollectionCommand { get; }
    public ICommand RemoveSeparatorCommand { get; }
    public ICommand DeleteItemGloballyCommand { get; }
    public ICommand EditItemCommand { get; }
    public ICommand EditMtabCommand { get; }
    public ICommand RetryHealthCheckCommand { get; }
    public ICommand OpenAllCommand { get; }
    public ICommand ToggleViewModeCommand { get; }
    public ICommand ToggleCollectionCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand AddCollectionCommand { get; }
    public ICommand DeleteCollectionCommand { get; }
    public ICommand RenameCollectionCommand { get; }

    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }

    public ICommand OpenSelectedCommand { get; }
    public ICommand RemoveSelectedFromCurrentCollectionCommand { get; }
    public ICommand DeleteSelectedGloballyCommand { get; }
    public ICommand MoveSelectedToCollectionCommand { get; }
    public ICommand AddTagToSelectedCommand { get; }
    public ICommand ToggleFavoriteSelectedCommand { get; }

    public ICommand BeginQuickEditCommand { get; }
    public ICommand QuickEditItemCommand { get; }
    public ICommand SaveQuickEditCommand { get; }
    public ICommand CancelQuickEditCommand { get; }

    public void AttachNetworkCheckService(NetworkCheckService service)
    {
        _networkCheckService = service;
    }

    public void QueueSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SetSelectedItems(IEnumerable<ItemModel> items)
    {
        var snapshot = items.Where(item => item is not null).Distinct().ToList();

        SelectedItems.Clear();
        foreach (var item in snapshot)
        {
            SelectedItems.Add(item);
        }

        if (snapshot.Count == 1)
        {
            SelectedItem = snapshot[0];
        }
        else if (snapshot.Count == 0)
        {
            SelectedItem = null;
        }

        OnPropertyChanged(nameof(IsBulkToolbarVisible));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RefreshSmartCollections()
    {
        UpdateMtabDerivedData();

        foreach (var collection in Collections)
        {
            if (collection.IsSmart)
            {
                collection.RefreshSmartItems(AllItems);
            }
            else
            {
                collection.ResetEntries(BuildCollectionEntries(collection.Model));
            }
        }

        UpdateStatusCounts();
    }

    public void MoveItem(CollectionViewModel collection, ItemModel item, int newIndex)
    {
        if (collection.IsSmart)
        {
            return;
        }

        var oldIndex = collection.Items.IndexOf(item);
        if (oldIndex < 0)
        {
            return;
        }

        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, collection.Items.Count - 1));
        if (oldIndex == newIndex)
        {
            return;
        }

        var action = new DelegateUndoableAction(
            "Move Item",
            () => MoveItemInternal(collection, item, oldIndex, newIndex),
            () => MoveItemInternal(collection, item, newIndex, oldIndex));

        ExecuteAction(action);
    }

    public void MoveCollectionEntry(CollectionViewModel collection, CollectionEntryViewModel entry, int newIndex)
    {
        if (collection.IsSmart)
        {
            return;
        }

        var oldIndex = collection.Entries.IndexOf(entry);
        if (oldIndex < 0)
        {
            return;
        }

        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, collection.Entries.Count - 1));
        if (oldIndex == newIndex)
        {
            return;
        }

        var action = new DelegateUndoableAction(
            "Move Collection Entry",
            () => MoveCollectionEntryInternal(collection, oldIndex, newIndex),
            () => MoveCollectionEntryInternal(collection, newIndex, oldIndex));

        ExecuteAction(action);
    }

    public void MoveCollection(CollectionViewModel collection, int newIndex)
    {
        var oldIndex = Collections.IndexOf(collection);
        if (oldIndex < 0)
        {
            return;
        }

        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, Collections.Count - 1));
        if (oldIndex == newIndex)
        {
            return;
        }

        var action = new DelegateUndoableAction(
            "Move Collection",
            () => MoveCollectionInternal(collection, oldIndex, newIndex),
            () => MoveCollectionInternal(collection, newIndex, oldIndex));

        ExecuteAction(action);
    }

    public void DeleteDraggedEntry(CollectionEntryViewModel? entry, CollectionViewModel? sourceCollection)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.IsSeparator)
        {
            var context = new CollectionEntryActionContext(entry, sourceCollection);
            if (!CanRemoveSeparator(context))
            {
                return;
            }

            var collectionName = sourceCollection?.Name ?? "this collection";
            var result = MessageBox.Show(
                $"Delete this separator from '{collectionName}'?",
                "Delete Separator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            RemoveSeparator(context);
            return;
        }

        if (entry.Item is not null)
        {
            DeleteItemGlobally(entry.Item);
        }
    }

    public bool CanTransferItemBetweenCollections(CollectionViewModel? sourceCollection, CollectionViewModel? targetCollection, ItemModel? item)
    {
        if (item is null || sourceCollection is null || targetCollection is null)
        {
            return false;
        }

        if (sourceCollection.IsSmart || targetCollection.IsSmart)
        {
            return false;
        }

        if (sourceCollection.Id == targetCollection.Id)
        {
            return false;
        }

        return sourceCollection.Model.ItemIds.Contains(item.Id);
    }

    public bool TryTransferItemBetweenCollections(
        CollectionViewModel? sourceCollection,
        CollectionViewModel? targetCollection,
        ItemModel? item,
        CollectionTransferAction action)
    {
        if (action == CollectionTransferAction.Cancel)
        {
            return false;
        }

        if (!CanTransferItemBetweenCollections(sourceCollection, targetCollection, item))
        {
            return false;
        }

        var source = sourceCollection!;
        var target = targetCollection!;
        var movedItem = item!;
        var sourceIndex = GetEntryIndex(source, movedItem);
        if (sourceIndex < 0)
        {
            return false;
        }

        var targetContainsItem = target.Model.ItemIds.Contains(movedItem.Id);
        var targetIndex = target.Model.Entries.Count;

        if (action == CollectionTransferAction.CopyToCollection)
        {
            if (targetContainsItem)
            {
                StatusText = $"'{movedItem.Name}' is already in {target.Name}.";
                return false;
            }

            var copyAction = new DelegateUndoableAction(
                "Copy Item To Collection",
                () => InsertItemIntoCollection(target, movedItem, targetIndex),
                () => RemoveItemFromCollection(target, movedItem));

            ExecuteAction(copyAction);
            StatusText = $"Copied '{movedItem.Name}' to {target.Name}.";
            return true;
        }

        var moveAction = new DelegateUndoableAction(
            "Move Item To Collection",
            () =>
            {
                if (!targetContainsItem)
                {
                    InsertItemIntoCollection(target, movedItem, targetIndex);
                }

                RemoveItemFromCollection(source, movedItem);
            },
            () =>
            {
                InsertItemIntoCollection(source, movedItem, sourceIndex);
                if (!targetContainsItem)
                {
                    RemoveItemFromCollection(target, movedItem);
                }
            });

        ExecuteAction(moveAction);
        StatusText = $"Moved '{movedItem.Name}' to {target.Name}.";
        return true;
    }

    public void AddItemFromPath(string? path)
    {
        AddOrLinkItem(path);
    }

    private void OpenInlineAdd()
    {
        CloseToolPane();
        IsSettingsOpen = false;
        IsQuickEditOpen = false;
        IsAddFormOpen = true;
        StatusText = "Paste or type a rooted path to add it to the current collection.";
    }

    private void CancelInlineAdd()
    {
        InlineAddValue = string.Empty;
        IsAddFormOpen = false;
        StatusText = "Add cancelled.";
    }

    private void SaveInlineAdd()
    {
        var value = InlineAddValue.Trim();
        var outcome = AddOrLinkItem(value);
        switch (outcome)
        {
            case AddPathOutcome.Invalid:
                StatusText = "Enter a valid rooted file, folder, or UNC path.";
                return;
            case AddPathOutcome.AddedNew:
                StatusText = $"Added '{GetDefaultName(value)}'.";
                break;
            case AddPathOutcome.LinkedExisting:
                StatusText = "Linked the existing item to this collection.";
                break;
            default:
                StatusText = "That item is already in this collection.";
                break;
        }

        InlineAddValue = string.Empty;
        IsAddFormOpen = false;
    }

    private void AddInlineMtabPath()
    {
        if (SelectedItem?.IsMtab != true)
        {
            return;
        }

        InlineMtabPaths.Add(new InlineMtabPathViewModel(string.Empty));
        InlineMtabStatusText = "Enter the new folder path, then save.";
    }

    private void RemoveInlineMtabPath(InlineMtabPathViewModel? path)
    {
        if (path is null)
        {
            return;
        }

        InlineMtabPaths.Remove(path);
        InlineMtabStatusText = "Save to apply the updated folder list.";
    }

    private void SaveInlineMtab()
    {
        if (SelectedItem?.IsMtab != true)
        {
            return;
        }

        if (!UpdateMtabFromPaths(SelectedItem, SelectedItem.Name, InlineMtabPaths.Select(path => path.PathText), out var status))
        {
            InlineMtabStatusText = status;
            StatusText = status;
            return;
        }

        LoadInlineMtabDraft(SelectedItem);
        InlineMtabStatusText = status;
        StatusText = status;
    }

    private void LoadInlineMtabDraft(ItemModel? item)
    {
        InlineMtabPaths.Clear();
        if (item?.IsMtab != true)
        {
            InlineMtabStatusText = "Edit folder paths inline, then save.";
            return;
        }

        foreach (var path in GetMtabFolderPaths(item))
        {
            InlineMtabPaths.Add(new InlineMtabPathViewModel(path));
        }

        InlineMtabStatusText = $"{InlineMtabPaths.Count} folders. Edit paths inline, then save.";
    }

    public void MergeDuplicateGroup(DuplicateGroup group)
    {
        if (group is null || group.Items.Count < 2)
        {
            return;
        }

        var survivor = group.SelectedSurvivor ?? group.Items.First();
        var duplicates = group.Items.Where(item => item.Id != survivor.Id).ToList();
        if (duplicates.Count == 0)
        {
            return;
        }

        var allCollectionSnapshots = Collections
            .ToDictionary(
                c => c,
                c => CloneCollectionEntries(c.Model));

        var allItemsSnapshot = AllItems.ToList();

        var action = new DelegateUndoableAction(
            "Merge Duplicates",
            () =>
            {
                foreach (var duplicate in duplicates)
                {
                    MergeDuplicateIntoSurvivor(survivor, duplicate);
                    ReplaceItemReferences(duplicate, survivor);
                    AllItems.Remove(duplicate);
                }
            },
            () =>
            {
                AllItems.Clear();
                foreach (var item in allItemsSnapshot)
                {
                    AllItems.Add(item);
                }

                foreach (var (collection, entries) in allCollectionSnapshots)
                {
                    collection.Model.Entries.Clear();
                    foreach (var entry in entries)
                    {
                        collection.Model.Entries.Add(entry);
                    }

                    SyncCollectionItemIds(collection.Model);
                }

                RefreshSmartCollections();
            });

        ExecuteAction(action);
    }

    public void ShowCommandPalette()
    {
        var entries = BuildCommandPaletteEntries();
        var palette = new CommandPaletteWindow(entries)
        {
            Owner = Application.Current.MainWindow
        };

        palette.ShowDialog();
    }

    public void ToggleDiagnostics()
    {
        Diagnostics.Refresh();

        var host = new Window
        {
            Title = "Diagnostics",
            Width = 760,
            Height = 520,
            MinWidth = 640,
            MinHeight = 420,
            Owner = Application.Current.MainWindow,
            Content = new DiagnosticsView { DataContext = Diagnostics }
        };

        host.ShowDialog();
    }

    public void OpenDuplicateManager()
    {
        var duplicateManager = new DuplicateManagerWindow(this)
        {
            Owner = Application.Current.MainWindow
        };

        duplicateManager.ShowDialog();
        RefreshSmartCollections();
    }

    private void OpenClipboardReviewPane()
    {
        SetActiveToolPane(showClipboard: true, showDuplicates: false);
        RefreshClipboardReview();
    }

    private void OpenDuplicateManagerPane()
    {
        DuplicateManager.Refresh();
        SetActiveToolPane(showClipboard: false, showDuplicates: true);
        StatusText = $"{DuplicateManager.Groups.Count} duplicate groups found.";
    }

    private void CloseToolPane()
    {
        SetActiveToolPane(showClipboard: false, showDuplicates: false);
    }

    private void SetActiveToolPane(bool showClipboard, bool showDuplicates)
    {
        var changed = SetProperty(ref _isClipboardReviewPaneOpen, showClipboard, nameof(IsClipboardReviewPaneOpen));
        changed |= SetProperty(ref _isDuplicateManagerPaneOpen, showDuplicates, nameof(IsDuplicateManagerPaneOpen));
        if (changed)
        {
            OnPropertyChanged(nameof(IsCollectionPaneVisible));
        }

        if (showClipboard || showDuplicates)
        {
            IsSettingsOpen = false;
            IsQuickEditOpen = false;
            IsAddFormOpen = false;
            Editor.Close();
        }
    }

    private void RefreshClipboardReview()
    {
        try
        {
            _isRefreshingClipboardReview = true;

            var previousTarget = SelectedClipboardTargetCollection;
            ClipboardTargetCollections.Clear();
            foreach (var collection in Collections.Where(collection => !collection.IsSmart))
            {
                ClipboardTargetCollections.Add(collection);
            }

            var target = previousTarget is not null && ClipboardTargetCollections.Contains(previousTarget)
                ? previousTarget
                : ResolveTargetCollection();
            SetProperty(ref _selectedClipboardTargetCollection, target, nameof(SelectedClipboardTargetCollection));

            ClipboardWillAddEntries.Clear();
            ClipboardWillLinkEntries.Clear();
            ClipboardSkippedEntries.Clear();
            _clipboardHasImmediateAsset = false;

            foreach (var entry in BuildClipboardReviewEntriesFromClipboard(target))
            {
                if (entry.Status == ClipboardReviewStatus.ValidNew && entry.WillApply)
                {
                    ClipboardWillAddEntries.Add(entry);
                }
                else if (entry.Status == ClipboardReviewStatus.ValidExisting && entry.WillApply)
                {
                    ClipboardWillLinkEntries.Add(entry);
                }
                else
                {
                    ClipboardSkippedEntries.Add(entry);
                }
            }

            ClipboardHistoryEntries.Clear();
            foreach (var historyEntry in AppData.ClipboardImportHistory.OrderByDescending(entry => entry.TimestampUtc).Take(20))
            {
                ClipboardHistoryEntries.Add(historyEntry);
            }

            if (ClipboardApplyCount == 0)
            {
                _clipboardHasImmediateAsset = Clipboard.ContainsImage()
                    || (Clipboard.ContainsText() && !string.IsNullOrWhiteSpace(Clipboard.GetText()));
            }

            ClipboardReviewStatusText = ClipboardApplyCount > 0
                ? "Review the paths below, then add them to the selected collection."
                : _clipboardHasImmediateAsset
                    ? "Clipboard text or image is ready to save as an item."
                    : "No importable content is currently on the clipboard.";
            NotifyClipboardReviewStateChanged();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Inline clipboard review failed.");
            ClipboardReviewStatusText = "Clipboard could not be read. Copy the paths again and refresh.";
            NotifyClipboardReviewStateChanged();
        }
        finally
        {
            _isRefreshingClipboardReview = false;
        }
    }

    private void ApplyClipboardReviewPane()
    {
        if (ClipboardApplyCount == 0 && _clipboardHasImmediateAsset)
        {
            var imported = Clipboard.ContainsImage()
                ? TryAddClipboardImageItem()
                : Clipboard.ContainsText() && TryAddClipboardTextItem(Clipboard.GetText());
            RefreshClipboardReview();
            ClipboardReviewStatusText = imported
                ? "Saved the clipboard content as an item."
                : "Clipboard content could not be saved.";
            StatusText = ClipboardReviewStatusText;
            return;
        }

        var entries = ClipboardWillAddEntries
            .Concat(ClipboardWillLinkEntries)
            .Concat(ClipboardSkippedEntries)
            .ToList();
        var summary = ApplyClipboardReview(entries, SelectedClipboardTargetCollection);
        if (summary.addedNew + summary.linkedExisting > 0)
        {
            AppendClipboardHistory(summary, SelectedClipboardTargetCollection, entries);
            ShowClipboardFeedback("Copied ✓");
        }

        RefreshClipboardReview();
        ClipboardReviewStatusText =
            $"Added {summary.addedNew}, linked {summary.linkedExisting}, skipped {summary.skippedInvalid + summary.skippedDuplicate}.";
        StatusText = ClipboardReviewStatusText;
    }

    private void ReapplyClipboardHistory()
    {
        if (SelectedClipboardHistoryEntry is null)
        {
            return;
        }

        var entries = BuildClipboardReviewEntriesFromPaths(
            SelectedClipboardHistoryEntry.Paths,
            SelectedClipboardTargetCollection);
        var summary = ApplyClipboardReview(entries, SelectedClipboardTargetCollection);
        if (summary.addedNew + summary.linkedExisting > 0)
        {
            AppendClipboardHistory(summary, SelectedClipboardTargetCollection, entries);
        }

        RefreshClipboardReview();
        ClipboardReviewStatusText =
            $"History applied: added {summary.addedNew}, linked {summary.linkedExisting}, skipped {summary.skippedInvalid + summary.skippedDuplicate}.";
        StatusText = ClipboardReviewStatusText;
    }

    private void NotifyClipboardReviewStateChanged()
    {
        OnPropertyChanged(nameof(ClipboardApplyCount));
        OnPropertyChanged(nameof(CanApplyClipboardReview));
        OnPropertyChanged(nameof(ClipboardApplyButtonText));
        OnPropertyChanged(nameof(ClipboardReviewSummaryText));
        OnPropertyChanged(nameof(ClipboardWillAddHeader));
        OnPropertyChanged(nameof(ClipboardWillLinkHeader));
        OnPropertyChanged(nameof(ClipboardSkippedHeader));
        CommandManager.InvalidateRequerySuggested();
    }

    private ObservableCollection<CollectionEntryViewModel> BuildCollectionEntries(CollectionModel model)
    {
        if (model.IsSmart)
        {
            return new ObservableCollection<CollectionEntryViewModel>();
        }

        SyncCollectionItemIds(model);

        var entries = new ObservableCollection<CollectionEntryViewModel>();
        foreach (var entry in model.Entries)
        {
            if (entry.Kind == CollectionEntryKind.Separator)
            {
                entries.Add(new CollectionEntryViewModel(entry, null));
                continue;
            }

            var item = AllItems.FirstOrDefault(i => i.Id == entry.ItemId);
            if (item is not null)
            {
                entries.Add(new CollectionEntryViewModel(entry, item));
            }
        }

        return entries;
    }

    private void ApplySearch()
    {
        _searchDebounceTimer.Stop();

        var query = SearchQueryParser.Parse(SearchText);
        _appliedSearchQuery = query;
        SearchStatusText = query.InvalidTokens.Count > 0
            ? $"Ignored invalid filters: {string.Join(", ", query.InvalidTokens)}"
            : string.Empty;

        foreach (var collection in GetSearchScopeCollections())
        {
            collection.UpdateSearchQuery(query);
        }

        UpdateStatusCounts();
    }

    private void ScheduleSearchApply()
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void ApplySearchToCollection(CollectionViewModel? collection)
    {
        collection?.UpdateSearchQuery(_appliedSearchQuery);
    }

    private IEnumerable<CollectionViewModel> GetSearchScopeCollections()
    {
        if (Settings.CollectionsLayout == CollectionsLayout.Accordion)
        {
            return Collections;
        }

        return SelectedCollection is not null
            ? new[] { SelectedCollection }
            : Enumerable.Empty<CollectionViewModel>();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.CollectionsLayout))
        {
            ApplySearch();
        }
    }

    private void AddFileItem()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add File or App",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddItemFromPath(dialog.FileName);
        }
    }

    private void AddFolderItem()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AddItemFromPath(dialog.SelectedPath);
        }
    }

    private void AddFromClipboard()
    {
        try
        {
            var entries = BuildClipboardReviewEntriesFromClipboard();
            var hasApplicableEntries = entries.Any(entry => entry.WillApply);

            if (!hasApplicableEntries && Clipboard.ContainsImage())
            {
                if (TryAddClipboardImageItem())
                {
                    return;
                }

                ShowClipboardFeedback("Image Failed");
                MessageBox.Show("Clipboard image could not be saved.", "Clipboard Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!hasApplicableEntries && Clipboard.ContainsText())
            {
                var clipboardText = Clipboard.GetText();
                if (TryAddClipboardTextItem(clipboardText))
                {
                    return;
                }
            }

            if (entries.Count == 0)
            {
                ShowClipboardFeedback("No Valid Path");
                MessageBox.Show("Clipboard does not contain importable paths.", "Clipboard Review", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var history = AppData.ClipboardImportHistory
                .OrderByDescending(entry => entry.TimestampUtc)
                .Take(20)
                .ToList();

            var reviewWindow = new ClipboardReviewWindow(
                entries,
                Collections.Where(c => !c.IsSmart).ToList(),
                ResolveTargetCollection(),
                history)
            {
                Owner = Application.Current.MainWindow
            };

            if (reviewWindow.ShowDialog() != true)
            {
                return;
            }

            var targetCollection = reviewWindow.SelectedTargetCollection;
            var effectiveEntries = entries;

            if (reviewWindow.UseHistorySelection && reviewWindow.SelectedHistoryEntry is not null)
            {
                effectiveEntries = BuildClipboardReviewEntriesFromPaths(reviewWindow.SelectedHistoryEntry.Paths, targetCollection);
            }

            var summary = ApplyClipboardReview(effectiveEntries, targetCollection);
            if (summary.addedNew + summary.linkedExisting > 0)
            {
                ShowClipboardFeedback("Copied ✓");
                AppendClipboardHistory(summary, targetCollection, effectiveEntries);
            }
            else
            {
                ShowClipboardFeedback("No New Item");
            }

            MessageBox.Show(
                $"Added new: {summary.addedNew}\nLinked existing: {summary.linkedExisting}\nSkipped invalid: {summary.skippedInvalid}\nSkipped duplicate/already linked: {summary.skippedDuplicate}",
                "Clipboard Review",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Clipboard import flow failed.");
            MessageBox.Show(
                "Clipboard import failed. Please copy paths again and retry.",
                "Clipboard Review",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public async Task HandleExplorerAddToMtabAsync(IReadOnlyList<string> rawPaths)
    {
        if (rawPaths is null || rawPaths.Count == 0)
        {
            return;
        }

        var normalizedFolders = NormalizeExplorerFolderPaths(rawPaths, out var invalidCount, out var duplicatePayloadCount);
        if (normalizedFolders.Count == 0)
        {
            StatusText = "Explorer add: no valid folder paths were found.";
            LogService.Instance.Log($"Explorer add-to-mtab skipped. Valid: 0, invalid: {invalidCount}, duplicate payload: {duplicatePayloadCount}.");
            MessageBox.Show(
                "No valid folders were received from Explorer.",
                "Add to MyList Mtab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var existingMtabs = AllItems
            .Where(item => item.IsMtab)
            .OrderBy(item => item.Name)
            .ToList();

        if (existingMtabs.Count == 0)
        {
            var createDialog = new MtabEditorWindow(
                "Create Mtab",
                BuildSuggestedMtabNameFromPaths(normalizedFolders),
                normalizedFolders)
            {
                Owner = Application.Current.MainWindow
            };

            if (createDialog.ShowDialog() != true)
            {
                StatusText = "Explorer add canceled.";
                return;
            }

            if (!CreateMtabFromPaths(createDialog.MtabName, createDialog.ValidPaths, ResolveTargetCollection(), out var createStatus))
            {
                MessageBox.Show(createStatus, "Add to MyList Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText = createStatus;
                return;
            }

            var skippedCount = invalidCount + duplicatePayloadCount;
            StatusText = skippedCount > 0
                ? $"{createStatus} Skipped invalid/duplicate payload paths: {skippedCount}."
                : createStatus;
            LogService.Instance.Log($"Explorer add-to-mtab created new Mtab. {StatusText}");
            await Task.CompletedTask;
            return;
        }

        var picker = new MtabPickerWindow(existingMtabs, normalizedFolders)
        {
            Owner = Application.Current.MainWindow
        };

        if (picker.ShowDialog() != true || picker.SelectedMtab is null)
        {
            StatusText = "Explorer add canceled.";
            return;
        }

        var selectedMtab = picker.SelectedMtab;
        if (!AppendPathsToMtab(selectedMtab, normalizedFolders, out var addedCount, out var skippedExistingCount))
        {
            var totalSkipped = invalidCount + duplicatePayloadCount + skippedExistingCount;
            StatusText = totalSkipped > 0
                ? $"No new paths were added to '{selectedMtab.Name}'. Skipped: {totalSkipped}."
                : $"No new paths were added to '{selectedMtab.Name}'.";
            LogService.Instance.Log($"Explorer add-to-mtab no changes. Target={selectedMtab.Name}, skipped={totalSkipped}.");
            return;
        }

        var skippedTotal = invalidCount + duplicatePayloadCount + skippedExistingCount;
        StatusText = $"Added {addedCount} folder(s) to '{selectedMtab.Name}'. Skipped: {skippedTotal}.";
        LogService.Instance.Log(
            $"Explorer add-to-mtab applied. Target={selectedMtab.Name}, added={addedCount}, skippedExisting={skippedExistingCount}, invalid={invalidCount}, duplicatePayload={duplicatePayloadCount}.");
        await Task.CompletedTask;
    }

    private bool TryLaunch(ItemModel item, Action launch)
    {
        try
        {
            launch();
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, $"Failed to open '{item.Name}'.");
            StatusText = $"Couldn't open '{item.Name}': {ex.Message}";
            return false;
        }
    }

    private async void OpenItem(ItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        var keepToolOnTop = ShouldKeepToolOnTopAfterOpen();

        if (item.IsActionItem)
        {
            if (TryRunActionItem(item))
            {
                item.LastOpenedDate = DateTime.UtcNow;
                RefreshSmartCollections();
                QueueSave();
                ApplyPostOpenWindowBehavior(keepToolOnTop);
            }

            return;
        }

        if (TryCopyClipboardItem(item))
        {
            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            return;
        }

        if (item.IsMtab)
        {
            var result = await OpenMtabAsync(item, openAsSeparateWindows: false);
            if (result is null)
            {
                return;
            }

            StatusText = result.BuildSummary();
            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            ApplyPostOpenWindowBehavior(keepToolOnTop);
            return;
        }

        if (!TryLaunch(item, () => _launcherService.Open(item)))
        {
            return;
        }

        item.LastOpenedDate = DateTime.UtcNow;
        RefreshSmartCollections();
        QueueSave();
        ApplyPostOpenWindowBehavior(keepToolOnTop);
    }

    private async void OpenInNewWindow(ItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        var keepToolOnTop = ShouldKeepToolOnTopAfterOpen();

        if (item.IsActionItem)
        {
            if (TryRunActionItem(item))
            {
                item.LastOpenedDate = DateTime.UtcNow;
                RefreshSmartCollections();
                QueueSave();
                ApplyPostOpenWindowBehavior(keepToolOnTop);
            }

            return;
        }

        if (TryCopyClipboardItem(item))
        {
            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            return;
        }

        if (item.IsMtab)
        {
            var result = await OpenMtabAsync(item, openAsSeparateWindows: true);
            if (result is null)
            {
                return;
            }

            StatusText = $"Opened {result.OpenedAsWindowsCount} Explorer window(s).";
            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            ApplyPostOpenWindowBehavior(keepToolOnTop);
            return;
        }

        if (!TryLaunch(item, () => _launcherService.OpenNewWindow(item)))
        {
            return;
        }

        item.LastOpenedDate = DateTime.UtcNow;
        RefreshSmartCollections();
        QueueSave();
        ApplyPostOpenWindowBehavior(keepToolOnTop);
    }

    private void OpenInTerminal(ItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        var keepToolOnTop = ShouldKeepToolOnTopAfterOpen();

        if (item.IsActionItem)
        {
            var workingDirectory = item.LaunchProfile?.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                StatusText = "Action item has no working directory set.";
                return;
            }

            var terminalItem = new ItemModel
            {
                Name = item.Name,
                Path = workingDirectory,
                Type = ItemType.Folder,
                LaunchProfile = item.LaunchProfile ?? new ItemLaunchProfile()
            };
            if (!TryLaunch(item, () => _launcherService.OpenInTerminal(terminalItem)))
            {
                return;
            }

            ApplyPostOpenWindowBehavior(keepToolOnTop);
            return;
        }

        if (TryCopyClipboardItem(item))
        {
            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            return;
        }

        if (item.IsMtab)
        {
            var firstPath = GetMtabFolderPaths(item).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstPath))
            {
                MessageBox.Show("Mtab has no valid folder paths.", "Open in Terminal", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var terminalItem = new ItemModel
            {
                Name = item.Name,
                Path = firstPath,
                Type = ItemType.Folder,
                LaunchProfile = item.LaunchProfile
            };
            if (!TryLaunch(item, () => _launcherService.OpenInTerminal(terminalItem)))
            {
                return;
            }

            item.LastOpenedDate = DateTime.UtcNow;
            RefreshSmartCollections();
            QueueSave();
            ApplyPostOpenWindowBehavior(keepToolOnTop);
            return;
        }

        if (!TryLaunch(item, () => _launcherService.OpenInTerminal(item)))
        {
            return;
        }

        ApplyPostOpenWindowBehavior(keepToolOnTop);
    }

    private static bool ShouldKeepToolOnTopAfterOpen()
    {
        return Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    }

    private void ApplyPostOpenWindowBehavior(bool keepToolOnTop)
    {
        if (Application.Current?.MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        if (keepToolOnTop)
        {
            mainWindow.RestoreAndActivate();
            return;
        }

        if (!Settings.AlwaysOnTop)
        {
            mainWindow.SendBehindOtherWindows();
        }
    }

    public bool TryCopyItemReference(ItemModel? item)
    {
        if (item is null)
        {
            return false;
        }

        try
        {
            if (item.IsClipboardImage)
            {
                if (!_clipboardAssetService.TryCopyImageToClipboard(item))
                {
                    StatusText = "Clipboard image is missing or unavailable.";
                    return false;
                }

                StatusText = $"Copied image item: {item.Name}";
                return true;
            }

            if (!TryBuildItemReferenceCopy(item, out var textToCopy, out var statusText))
            {
                return false;
            }

            Clipboard.SetText(textToCopy);
            StatusText = statusText;
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Failed to copy item reference to clipboard.");
            StatusText = "Clipboard is busy. Try again.";
            return false;
        }
    }

    private void CopyPath(ItemModel? item)
    {
        TryCopyItemReference(item);
    }

    private bool TryCopyClipboardItem(ItemModel item)
    {
        return (item.IsClipboardText || item.IsClipboardImage) && TryCopyItemReference(item);
    }

    private bool TryRunActionItem(ItemModel item)
    {
        if (!item.IsActionItem)
        {
            return false;
        }

        if (_managedActionRunnerService.TryRun(item, out var error))
        {
            StatusText = $"Executed action item: {item.Name}";
            return true;
        }

        StatusText = $"Failed to run action item: {item.Name}";
        MessageBox.Show(
            string.IsNullOrWhiteSpace(error) ? "The action item could not be executed." : error,
            "Action Item",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return false;
    }

    private async Task RetryHealthCheckAsync(ItemModel? item)
    {
        if (item is null || _networkCheckService is null)
        {
            return;
        }

        await _networkCheckService.RetryCheckAsync(item);
        UpdateStatusCounts();
    }

    private bool CanRemoveItemFromCurrentCollection(ItemCollectionActionContext? context)
    {
        return context?.Item is not null
               && context.Collection is not null
               && !context.Collection.IsSmart
               && context.Collection.Model.ItemIds.Contains(context.Item.Id);
    }

    private bool CanRemoveSeparator(CollectionEntryActionContext? context)
    {
        return context?.Entry is not null
               && context.Collection is not null
               && !context.Collection.IsSmart
               && context.Entry.IsSeparator
               && context.Collection.Entries.Contains(context.Entry);
    }

    private bool CanDeleteItemGlobally(ItemModel? item)
    {
        return item is not null;
    }

    private bool CanAddSeparator()
    {
        var targetCollection = ResolveTargetCollection();
        return targetCollection is not null && !targetCollection.IsSmart;
    }

    private bool CanRemoveSelectedItemsFromCurrentCollection()
    {
        return SelectedItems.Count > 0
               && SelectedCollection is not null
               && !SelectedCollection.IsSmart;
    }

    private void RemoveItemFromCurrentCollection(ItemCollectionActionContext? context)
    {
        if (!CanRemoveItemFromCurrentCollection(context))
        {
            return;
        }

        var item = context!.Item!;
        var collection = context.Collection!;
        var action = CreateRemoveItemFromCollectionAction(collection, item);
        if (action is null)
        {
            return;
        }

        ExecuteAction(action);
        SetSelectedItems(SelectedItems.Where(selected => selected.Id != item.Id).ToList());
        if (SelectedCollection == collection && SelectedItem?.Id == item.Id)
        {
            SelectedItem = null;
        }

        if (SelectedCollection == collection && Editor.IsOpen && Editor.Item?.Id == item.Id)
        {
            Editor.Close();
        }

        UpdateStatusCounts();
        StatusText = $"Removed '{item.Name}' from {collection.Name}.";
    }

    private void RemoveSeparator(CollectionEntryActionContext? context)
    {
        if (!CanRemoveSeparator(context))
        {
            return;
        }

        var collection = context!.Collection!;
        var separator = context.Entry!;
        var sourceIndex = collection.Entries.IndexOf(separator);
        if (sourceIndex < 0)
        {
            return;
        }

        var model = separator.Model;
        var action = new DelegateUndoableAction(
            "Remove Separator",
            () =>
            {
                collection.Model.Entries.Remove(model);
                SyncCollectionItemIds(collection.Model);
                collection.ResetEntries(BuildCollectionEntries(collection.Model));
            },
            () =>
            {
                collection.Model.Entries.Insert(Math.Min(sourceIndex, collection.Model.Entries.Count), model);
                SyncCollectionItemIds(collection.Model);
                collection.ResetEntries(BuildCollectionEntries(collection.Model));
            });

        ExecuteAction(action);
        StatusText = $"Deleted separator from {collection.Name}.";
    }

    private void DeleteItemGlobally(ItemModel? item)
    {
        if (!CanDeleteItemGlobally(item))
        {
            return;
        }

        var targetItem = item!;
        var items = new[] { targetItem };
        if (!ConfirmGlobalDelete(items))
        {
            return;
        }

        var action = CreateDeleteItemGloballyAction(targetItem);
        ExecuteAction(action);
        SetSelectedItems(SelectedItems.Where(selected => selected.Id != targetItem.Id).ToList());
        if (SelectedItem?.Id == targetItem.Id)
        {
            SelectedItem = null;
        }

        if (Editor.IsOpen && Editor.Item?.Id == targetItem.Id)
        {
            Editor.Close();
        }

        UpdateStatusCounts();
        StatusText = $"Deleted '{targetItem.Name}' globally.";
    }

    private void EditItem(ItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.IsMtab)
        {
            EditMtab(item);
            return;
        }

        if (item.IsActionItem)
        {
            EditActionItem(item);
            return;
        }

        IsSettingsOpen = false;
        Editor.BeginEdit(item);
    }

    private async void OpenAllItems(CollectionViewModel? collection)
    {
        collection ??= SelectedCollection;
        if (collection is null)
        {
            return;
        }

        foreach (var item in collection.Items.ToList())
        {
            if (item.IsMtab)
            {
                await OpenMtabAsync(item, openAsSeparateWindows: false);
            }
            else if (item.IsActionItem)
            {
                TryRunActionItem(item);
            }
            else
            {
                TryLaunch(item, () => _launcherService.Open(item));
            }

            item.LastOpenedDate = DateTime.UtcNow;
        }

        RefreshSmartCollections();
        QueueSave();
    }

    private void ToggleViewMode()
    {
        Settings.ViewMode = Settings.ViewMode == ViewMode.Grid ? ViewMode.List : ViewMode.Grid;
    }

    private void ToggleCollection()
    {
        if (Collections.Count == 0)
        {
            return;
        }

        if (SelectedCollection is null)
        {
            SelectedCollection = Collections[0];
            return;
        }

        var currentIndex = Collections.IndexOf(SelectedCollection);
        if (currentIndex < 0)
        {
            SelectedCollection = Collections[0];
            return;
        }

        var nextIndex = (currentIndex + 1) % Collections.Count;
        SelectedCollection = Collections[nextIndex];
    }

    private void ToggleSettings()
    {
        if (!IsSettingsOpen)
        {
            CloseToolPane();
            IsAddFormOpen = false;
            Editor.Close();
        }

        IsSettingsOpen = !IsSettingsOpen;
    }

    private void CloseSettings()
    {
        if (!IsSettingsOpen)
        {
            return;
        }

        IsSettingsOpen = false;
    }

    private void AddCollection()
    {
        var suggestedName = BuildNextCollectionName("New Collection");
        var collection = new CollectionModel { Name = suggestedName };
        var vm = new CollectionViewModel(collection, BuildCollectionEntries(collection));

        var dialog = new RenameCollectionWindow(vm, "New Collection")
        {
            Owner = Application.Current.MainWindow,
            IsCollectionNameAvailable = name => IsCollectionNameAvailable(name)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var index = AppData.Collections.Count;
        var action = new DelegateUndoableAction(
            "Add Collection",
            () =>
            {
                if (!AppData.Collections.Contains(collection))
                {
                    AppData.Collections.Insert(Math.Min(index, AppData.Collections.Count), collection);
                }

                if (!Collections.Contains(vm))
                {
                    Collections.Insert(Math.Min(index, Collections.Count), vm);
                }

                SelectedCollection = vm;
            },
            () =>
            {
                AppData.Collections.Remove(collection);
                Collections.Remove(vm);
                SelectedCollection = Collections.FirstOrDefault();
            });

        ExecuteAction(action);
    }

    private bool CreateActionItem(ActionItemEditorViewModel editor, CollectionViewModel? requestedCollection, out string status)
    {
        var targetCollection = ResolveTargetCollection(requestedCollection);
        if (targetCollection is null || targetCollection.IsSmart)
        {
            status = "Select a normal collection before creating an action item.";
            return false;
        }

        var item = new ItemModel
        {
            Name = editor.Name.Trim(),
            Path = string.Empty,
            Type = ItemType.App,
            UseSystemIcon = false,
            CreatedDate = DateTime.UtcNow,
            LaunchProfile = new ItemLaunchProfile
            {
                WorkingDirectory = editor.WorkingDirectory.Trim(),
                RunAsAdmin = editor.RunAsAdmin
            },
            HealthState = ItemHealthState.Healthy,
            IsActionItem = true,
            ActionKind = editor.GetActionKind(),
            ActionContent = editor.Content
        };

        _iconService.QueueIconRefresh(item);
        var allItemsIndex = AllItems.Count;
        var collectionInsertIndex = targetCollection.Model.Entries.Count;

        var action = new DelegateUndoableAction(
            "Create Action Item",
            () =>
            {
                if (!AllItems.Any(existing => existing.Id == item.Id))
                {
                    AllItems.Insert(Math.Min(allItemsIndex, AllItems.Count), item);
                }

                InsertItemIntoCollection(targetCollection, item, collectionInsertIndex);
            },
            () =>
            {
                RemoveItemFromCollection(targetCollection, item);
                AllItems.Remove(item);
            });

        ExecuteAction(action);
        status = $"Created action item '{item.Name}'.";
        return true;
    }

    private bool AddSeparatorToCollection(CollectionViewModel? requestedCollection, out string status)
    {
        var targetCollection = ResolveTargetCollection(requestedCollection);
        if (targetCollection is null || targetCollection.IsSmart)
        {
            status = "Select a normal collection before adding a separator.";
            return false;
        }

        var separator = new CollectionEntryModel
        {
            Kind = CollectionEntryKind.Separator
        };
        var insertIndex = targetCollection.Model.Entries.Count;

        var action = new DelegateUndoableAction(
            "Add Separator",
            () =>
            {
                targetCollection.Model.Entries.Insert(Math.Min(insertIndex, targetCollection.Model.Entries.Count), separator);
                SyncCollectionItemIds(targetCollection.Model);
                targetCollection.ResetEntries(BuildCollectionEntries(targetCollection.Model));
            },
            () =>
            {
                targetCollection.Model.Entries.Remove(separator);
                SyncCollectionItemIds(targetCollection.Model);
                targetCollection.ResetEntries(BuildCollectionEntries(targetCollection.Model));
            });

        ExecuteAction(action);
        status = $"Added separator to '{targetCollection.Name}'.";
        return true;
    }

    private void OpenNewMtabDialog()
    {
        var suggestedName = BuildSuggestedMtabName(Array.Empty<ItemModel>());
        var editor = new MtabEditorWindow("New Mtab", suggestedName)
        {
            Owner = Application.Current.MainWindow
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (!CreateMtabFromPaths(editor.MtabName, editor.ValidPaths, ResolveTargetCollection(), out var status))
        {
            MessageBox.Show(status, "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = status;
    }

    private void OpenNewActionDialog()
    {
        var dialog = new ActionItemWindow("New Action")
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.ViewModel.IsSeparator)
        {
            if (!AddSeparatorToCollection(ResolveTargetCollection(), out var separatorStatus))
            {
                MessageBox.Show(separatorStatus, "Separator", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText = separatorStatus;
            return;
        }

        if (!CreateActionItem(dialog.ViewModel, ResolveTargetCollection(), out var status))
        {
            MessageBox.Show(status, "Action Item", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = status;
    }

    private void AddSeparator()
    {
        if (!AddSeparatorToCollection(ResolveTargetCollection(), out var status))
        {
            MessageBox.Show(status, "Add Separator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = status;
    }

    private void EditMtab(ItemModel? item)
    {
        if (item is null || !item.IsMtab)
        {
            return;
        }

        var existingPaths = GetMtabFolderPaths(item);
        var editor = new MtabEditorWindow("Edit Mtab", item.Name, existingPaths)
        {
            Owner = Application.Current.MainWindow
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (!UpdateMtabFromPaths(item, editor.MtabName, editor.ValidPaths, out var status))
        {
            MessageBox.Show(status, "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = status;
    }

    private void EditActionItem(ItemModel item)
    {
        var dialog = new ActionItemWindow("Edit Action")
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ViewModel.Load(item);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        item.Name = dialog.ViewModel.Name.Trim();
        item.IsActionItem = true;
        item.ActionKind = dialog.ViewModel.GetActionKind();
        item.ActionContent = dialog.ViewModel.Content;
        item.Path = string.Empty;
        item.Type = ItemType.App;
        item.UseSystemIcon = false;
        item.LaunchProfile ??= new ItemLaunchProfile();
        item.LaunchProfile.WorkingDirectory = dialog.ViewModel.WorkingDirectory.Trim();
        item.LaunchProfile.RunAsAdmin = dialog.ViewModel.RunAsAdmin;
        _iconService.QueueIconRefresh(item);
        RefreshSmartCollections();
        QueueSave();
        StatusText = $"Updated action item '{item.Name}'.";
    }

    public bool TryHandleInternalItemDropAction(ItemModel sourceItem, ItemModel? targetItem)
    {
        if (targetItem is null || sourceItem.Id == targetItem.Id)
        {
            return false;
        }

        if (!IsFolderLikeItem(sourceItem) || !IsFolderLikeItem(targetItem))
        {
            return false;
        }

        var choice = ShowDropActionDialog(sourceItem.Name, targetItem.Name);
        if (choice == DropItemActionChoice.Cancel)
        {
            return true;
        }

        if (choice == DropItemActionChoice.CreateMtab)
        {
            var suggestedName = BuildSuggestedMtabName(new[] { targetItem, sourceItem });
            var editor = new MtabEditorWindow("Create Mtab", suggestedName, new[] { targetItem.Path, sourceItem.Path })
            {
                Owner = Application.Current.MainWindow
            };

            if (editor.ShowDialog() != true)
            {
                return true;
            }

            if (!CreateMtabFromPaths(editor.MtabName, editor.ValidPaths, ResolveTargetCollection(), out var status))
            {
                MessageBox.Show(status, "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }

            StatusText = status;
            return true;
        }

        var suggestedCollectionName = BuildNextCollectionName(BuildSuggestedCollectionName(new[] { targetItem, sourceItem }));
        var collectionName = PromptForName("Create Collection", "Collection name:", suggestedCollectionName);
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return true;
        }

        if (!CreateCollectionWithItems(collectionName, new[] { targetItem, sourceItem }, out var collectionStatus))
        {
            MessageBox.Show(collectionStatus, "Create Collection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }

        StatusText = collectionStatus;
        return true;
    }

    public bool TryHandleExternalFolderDropAction(IEnumerable<string> droppedPaths, ItemModel? targetItem)
    {
        if (targetItem is null || !IsFolderLikeItem(targetItem))
        {
            return false;
        }

        var normalizedDroppedFolders = droppedPaths
            .Select(path =>
            {
                if (!PathNormalizationHelper.TryNormalizeUserPath(path, out var normalizedPath, out _))
                {
                    return null;
                }

                return IsFolderPath(normalizedPath) ? normalizedPath : null;
            })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        if (normalizedDroppedFolders.Count == 0)
        {
            return false;
        }

        var choice = ShowDropActionDialog($"{normalizedDroppedFolders.Count} dropped folder(s)", targetItem.Name);
        if (choice == DropItemActionChoice.Cancel)
        {
            return true;
        }

        if (choice == DropItemActionChoice.CreateMtab)
        {
            var initialPaths = new List<string> { targetItem.Path };
            initialPaths.AddRange(normalizedDroppedFolders);

            var suggestedName = BuildSuggestedMtabName(new[] { targetItem });
            var editor = new MtabEditorWindow("Create Mtab", suggestedName, initialPaths)
            {
                Owner = Application.Current.MainWindow
            };

            if (editor.ShowDialog() != true)
            {
                return true;
            }

            if (!CreateMtabFromPaths(editor.MtabName, editor.ValidPaths, ResolveTargetCollection(), out var status))
            {
                MessageBox.Show(status, "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }

            StatusText = status;
            return true;
        }

        var collectionName = PromptForName(
            "Create Collection",
            "Collection name:",
            BuildNextCollectionName(BuildSuggestedCollectionName(new[] { targetItem })));

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return true;
        }

        var candidatePaths = new List<string> { targetItem.Path };
        candidatePaths.AddRange(normalizedDroppedFolders);

        if (!CreateCollectionWithPaths(collectionName, candidatePaths, out var collectionStatus))
        {
            MessageBox.Show(collectionStatus, "Create Collection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }

        StatusText = collectionStatus;
        return true;
    }

    private void DeleteCollection(CollectionViewModel? collection)
    {
        collection ??= SelectedCollection;
        if (collection is null)
        {
            return;
        }

        var index = Collections.IndexOf(collection);
        if (index < 0)
        {
            return;
        }

        var action = new DelegateUndoableAction(
            "Delete Collection",
            () =>
            {
                AppData.Collections.Remove(collection.Model);
                Collections.Remove(collection);
                SelectedCollection = Collections.FirstOrDefault();
            },
            () =>
            {
                AppData.Collections.Insert(Math.Min(index, AppData.Collections.Count), collection.Model);
                Collections.Insert(Math.Min(index, Collections.Count), collection);
                SelectedCollection = collection;
            });

        ExecuteAction(action);
    }

    private void RenameCollection(CollectionViewModel? collection)
    {
        collection ??= SelectedCollection;
        if (collection is null)
        {
            return;
        }

        var oldName = collection.Name;
        var oldSmart = collection.IsSmart;
        var oldSmartType = collection.SmartType;
        var oldSmartValue = collection.SmartValue;

        var dialog = new RenameCollectionWindow(collection)
        {
            Owner = Application.Current.MainWindow,
            IsCollectionNameAvailable = name => IsCollectionNameAvailable(name, collection)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = collection.Name;
        var newSmart = collection.IsSmart;
        var newSmartType = collection.SmartType;
        var newSmartValue = collection.SmartValue;

        if (oldName == newName && oldSmart == newSmart && oldSmartType == newSmartType && oldSmartValue == newSmartValue)
        {
            return;
        }

        var action = new DelegateUndoableAction(
            "Rename Collection",
            () =>
            {
                collection.Name = newName;
                collection.IsSmart = newSmart;
                collection.SmartType = newSmartType;
                collection.SmartValue = newSmartValue;
                RefreshSmartCollections();
            },
            () =>
            {
                collection.Name = oldName;
                collection.IsSmart = oldSmart;
                collection.SmartType = oldSmartType;
                collection.SmartValue = oldSmartValue;
                RefreshSmartCollections();
            });

        _historyService.Execute(action);
        QueueSave();
    }

    private void Undo()
    {
        if (_historyService.TryUndo())
        {
            RefreshSmartCollections();
            QueueSave();
        }
    }

    private void Redo()
    {
        if (_historyService.TryRedo())
        {
            RefreshSmartCollections();
            QueueSave();
        }
    }

    private async void OpenSelectedItems()
    {
        foreach (var item in SelectedItems.ToList())
        {
            if (item.IsMtab)
            {
                await OpenMtabAsync(item, openAsSeparateWindows: false);
            }
            else if (item.IsActionItem)
            {
                TryRunActionItem(item);
            }
            else if (!item.IsClipboardText && !item.IsClipboardImage)
            {
                TryLaunch(item, () => _launcherService.Open(item));
            }
            else
            {
                TryCopyClipboardItem(item);
            }

            item.LastOpenedDate = DateTime.UtcNow;
        }

        RefreshSmartCollections();
        QueueSave();
    }

    private void RemoveSelectedItemsFromCurrentCollection()
    {
        if (!CanRemoveSelectedItemsFromCurrentCollection())
        {
            return;
        }

        var sourceCollection = SelectedCollection!;
        var actions = SelectedItems
            .Distinct()
            .Select(item => CreateRemoveItemFromCollectionAction(sourceCollection, item))
            .Where(action => action is not null)
            .Cast<IUndoableAction>()
            .ToList();

        if (actions.Count == 0)
        {
            return;
        }

        ExecuteAction(new CompositeUndoableAction("Remove Selected Items From Collection", actions));
        SetSelectedItems(Array.Empty<ItemModel>());
        if (Editor.IsOpen && Editor.Item is not null && !sourceCollection.Model.ItemIds.Contains(Editor.Item.Id))
        {
            Editor.Close();
        }

        UpdateStatusCounts();
        StatusText = $"Removed {actions.Count} item(s) from {sourceCollection.Name}.";
    }

    private void DeleteSelectedItemsGlobally()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        var items = SelectedItems.Distinct().ToList();
        if (!ConfirmGlobalDelete(items))
        {
            return;
        }

        var actions = items
            .Select(CreateDeleteItemGloballyAction)
            .ToList();

        var composite = new CompositeUndoableAction("Delete Selected Items Globally", actions);
        ExecuteAction(composite);
        SetSelectedItems(Array.Empty<ItemModel>());
        if (Editor.IsOpen && Editor.Item is not null && items.Any(item => item.Id == Editor.Item.Id))
        {
            Editor.Close();
        }

        UpdateStatusCounts();
        StatusText = $"Deleted {items.Count} item(s) globally.";
    }

    private void MoveSelectedToCollection()
    {
        var target = BulkTargetCollection;
        if (target is null || target.IsSmart || SelectedItems.Count == 0)
        {
            return;
        }

        var source = SelectedCollection;
        var actions = new List<IUndoableAction>();

        foreach (var item in SelectedItems.Distinct())
        {
            var capturedItem = item;
            var sourceIndex = -1;

            actions.Add(new DelegateUndoableAction(
                $"Move {capturedItem.Name}",
                () =>
                {
                    // Capture the source slot at apply time and append to the target's
                    // end so multi-item moves keep order and undo restores positions.
                    if (source is not null && !source.IsSmart)
                    {
                        sourceIndex = GetEntryIndex(source, capturedItem);
                        RemoveItemFromCollection(source, capturedItem);
                    }

                    InsertItemIntoCollection(target, capturedItem, target.Model.Entries.Count);
                },
                () =>
                {
                    RemoveItemFromCollection(target, capturedItem);
                    if (source is not null && !source.IsSmart)
                    {
                        InsertItemIntoCollection(source, capturedItem, sourceIndex >= 0 ? sourceIndex : source.Model.Entries.Count);
                    }
                }));
        }

        if (actions.Count == 0)
        {
            return;
        }

        ExecuteAction(new CompositeUndoableAction("Move Selected Items", actions));
        StatusText = $"Moved {actions.Count} item(s) to {target.Name}.";
    }

    private void AddTagToSelected()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        var tag = Interaction.InputBox("Tag to add to selected items:", "Bulk Tag", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var actions = new List<IUndoableAction>();
        foreach (var item in SelectedItems.Distinct())
        {
            if (item.Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            actions.Add(new DelegateUndoableAction(
                $"Add tag '{tag}'",
                () => item.Tags.Add(tag),
                () =>
                {
                    var toRemove = item.Tags.FirstOrDefault(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase));
                    if (toRemove is not null)
                    {
                        item.Tags.Remove(toRemove);
                    }
                }));
        }

        if (actions.Count == 0)
        {
            StatusText = "Tag already exists on all selected items.";
            return;
        }

        ExecuteAction(new CompositeUndoableAction($"Tag {actions.Count} items", actions));
        StatusText = $"Added tag '{tag}' to {actions.Count} item(s).";
    }

    private void ToggleFavoriteSelected()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        var targetFavorite = SelectedItems.Any(item => !item.IsFavorite);
        var actions = new List<IUndoableAction>();

        foreach (var item in SelectedItems.Distinct())
        {
            var oldFavorite = item.IsFavorite;
            actions.Add(new DelegateUndoableAction(
                "Toggle Favorite",
                () => item.IsFavorite = targetFavorite,
                () => item.IsFavorite = oldFavorite));
        }

        ExecuteAction(new CompositeUndoableAction("Toggle Favorite (Bulk)", actions));
        StatusText = targetFavorite
            ? $"Marked {actions.Count} item(s) as favorite."
            : $"Removed favorite on {actions.Count} item(s).";
    }

    private void BeginQuickEdit()
    {
        if (SelectedItem is null)
        {
            return;
        }

        QuickEditName = SelectedItem.Name;
        QuickEditPath = SelectedItem.Path;
        QuickEditTags = string.Join(", ", SelectedItem.Tags);
        IsQuickEditOpen = true;
        StatusText = $"Quick edit: {SelectedItem.Name}";
    }

    private void BeginQuickEditForItem(ItemModel? item)
    {
        if (item is not null)
        {
            SelectedItem = item;
        }

        BeginQuickEdit();
    }

    private void SaveQuickEdit()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var targetItem = SelectedItem;
        var oldName = targetItem.Name;
        var oldPath = targetItem.Path;
        var oldTags = targetItem.Tags.ToList();

        var newName = string.IsNullOrWhiteSpace(QuickEditName) ? targetItem.Name : QuickEditName.Trim();
        var newPath = string.IsNullOrWhiteSpace(QuickEditPath) ? targetItem.Path : QuickEditPath.Trim();
        var newTags = QuickEditTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var action = new DelegateUndoableAction(
            "Quick Edit Item",
            () =>
            {
                targetItem.Name = newName;
                targetItem.Path = newPath;
                targetItem.Tags.Clear();
                foreach (var tag in newTags)
                {
                    targetItem.Tags.Add(tag);
                }

                _iconService.QueueIconRefresh(targetItem);
            },
            () =>
            {
                targetItem.Name = oldName;
                targetItem.Path = oldPath;
                targetItem.Tags.Clear();
                foreach (var tag in oldTags)
                {
                    targetItem.Tags.Add(tag);
                }

                _iconService.QueueIconRefresh(targetItem);
            });

        ExecuteAction(action);
        IsQuickEditOpen = false;
        StatusText = $"Updated {targetItem.Name}.";
    }

    private void CancelQuickEdit()
    {
        IsQuickEditOpen = false;
        StatusText = "Quick edit canceled.";
    }

    private void UpdateMtabDerivedData()
    {
        var itemMap = AllItems.ToDictionary(item => item.Id);
        foreach (var mtab in AllItems.Where(item => item.IsMtab))
        {
            var normalizedPaths = NormalizeMtabPaths(mtab.MtabPaths);
            if (normalizedPaths.Count == 0 && mtab.MtabMemberIds.Count > 0)
            {
                var legacyPaths = mtab.MtabMemberIds
                    .Where(memberId => memberId != mtab.Id && itemMap.TryGetValue(memberId, out var member) && IsFolderLikeItem(member))
                    .Select(memberId => itemMap[memberId].Path);
                normalizedPaths = NormalizeMtabPaths(legacyPaths);
            }

            if (!normalizedPaths.SequenceEqual(mtab.MtabPaths, StringComparer.OrdinalIgnoreCase))
            {
                mtab.MtabPaths = new ObservableCollection<string>(normalizedPaths);
            }

            if (mtab.MtabMemberIds.Count > 0)
            {
                // Keep mtab standalone from item links so deleting normal items does not break it.
                mtab.MtabMemberIds = new ObservableCollection<Guid>();
            }

            mtab.MtabSearchHint = string.Join(' ', normalizedPaths);
            mtab.HealthState = normalizedPaths.Count == 0 ? ItemHealthState.Missing : ItemHealthState.Healthy;
            if (mtab.Type != ItemType.Folder)
            {
                mtab.Type = ItemType.Folder;
            }
        }
    }

    private async Task<ExplorerMtabOpenResult?> OpenMtabAsync(ItemModel mtab, bool openAsSeparateWindows)
    {
        var folderPaths = GetMtabFolderPaths(mtab);
        if (folderPaths.Count == 0)
        {
            MessageBox.Show("This Mtab has no valid folder members.", "Mtab", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        ExplorerMtabOpenResult result;
        if (openAsSeparateWindows)
        {
            await _explorerTabAutomationService.OpenAsSeparateWindowsAsync(folderPaths);
            result = new ExplorerMtabOpenResult
            {
                RequestedCount = folderPaths.Count,
                TabsSupported = false,
                OpenedAsWindowsCount = folderPaths.Count
            };
        }
        else
        {
            result = await _explorerTabAutomationService.OpenMtabAsync(folderPaths);
        }

        LogService.Instance.Log($"Mtab open '{mtab.Name}': {result.BuildSummary()}");
        return result;
    }

    private List<string> GetMtabFolderPaths(ItemModel mtab)
    {
        var directPaths = NormalizeMtabPaths(mtab.MtabPaths);
        if (directPaths.Count > 0)
        {
            return directPaths;
        }

        var legacyPaths = mtab.MtabMemberIds
            .Select(memberId => AllItems.FirstOrDefault(item => item.Id == memberId))
            .Where(member => member is not null && IsFolderLikeItem(member))
            .Select(member => member!.Path);

        return NormalizeMtabPaths(legacyPaths);
    }

    private bool CreateMtabFromPaths(
        string mtabName,
        IEnumerable<string> rawPaths,
        CollectionViewModel? requestedCollection,
        out string status)
    {
        status = string.Empty;
        var normalizedPaths = NormalizeMtabPaths(rawPaths);
        if (normalizedPaths.Count < 2)
        {
            status = "At least two valid folder paths are required for Mtab.";
            return false;
        }

        return CreateMtabWithPaths(mtabName, normalizedPaths, requestedCollection, out status);
    }

    private bool UpdateMtabFromPaths(ItemModel mtab, string mtabName, IEnumerable<string> rawPaths, out string status)
    {
        status = string.Empty;
        if (mtab is null || !mtab.IsMtab)
        {
            status = "Selected item is not an Mtab.";
            return false;
        }

        var normalizedPaths = NormalizeMtabPaths(rawPaths);
        if (normalizedPaths.Count < 2)
        {
            status = "At least two valid folder paths are required for Mtab.";
            return false;
        }

        var trimmedName = string.IsNullOrWhiteSpace(mtabName) ? mtab.Name : mtabName.Trim();
        var oldName = mtab.Name;
        var oldPaths = mtab.MtabPaths.ToList();
        var oldMemberIds = mtab.MtabMemberIds.ToList();

        var action = new DelegateUndoableAction(
            "Edit Mtab",
            () =>
            {
                mtab.Name = trimmedName;
                mtab.MtabPaths = new ObservableCollection<string>(normalizedPaths);
                mtab.MtabMemberIds = new ObservableCollection<Guid>();
                _iconService.QueueIconRefresh(mtab);
            },
            () =>
            {
                mtab.Name = oldName;
                mtab.MtabPaths = new ObservableCollection<string>(oldPaths);
                mtab.MtabMemberIds = new ObservableCollection<Guid>(oldMemberIds);
                _iconService.QueueIconRefresh(mtab);
            });

        ExecuteAction(action);
        status = $"Updated Mtab '{mtab.Name}' with {normalizedPaths.Count} folders.";
        return true;
    }

    private bool CreateMtabWithPaths(
        string mtabName,
        IReadOnlyList<string> folderPaths,
        CollectionViewModel? requestedCollection,
        out string status)
    {
        status = string.Empty;
        var targetCollection = ResolveTargetCollection(requestedCollection);
        if (targetCollection is null || targetCollection.IsSmart)
        {
            status = "Select a normal collection before creating Mtab.";
            return false;
        }

        var trimmedName = string.IsNullOrWhiteSpace(mtabName)
            ? BuildSuggestedMtabNameFromPaths(folderPaths)
            : mtabName.Trim();
        var normalizedPaths = NormalizeMtabPaths(folderPaths);
        if (normalizedPaths.Count < 2)
        {
            status = "At least two folder members are required.";
            return false;
        }

        var mtabItem = new ItemModel
        {
            Name = trimmedName,
            Path = string.Empty,
            Type = ItemType.Folder,
            UseSystemIcon = true,
            CreatedDate = DateTime.UtcNow,
            LaunchProfile = new ItemLaunchProfile(),
            HealthState = ItemHealthState.Healthy,
            IsMtab = true,
            MtabMemberIds = new ObservableCollection<Guid>(),
            MtabPaths = new ObservableCollection<string>(normalizedPaths)
        };
        _iconService.QueueIconRefresh(mtabItem);

        var allItemsInsertIndex = AllItems.Count;
        var collectionInsertIndex = targetCollection.Model.ItemIds.Count;

        var action = new DelegateUndoableAction(
            "Create Mtab",
            () =>
            {
                if (!AllItems.Any(existing => existing.Id == mtabItem.Id))
                {
                    AllItems.Insert(Math.Min(allItemsInsertIndex, AllItems.Count), mtabItem);
                }

                InsertItemIntoCollection(targetCollection, mtabItem, collectionInsertIndex);
            },
            () =>
            {
                RemoveItemFromCollection(targetCollection, mtabItem);
                AllItems.Remove(mtabItem);
            });

        ExecuteAction(action);
        status = $"Created Mtab '{trimmedName}' with {normalizedPaths.Count} folders.";
        return true;
    }

    private bool AppendPathsToMtab(
        ItemModel mtab,
        IReadOnlyList<string> incomingPaths,
        out int addedCount,
        out int skippedExistingCount)
    {
        addedCount = 0;
        skippedExistingCount = 0;

        if (mtab is null || !mtab.IsMtab || incomingPaths.Count == 0)
        {
            return false;
        }

        var existingNormalizedPaths = NormalizeMtabPaths(mtab.MtabPaths);
        var existingKeys = new HashSet<string>(
            existingNormalizedPaths
                .Select(PathNormalizationHelper.GetPathKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Cast<string>(),
            StringComparer.OrdinalIgnoreCase);

        var pathsToAppend = new List<string>();
        foreach (var incomingPath in incomingPaths)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(incomingPath, out var normalizedPath, out var pathKey))
            {
                continue;
            }

            if (!IsFolderPath(normalizedPath))
            {
                continue;
            }

            if (!existingKeys.Add(pathKey))
            {
                skippedExistingCount++;
                continue;
            }

            pathsToAppend.Add(normalizedPath);
        }

        if (pathsToAppend.Count == 0)
        {
            return false;
        }

        var previousPaths = mtab.MtabPaths.ToList();
        var previousMemberIds = mtab.MtabMemberIds.ToList();
        var mergedPaths = existingNormalizedPaths.Concat(pathsToAppend).ToList();

        var action = new DelegateUndoableAction(
            "Append Explorer Folders To Mtab",
            () =>
            {
                mtab.MtabPaths = new ObservableCollection<string>(mergedPaths);
                mtab.MtabMemberIds = new ObservableCollection<Guid>();
                _iconService.QueueIconRefresh(mtab);
            },
            () =>
            {
                mtab.MtabPaths = new ObservableCollection<string>(previousPaths);
                mtab.MtabMemberIds = new ObservableCollection<Guid>(previousMemberIds);
                _iconService.QueueIconRefresh(mtab);
            });

        ExecuteAction(action);
        addedCount = pathsToAppend.Count;
        return true;
    }

    private bool CreateCollectionWithItems(string collectionName, IEnumerable<ItemModel> items, out string status)
    {
        status = string.Empty;
        var distinctItems = items
            .Where(item => item is not null)
            .DistinctBy(item => item.Id)
            .ToList();

        if (distinctItems.Count < 2)
        {
            status = "At least two items are required to create a collection.";
            return false;
        }

        var normalizedName = string.IsNullOrWhiteSpace(collectionName)
            ? BuildSuggestedCollectionName(distinctItems)
            : collectionName.Trim();

        return CreateCollectionWithResolvedItems(normalizedName, distinctItems, Array.Empty<ItemModel>(), out status);
    }

    private bool CreateCollectionWithPaths(string collectionName, IEnumerable<string> rawPaths, out string status)
    {
        status = string.Empty;
        var resolved = ResolveFolderItemsFromPaths(rawPaths);
        if (resolved.folderItems.Count < 2)
        {
            status = "At least two valid folder paths are required for collection creation.";
            return false;
        }

        var normalizedName = string.IsNullOrWhiteSpace(collectionName)
            ? BuildSuggestedCollectionName(resolved.folderItems)
            : collectionName.Trim();

        return CreateCollectionWithResolvedItems(normalizedName, resolved.folderItems, resolved.newItems, out status);
    }

    private bool CreateCollectionWithResolvedItems(
        string collectionName,
        IReadOnlyList<ItemModel> items,
        IReadOnlyList<ItemModel> newItems,
        out string status)
    {
        status = string.Empty;
        collectionName = collectionName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            status = "Collection name is required.";
            return false;
        }

        if (!IsCollectionNameAvailable(collectionName))
        {
            status = $"Collection '{collectionName}' already exists.";
            return false;
        }

        var model = new CollectionModel { Name = collectionName };
        foreach (var item in items)
        {
            if (!model.ItemIds.Contains(item.Id))
            {
                model.Entries.Add(new CollectionEntryModel
                {
                    Kind = CollectionEntryKind.Item,
                    ItemId = item.Id
                });
            }
        }
        SyncCollectionItemIds(model);

        var vm = new CollectionViewModel(model, BuildCollectionEntries(model));
        var index = AppData.Collections.Count;
        var allItemsInsertIndex = AllItems.Count;

        var action = new DelegateUndoableAction(
            "Create Collection from Drop",
            () =>
            {
                InsertNewItems(newItems, allItemsInsertIndex);

                if (!AppData.Collections.Contains(model))
                {
                    AppData.Collections.Insert(Math.Min(index, AppData.Collections.Count), model);
                }

                if (!Collections.Contains(vm))
                {
                    Collections.Insert(Math.Min(index, Collections.Count), vm);
                }

                SelectedCollection = vm;
            },
            () =>
            {
                AppData.Collections.Remove(model);
                Collections.Remove(vm);
                RemoveNewItems(newItems);
                SelectedCollection = Collections.FirstOrDefault();
            });

        ExecuteAction(action);
        status = $"Created collection '{collectionName}' with {model.ItemIds.Count} items.";
        return true;
    }

    private (List<ItemModel> folderItems, List<ItemModel> newItems) ResolveFolderItemsFromPaths(IEnumerable<string> rawPaths)
    {
        var folderItems = new List<ItemModel>();
        var newItems = new List<ItemModel>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in rawPaths)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(rawPath, out var normalizedPath, out var pathKey))
            {
                continue;
            }

            if (!seenKeys.Add(pathKey))
            {
                continue;
            }

            if (!IsFolderPath(normalizedPath))
            {
                continue;
            }

            var existingItem = FindExistingItem(pathKey);
            if (existingItem is not null)
            {
                if (IsFolderLikeItem(existingItem))
                {
                    folderItems.Add(existingItem);
                }

                continue;
            }

            var newItem = new ItemModel
            {
                Name = GetDefaultName(normalizedPath),
                Path = normalizedPath,
                Type = ItemType.Folder,
                UseSystemIcon = true,
                CreatedDate = DateTime.UtcNow,
                LaunchProfile = new ItemLaunchProfile(),
                HealthState = ItemHealthState.Healthy
            };

            _iconService.QueueIconRefresh(newItem);
            folderItems.Add(newItem);
            newItems.Add(newItem);
        }

        return (folderItems, newItems);
    }

    private static string BuildSuggestedMtabName(IEnumerable<ItemModel> members)
    {
        var names = members
            .Where(IsFolderLikeItem)
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (names.Count == 0)
        {
            return "New Mtab";
        }

        return string.Join(" + ", names);
    }

    private static string BuildSuggestedMtabNameFromPaths(IEnumerable<string> paths)
    {
        var names = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                var trimmed = path.TrimEnd('\\', '/');
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return string.Empty;
                }

                var name = System.IO.Path.GetFileName(trimmed);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                return trimmed;
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (names.Count == 0)
        {
            return "New Mtab";
        }

        return string.Join(" + ", names);
    }

    private static string BuildSuggestedCollectionName(IEnumerable<ItemModel> members)
    {
        var baseName = BuildSuggestedMtabName(members);
        return baseName == "New Mtab" ? "New Collection" : $"{baseName} Collection";
    }

    private static DropItemActionChoice ShowDropActionDialog(string sourceLabel, string targetLabel)
    {
        var prompt = $"Dropped \"{sourceLabel}\" on \"{targetLabel}\".\nChoose what to create:";
        var dialog = new DropItemActionWindow(prompt)
        {
            Owner = Application.Current.MainWindow
        };

        _ = dialog.ShowDialog();
        return dialog.SelectedAction;
    }

    private static string? PromptForName(string title, string prompt, string defaultValue)
    {
        var input = Interaction.InputBox(prompt, title, defaultValue).Trim();
        return string.IsNullOrWhiteSpace(input) ? null : input;
    }

    private bool IsCollectionNameAvailable(string name, CollectionViewModel? excludedCollection = null)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return false;
        }

        return !Collections.Any(existing =>
            !ReferenceEquals(existing, excludedCollection) &&
            !string.IsNullOrWhiteSpace(existing.Name) &&
            string.Equals(existing.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildNextCollectionName(string baseName, CollectionViewModel? excludedCollection = null)
    {
        var normalizedBaseName = string.IsNullOrWhiteSpace(baseName)
            ? "New Collection"
            : baseName.Trim();

        var maxSuffix = -1;
        foreach (var collection in Collections)
        {
            if (ReferenceEquals(collection, excludedCollection))
            {
                continue;
            }

            if (TryGetCollectionNameSuffix(collection.Name, normalizedBaseName, out var suffix))
            {
                maxSuffix = Math.Max(maxSuffix, suffix);
            }
        }

        return maxSuffix < 0
            ? normalizedBaseName
            : $"{normalizedBaseName}_{maxSuffix + 1}";
    }

    private static bool TryGetCollectionNameSuffix(string? collectionName, string baseName, out int suffix)
    {
        suffix = -1;
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return false;
        }

        var trimmedName = collectionName.Trim();
        if (string.Equals(trimmedName, baseName, StringComparison.OrdinalIgnoreCase))
        {
            suffix = 0;
            return true;
        }

        var prefix = $"{baseName}_";
        if (!trimmedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(trimmedName[prefix.Length..], out suffix) && suffix >= 1;
    }

    private static bool IsFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Directory.Exists(path) || path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static List<string> NormalizeExplorerFolderPaths(
        IReadOnlyList<string> rawPaths,
        out int invalidCount,
        out int duplicatePayloadCount)
    {
        invalidCount = 0;
        duplicatePayloadCount = 0;

        var normalizedFolders = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in rawPaths)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(rawPath, out var normalizedPath, out var pathKey))
            {
                invalidCount++;
                continue;
            }

            if (!seenKeys.Add(pathKey))
            {
                duplicatePayloadCount++;
                continue;
            }

            if (!IsFolderPath(normalizedPath))
            {
                invalidCount++;
                continue;
            }

            normalizedFolders.Add(normalizedPath);
        }

        return normalizedFolders;
    }

    private static List<string> NormalizeMtabPaths(IEnumerable<string>? rawPaths)
    {
        var normalizedPaths = new List<string>();
        var seenPathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rawPaths is null)
        {
            return normalizedPaths;
        }

        foreach (var rawPath in rawPaths)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(rawPath, out var normalizedPath, out var pathKey))
            {
                continue;
            }

            if (!seenPathKeys.Add(pathKey))
            {
                continue;
            }

            if (!IsFolderPath(normalizedPath))
            {
                continue;
            }

            normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths;
    }

    private static bool IsFolderLikeItem(ItemModel? item)
    {
        return item is not null
               && !item.IsClipboardText
               && !item.IsClipboardImage
               && !item.IsActionItem
               && !item.IsMtab
               && item.Type == ItemType.Folder
               && !string.IsNullOrWhiteSpace(item.Path);
    }

    private void InsertNewItems(IReadOnlyList<ItemModel> newItems, int allItemsInsertIndex)
    {
        var insertionIndex = Math.Clamp(allItemsInsertIndex, 0, AllItems.Count);
        foreach (var newItem in newItems.Where(newItem => !AllItems.Any(existing => existing.Id == newItem.Id)))
        {
            AllItems.Insert(insertionIndex, newItem);
            insertionIndex++;
        }
    }

    private void RemoveNewItems(IReadOnlyList<ItemModel> newItems)
    {
        foreach (var newItem in newItems)
        {
            foreach (var collection in Collections)
            {
                RemoveItemFromCollection(collection, newItem);
            }

            AllItems.Remove(newItem);
        }
    }

    private void RefreshAllIcons()
    {
        foreach (var item in AllItems)
        {
            _iconService.QueueIconRefresh(item);
        }
    }

    private ItemType DetermineItemType(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            return ItemType.Folder;
        }

        if (System.IO.File.Exists(path))
        {
            return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ItemType.App : ItemType.File;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return ItemType.Folder;
        }

        return ItemType.File;
    }

    private string GetDefaultName(string path)
    {
        var leafName = GetPathLeafName(path);
        if (IsFolderPath(path))
        {
            if (!string.IsNullOrWhiteSpace(leafName))
            {
                return leafName;
            }

            if (System.IO.Directory.Exists(path))
            {
                return new System.IO.DirectoryInfo(path).Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(leafName))
        {
            return System.IO.Path.GetFileNameWithoutExtension(leafName);
        }

        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    private bool RepairGeneratedFolderNames()
    {
        var changed = false;
        foreach (var item in AllItems)
        {
            changed |= RepairGeneratedFolderName(item);
        }

        return changed;
    }

    private bool RepairGeneratedFolderName(ItemModel item)
    {
        if (item.IsClipboardText || item.IsMtab || item.Type != ItemType.Folder || string.IsNullOrWhiteSpace(item.Path))
        {
            return false;
        }

        var correctedName = GetDefaultName(item.Path);
        if (string.IsNullOrWhiteSpace(correctedName) || string.Equals(item.Name, correctedName, StringComparison.Ordinal))
        {
            return false;
        }

        var currentName = item.Name?.Trim() ?? string.Empty;
        if (!LooksLikeAutoGeneratedPathName(currentName))
        {
            return false;
        }

        item.Name = correctedName;
        return true;
    }

    private static string GetPathLeafName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim().TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = trimmedPath.LastIndexOfAny(new[] { '\\', '/' });
        if (lastSeparatorIndex < 0 || lastSeparatorIndex == trimmedPath.Length - 1)
        {
            return trimmedPath;
        }

        return trimmedPath[(lastSeparatorIndex + 1)..];
    }

    private static bool LooksLikeAutoGeneratedPathName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.StartsWith("\\\\", StringComparison.Ordinal)
               || System.IO.Path.IsPathRooted(name)
               || name.Contains('\\')
               || name.Contains('/');
    }

    private (int addedNew, int linkedExisting, int skippedInvalid, int skippedDuplicate) ApplyClipboardReview(IReadOnlyList<ClipboardReviewEntry> entries, CollectionViewModel? targetCollection)
    {
        var addedNew = 0;
        var linkedExisting = 0;
        var skippedInvalid = 0;
        var skippedDuplicate = 0;
        var changed = false;

        foreach (var entry in entries)
        {
            if (!entry.WillApply)
            {
                if (entry.Status == ClipboardReviewStatus.Invalid)
                {
                    skippedInvalid++;
                }
                else
                {
                    skippedDuplicate++;
                }

                continue;
            }

            var result = AddOrLinkItem(entry.Path, targetCollection, suppressRefreshAndSave: true);
            switch (result)
            {
                case AddPathOutcome.AddedNew:
                    addedNew++;
                    changed = true;
                    break;
                case AddPathOutcome.LinkedExisting:
                    linkedExisting++;
                    changed = true;
                    break;
                case AddPathOutcome.Invalid:
                    skippedInvalid++;
                    break;
                default:
                    skippedDuplicate++;
                    break;
            }
        }

        if (changed)
        {
            RefreshSmartCollections();
            QueueSave();
        }

        return (addedNew, linkedExisting, skippedInvalid, skippedDuplicate);
    }

    private List<ClipboardReviewEntry> BuildClipboardReviewEntriesFromClipboard(CollectionViewModel? requestedCollection = null)
    {
        var rawCandidates = new List<string>();
        if (Clipboard.ContainsFileDropList())
        {
            rawCandidates.AddRange(Clipboard.GetFileDropList().Cast<string>());
        }

        if (Clipboard.ContainsText())
        {
            rawCandidates.AddRange(PathNormalizationHelper.ExtractClipboardCandidates(Clipboard.GetText()));
        }

        return BuildClipboardReviewEntriesFromPaths(rawCandidates, requestedCollection ?? ResolveTargetCollection());
    }

    private bool TryAddClipboardTextItem(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalizedText = text.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var targetCollection = ResolveTargetCollection();
        var item = new ItemModel
        {
            Name = BuildClipboardTextTitle(normalizedText),
            Path = string.Empty,
            Type = ItemType.File,
            UseSystemIcon = true,
            CreatedDate = DateTime.UtcNow,
            LaunchProfile = new ItemLaunchProfile(),
            HealthState = ItemHealthState.Healthy,
            IsClipboardText = true,
            ClipboardContent = normalizedText
        };

        _iconService.QueueIconRefresh(item);
        var allItemsIndex = AllItems.Count;
        var collectionInsertIndex = targetCollection?.Model.ItemIds.Count ?? -1;

        var addAction = new DelegateUndoableAction(
            "Add Clipboard Text",
            () =>
            {
                if (!AllItems.Any(existing => existing.Id == item.Id))
                {
                    AllItems.Insert(Math.Min(allItemsIndex, AllItems.Count), item);
                }

                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    InsertItemIntoCollection(targetCollection, item, collectionInsertIndex);
                }
            },
            () =>
            {
                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    RemoveItemFromCollection(targetCollection, item);
                }

                AllItems.Remove(item);
            });

        ExecuteAction(addAction);
        ShowClipboardFeedback("Saved Text ✓");
        StatusText = $"Saved clipboard text to {(targetCollection?.Name ?? "collection")}.";
        return true;
    }

    private bool TryAddClipboardImageItem()
    {
        if (!Clipboard.ContainsImage())
        {
            return false;
        }

        var bitmapSource = Clipboard.GetImage();
        if (bitmapSource is null)
        {
            return false;
        }

        var asset = _clipboardAssetService.SaveClipboardImage(bitmapSource);
        if (asset is null)
        {
            return false;
        }

        var targetCollection = ResolveTargetCollection();
        var item = new ItemModel
        {
            Name = BuildClipboardImageTitle(asset.PixelWidth, asset.PixelHeight),
            Path = string.Empty,
            Type = ItemType.File,
            UseSystemIcon = false,
            CreatedDate = DateTime.UtcNow,
            LaunchProfile = new ItemLaunchProfile(),
            HealthState = ItemHealthState.Healthy,
            IsClipboardImage = true,
            ClipboardImageAssetPath = asset.AssetPath,
            ClipboardImagePixelWidth = asset.PixelWidth,
            ClipboardImagePixelHeight = asset.PixelHeight
        };

        _iconService.QueueIconRefresh(item);
        var allItemsIndex = AllItems.Count;
        var collectionInsertIndex = targetCollection?.Model.ItemIds.Count ?? -1;

        var addAction = new DelegateUndoableAction(
            "Add Clipboard Image",
            () =>
            {
                if (!AllItems.Any(existing => existing.Id == item.Id))
                {
                    AllItems.Insert(Math.Min(allItemsIndex, AllItems.Count), item);
                }

                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    InsertItemIntoCollection(targetCollection, item, collectionInsertIndex);
                }
            },
            () =>
            {
                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    RemoveItemFromCollection(targetCollection, item);
                }

                AllItems.Remove(item);
            });

        ExecuteAction(addAction);
        ShowClipboardFeedback("Saved Image ✓");
        StatusText = $"Saved clipboard image to {(targetCollection?.Name ?? "collection")}.";
        return true;
    }

    private static string BuildClipboardTextTitle(string text)
    {
        var firstLine = text
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "Clipboard Text";
        }

        var normalized = firstLine.Trim();
        return normalized.Length > 64 ? $"{normalized[..61]}..." : normalized;
    }

    private static string BuildClipboardImageTitle(int width, int height)
    {
        return width > 0 && height > 0
            ? $"Clipboard Image {width}x{height}"
            : "Clipboard Image";
    }

    private List<ClipboardReviewEntry> BuildClipboardReviewEntriesFromPaths(IEnumerable<string> rawCandidates, CollectionViewModel? requestedCollection)
    {
        var existingByKey = BuildExistingItemsByKey();
        var targetCollection = ResolveTargetCollection(requestedCollection);
        var targetKeys = BuildCollectionPathKeySet(targetCollection);

        var seenInClipboard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ClipboardReviewEntry>();

        foreach (var rawCandidate in rawCandidates)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(rawCandidate, out var normalizedPath, out var pathKey, out var validationStatus))
            {
                entries.Add(new ClipboardReviewEntry
                {
                    RawInput = rawCandidate,
                    Status = ClipboardReviewStatus.Invalid,
                    WillApply = false,
                    Reason = ValidationStatusToReason(validationStatus)
                });
                continue;
            }

            if (!seenInClipboard.Add(pathKey))
            {
                entries.Add(new ClipboardReviewEntry
                {
                    RawInput = rawCandidate,
                    Path = normalizedPath,
                    PathKey = pathKey,
                    Status = ClipboardReviewStatus.DuplicateInClipboard,
                    WillApply = false,
                    Reason = "Duplicate in clipboard payload."
                });
                continue;
            }

            if (existingByKey.ContainsKey(pathKey))
            {
                var alreadyInCollection = targetKeys.Contains(pathKey);
                entries.Add(new ClipboardReviewEntry
                {
                    RawInput = rawCandidate,
                    Path = normalizedPath,
                    PathKey = pathKey,
                    Status = ClipboardReviewStatus.ValidExisting,
                    WillApply = !alreadyInCollection,
                    Reason = alreadyInCollection
                        ? "Already linked to selected collection."
                        : "Already exists globally; will link."
                });
                continue;
            }

            entries.Add(new ClipboardReviewEntry
            {
                RawInput = rawCandidate,
                Path = normalizedPath,
                PathKey = pathKey,
                Status = ClipboardReviewStatus.ValidNew,
                WillApply = true,
                Reason = "Valid path."
            });
        }

        return entries;
    }

    private AddPathOutcome AddOrLinkItem(string? path, CollectionViewModel? requestedCollection = null, bool suppressRefreshAndSave = false)
    {
        if (!PathNormalizationHelper.TryNormalizeUserPath(path, out var normalizedPath, out var pathKey))
        {
            return AddPathOutcome.Invalid;
        }

        var existingItem = FindExistingItem(pathKey);
        var targetCollection = ResolveTargetCollection(requestedCollection);

        if (existingItem is not null)
        {
            if (RepairGeneratedFolderName(existingItem))
            {
                QueueSave();
            }

            if (targetCollection is null || targetCollection.IsSmart)
            {
                return AddPathOutcome.AlreadyInCollection;
            }

            if (targetCollection.Model.ItemIds.Contains(existingItem.Id))
            {
                return AddPathOutcome.AlreadyInCollection;
            }

            var insertIndex = targetCollection.Model.ItemIds.Count;
            var action = new DelegateUndoableAction(
                "Link Existing Item",
                () => InsertItemIntoCollection(targetCollection, existingItem, insertIndex),
                () => RemoveItemFromCollection(targetCollection, existingItem));

            ExecuteAction(action, refreshAndSave: !suppressRefreshAndSave);
            return AddPathOutcome.LinkedExisting;
        }

        var item = new ItemModel
        {
            Name = GetDefaultName(normalizedPath),
            Path = normalizedPath,
            Type = DetermineItemType(normalizedPath),
            UseSystemIcon = true,
            CreatedDate = DateTime.UtcNow,
            LaunchProfile = new ItemLaunchProfile(),
            HealthState = ItemHealthState.Healthy
        };

        _iconService.QueueIconRefresh(item);

        var allItemsIndex = AllItems.Count;
        var collectionInsertIndex = targetCollection?.Model.ItemIds.Count ?? -1;

        var addAction = new DelegateUndoableAction(
            "Add Item",
            () =>
            {
                if (!AllItems.Any(existing => existing.Id == item.Id))
                {
                    AllItems.Insert(Math.Min(allItemsIndex, AllItems.Count), item);
                }

                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    InsertItemIntoCollection(targetCollection, item, collectionInsertIndex);
                }
            },
            () =>
            {
                if (targetCollection is not null && !targetCollection.IsSmart)
                {
                    RemoveItemFromCollection(targetCollection, item);
                }

                AllItems.Remove(item);
            });

        ExecuteAction(addAction, refreshAndSave: !suppressRefreshAndSave);
        return AddPathOutcome.AddedNew;
    }

    private ItemModel? FindExistingItem(string pathKey)
    {
        return BuildExistingItemsByKey().GetValueOrDefault(pathKey);
    }

    private Dictionary<string, ItemModel> BuildExistingItemsByKey()
    {
        var map = new Dictionary<string, ItemModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in AllItems)
        {
            var key = PathNormalizationHelper.GetPathKey(item.Path);
            if (string.IsNullOrWhiteSpace(key) || map.ContainsKey(key))
            {
                continue;
            }

            map[key] = item;
        }

        return map;
    }

    private static HashSet<string> BuildCollectionPathKeySet(CollectionViewModel? collection)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (collection is null)
        {
            return keys;
        }

        foreach (var item in collection.Items)
        {
            var key = PathNormalizationHelper.GetPathKey(item.Path);
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private CollectionViewModel? ResolveTargetCollection(CollectionViewModel? requestedCollection = null)
    {
        var targetCollection = requestedCollection ?? SelectedCollection ?? Collections.FirstOrDefault();
        if (targetCollection is not null && targetCollection.IsSmart)
        {
            targetCollection = Collections.FirstOrDefault(collection => !collection.IsSmart);
        }

        return targetCollection;
    }

    private bool ConfirmGlobalDelete(IReadOnlyCollection<ItemModel> items)
    {
        if (items.Count == 0)
        {
            return false;
        }

        var distinctItems = items
            .Where(item => item is not null)
            .Distinct()
            .ToList();
        if (distinctItems.Count == 0)
        {
            return false;
        }

        var affectedCollections = Collections
            .Where(collection => distinctItems.Any(item => collection.Model.ItemIds.Contains(item.Id)))
            .ToList();
        var affectedLinks = affectedCollections.Sum(collection => distinctItems.Count(item => collection.Model.ItemIds.Contains(item.Id)));

        string message;
        if (distinctItems.Count == 1)
        {
            var item = distinctItems[0];
            message = affectedCollections.Count > 1
                ? $"Delete '{item.Name}' globally?{Environment.NewLine}{Environment.NewLine}It will be removed from {affectedCollections.Count} collections ({affectedLinks} links total)."
                : $"Delete '{item.Name}' globally?{Environment.NewLine}{Environment.NewLine}It will be removed from the app and all collection links.";
        }
        else
        {
            message = affectedCollections.Count > 1
                ? $"Delete {distinctItems.Count} items globally?{Environment.NewLine}{Environment.NewLine}They will be removed from {affectedCollections.Count} collections ({affectedLinks} links total)."
                : $"Delete {distinctItems.Count} items globally?{Environment.NewLine}{Environment.NewLine}They will be removed from the app and all collection links.";
        }

        return MessageBox.Show(
            message,
            "Delete Globally",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private IUndoableAction? CreateRemoveItemFromCollectionAction(CollectionViewModel collection, ItemModel item)
    {
        if (collection.IsSmart)
        {
            return null;
        }

        var sourceIndex = GetEntryIndex(collection, item);
        if (sourceIndex < 0)
        {
            return null;
        }

        return new DelegateUndoableAction(
            "Remove Item From Collection",
            () => RemoveItemFromCollection(collection, item),
            () => InsertItemIntoCollection(collection, item, sourceIndex));
    }

    private IUndoableAction CreateDeleteItemGloballyAction(ItemModel item)
    {
        var allItemsIndex = -1;
        var collectionLinks = new List<CollectionLinkSnapshot>();

        return new DelegateUndoableAction(
            "Delete Item Globally",
            () =>
            {
                // Capture positions at apply time so reverting a bulk delete (which
                // reverts actions in reverse order) restores each item to its slot.
                allItemsIndex = AllItems.IndexOf(item);
                collectionLinks = Collections
                    .Where(collection => collection.Model.ItemIds.Contains(item.Id))
                    .Select(collection => new CollectionLinkSnapshot(collection, GetEntryIndex(collection, item)))
                    .ToList();

                foreach (var link in collectionLinks)
                {
                    RemoveItemFromCollection(link.Collection, item);
                }

                AllItems.Remove(item);
            },
            () =>
            {
                if (!AllItems.Contains(item))
                {
                    var insertIndex = allItemsIndex < 0 ? AllItems.Count : allItemsIndex;
                    AllItems.Insert(Math.Clamp(insertIndex, 0, AllItems.Count), item);
                }

                foreach (var link in collectionLinks)
                {
                    InsertItemIntoCollection(link.Collection, item, link.Index);
                }
            });
    }

    private void MoveItemInternal(CollectionViewModel collection, ItemModel item, int fromIndex, int toIndex)
    {
        if (collection.IsSmart)
        {
            return;
        }

        var currentIndex = GetEntryIndex(collection, item);
        if (currentIndex < 0)
        {
            return;
        }

        toIndex = Math.Clamp(toIndex, 0, Math.Max(0, collection.Model.Entries.Count - 1));
        if (currentIndex == toIndex)
        {
            return;
        }

        collection.Model.Entries.Move(currentIndex, toIndex);
        SyncCollectionItemIds(collection.Model);
        collection.ResetEntries(BuildCollectionEntries(collection.Model));
    }

    private void MoveCollectionEntryInternal(CollectionViewModel collection, int fromIndex, int toIndex)
    {
        if (collection.IsSmart)
        {
            return;
        }

        if (fromIndex < 0 || fromIndex >= collection.Model.Entries.Count)
        {
            return;
        }

        toIndex = Math.Clamp(toIndex, 0, Math.Max(0, collection.Model.Entries.Count - 1));
        if (fromIndex == toIndex)
        {
            return;
        }

        collection.Model.Entries.Move(fromIndex, toIndex);
        SyncCollectionItemIds(collection.Model);
        collection.ResetEntries(BuildCollectionEntries(collection.Model));
    }

    private void MoveCollectionInternal(CollectionViewModel collection, int fromIndex, int toIndex)
    {
        var currentIndex = Collections.IndexOf(collection);
        if (currentIndex < 0)
        {
            return;
        }

        toIndex = Math.Clamp(toIndex, 0, Math.Max(0, Collections.Count - 1));
        if (currentIndex == toIndex)
        {
            return;
        }

        Collections.Move(currentIndex, toIndex);
        AppData.Collections.Move(currentIndex, toIndex);
    }

    private static int GetEntryIndex(CollectionViewModel collection, ItemModel item)
    {
        return collection.Model.Entries
            .Select((entry, index) => new { entry, index })
            .Where(tuple => tuple.entry.Kind == CollectionEntryKind.Item && tuple.entry.ItemId == item.Id)
            .Select(tuple => tuple.index)
            .DefaultIfEmpty(-1)
            .First();
    }

    private static void SyncCollectionItemIds(CollectionModel model)
    {
        model.Entries ??= new ObservableCollection<CollectionEntryModel>();
        model.ItemIds ??= new ObservableCollection<Guid>();

        var itemIds = model.Entries
            .Where(entry => entry.Kind == CollectionEntryKind.Item && entry.ItemId != Guid.Empty)
            .Select(entry => entry.ItemId)
            .Distinct()
            .ToList();

        model.ItemIds.Clear();
        foreach (var id in itemIds)
        {
            model.ItemIds.Add(id);
        }
    }

    private static List<CollectionEntryModel> CloneCollectionEntries(CollectionModel model)
    {
        return model.Entries
            .Select(entry => new CollectionEntryModel
            {
                Id = entry.Id,
                Kind = entry.Kind,
                ItemId = entry.ItemId
            })
            .ToList();
    }

    private void InsertItemIntoCollection(CollectionViewModel collection, ItemModel item, int preferredIndex)
    {
        if (collection.IsSmart || collection.Model.ItemIds.Contains(item.Id))
        {
            return;
        }

        var index = Math.Clamp(preferredIndex, 0, collection.Model.Entries.Count);
        collection.Model.Entries.Insert(index, new CollectionEntryModel
        {
            Kind = CollectionEntryKind.Item,
            ItemId = item.Id
        });
        SyncCollectionItemIds(collection.Model);
        collection.ResetEntries(BuildCollectionEntries(collection.Model));
    }

    private void RemoveItemFromCollection(CollectionViewModel collection, ItemModel item)
    {
        if (collection.IsSmart)
        {
            return;
        }

        var entryIndex = GetEntryIndex(collection, item);
        if (entryIndex >= 0)
        {
            collection.Model.Entries.RemoveAt(entryIndex);
        }
        SyncCollectionItemIds(collection.Model);
        collection.ResetEntries(BuildCollectionEntries(collection.Model));
    }

    private static string ValidationStatusToReason(PathValidationStatus status)
    {
        return status switch
        {
            PathValidationStatus.Empty => "Invalid format.",
            PathValidationStatus.NotRooted => "Path is not rooted.",
            PathValidationStatus.InvalidFormat => "Invalid format.",
            _ => "Invalid path."
        };
    }

    private void ShowClipboardFeedback(string text)
    {
        ClipboardButtonText = text;
        _clipboardFeedbackTimer.Stop();
        _clipboardFeedbackTimer.Start();
    }

    private bool TryBuildItemReferenceCopy(ItemModel? item, out string textToCopy, out string statusText)
    {
        textToCopy = string.Empty;
        statusText = string.Empty;

        if (item is null)
        {
            return false;
        }

        textToCopy = item.IsClipboardText
            ? item.ClipboardContent
            : item.IsActionItem
                ? item.ActionContent
            : item.IsMtab
                ? string.Join(Environment.NewLine, GetMtabFolderPaths(item))
                : item.Path;

        if (string.IsNullOrWhiteSpace(textToCopy))
        {
            textToCopy = string.Empty;
            return false;
        }

        statusText = item.IsClipboardText
            ? $"Copied text item: {item.Name}"
            : item.IsActionItem
                ? $"Copied action content: {item.Name}"
            : item.IsMtab
                ? $"Copied Mtab paths: {item.Name}"
                : $"Copied path: {item.Name}";

        return true;
    }

    private void AppendClipboardHistory(
        (int addedNew, int linkedExisting, int skippedInvalid, int skippedDuplicate) summary,
        CollectionViewModel? targetCollection,
        IReadOnlyList<ClipboardReviewEntry> entries)
    {
        var addedPaths = entries
            .Where(entry => entry.WillApply)
            .Select(entry => entry.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (addedPaths.Count == 0)
        {
            return;
        }

        var historyEntry = new ClipboardImportHistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            CollectionId = targetCollection?.Id.ToString() ?? string.Empty,
            CollectionName = targetCollection?.Name ?? "(No Collection)",
            Summary = $"Added {summary.addedNew}, linked {summary.linkedExisting}, skipped {summary.skippedInvalid + summary.skippedDuplicate}"
        };

        foreach (var path in addedPaths)
        {
            historyEntry.Paths.Add(path);
        }

        AppData.ClipboardImportHistory.Insert(0, historyEntry);
        while (AppData.ClipboardImportHistory.Count > 20)
        {
            AppData.ClipboardImportHistory.RemoveAt(AppData.ClipboardImportHistory.Count - 1);
        }

        QueueSave();
    }

    private IReadOnlyList<CommandPaletteEntry> BuildCommandPaletteEntries()
    {
        return new List<CommandPaletteEntry>
        {
            new() { Name = "Add File", Description = "Open file picker and add item", Execute = () => AddFileCommand.Execute(null) },
            new() { Name = "Add Folder", Description = "Open folder picker and add item", Execute = () => AddFolderCommand.Execute(null) },
            new() { Name = "Open Clipboard Inbox", Description = "Review clipboard paths", Execute = OpenClipboardReviewPane },
            new() { Name = "New Action", Description = "Create an internal command, batch, or PowerShell item", Execute = () => NewActionCommand.Execute(null) },
            new() { Name = "Add Separator", Description = "Insert a visual divider in the current collection", Execute = () => AddSeparatorCommand.Execute(null) },
            new() { Name = "New Mtab", Description = "Create explorer multi-tab group", Execute = () => NewMtabCommand.Execute(null) },
            new() { Name = "Open Settings", Description = "Toggle settings drawer", Execute = () => ToggleSettingsCommand.Execute(null) },
            new() { Name = "Open Diagnostics", Description = "Open diagnostics page", Execute = ToggleDiagnostics },
            new() { Name = "Open Duplicate Manager", Description = "Manage duplicate paths", Execute = OpenDuplicateManagerPane },
            new() { Name = "Toggle View", Description = "Switch between grid and list", Execute = () => ToggleViewModeCommand.Execute(null) },
            new() { Name = "Next Collection", Description = "Switch active collection", Execute = () => ToggleCollectionCommand.Execute(null) },
            new() { Name = "Undo", Description = "Undo last action", Execute = () => UndoCommand.Execute(null) },
            new() { Name = "Redo", Description = "Redo last action", Execute = () => RedoCommand.Execute(null) },
            new() { Name = "Open All", Description = "Open all items in collection", Execute = () => OpenAllCommand.Execute(SelectedCollection) }
        };
    }

    private void ExecuteAction(IUndoableAction action, bool refreshAndSave = true)
    {
        _historyService.Execute(action);
        if (refreshAndSave)
        {
            RefreshSmartCollections();
            QueueSave();
        }
    }

    private static void MergeDuplicateIntoSurvivor(ItemModel survivor, ItemModel duplicate)
    {
        if (duplicate.IsFavorite)
        {
            survivor.IsFavorite = true;
        }

        if (duplicate.LastOpenedDate > survivor.LastOpenedDate)
        {
            survivor.LastOpenedDate = duplicate.LastOpenedDate;
        }

        if (string.IsNullOrWhiteSpace(survivor.IconPath) && !string.IsNullOrWhiteSpace(duplicate.IconPath))
        {
            survivor.IconPath = duplicate.IconPath;
            survivor.UseSystemIcon = duplicate.UseSystemIcon;
        }

        foreach (var tag in duplicate.Tags)
        {
            if (!survivor.Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                survivor.Tags.Add(tag);
            }
        }
    }

    private void ReplaceItemReferences(ItemModel duplicate, ItemModel survivor)
    {
        foreach (var collection in Collections)
        {
            var seenItemIds = new HashSet<Guid>();
            var rebuiltEntries = new List<CollectionEntryModel>();
            foreach (var entry in collection.Model.Entries)
            {
                if (entry.Kind == CollectionEntryKind.Separator)
                {
                    rebuiltEntries.Add(entry);
                    continue;
                }

                var mappedId = entry.ItemId == duplicate.Id ? survivor.Id : entry.ItemId;
                if (!seenItemIds.Add(mappedId))
                {
                    continue;
                }

                rebuiltEntries.Add(new CollectionEntryModel
                {
                    Id = entry.Id,
                    Kind = CollectionEntryKind.Item,
                    ItemId = mappedId
                });
            }

            collection.Model.Entries = new ObservableCollection<CollectionEntryModel>(rebuiltEntries);
            SyncCollectionItemIds(collection.Model);
        }

        foreach (var mtab in AllItems.Where(item => item.IsMtab))
        {
            var remapped = mtab.MtabMemberIds
                .Select(memberId => memberId == duplicate.Id ? survivor.Id : memberId)
                .Where(memberId => memberId != mtab.Id)
                .Distinct()
                .ToList();

            if (!remapped.SequenceEqual(mtab.MtabMemberIds))
            {
                mtab.MtabMemberIds = new ObservableCollection<Guid>(remapped);
            }
        }

        RefreshSmartCollections();
    }

    private void UpdateStatusCounts()
    {
        var collection = SelectedCollection;
        if (collection is null)
        {
            VisibleItemCount = 0;
            OfflineItemCount = 0;
            FavoriteItemCount = 0;
        }
        else
        {
            var visibleItems = collection.FilteredEntries
                .Cast<CollectionEntryViewModel>()
                .Select(entry => entry.Item)
                .Where(item => item is not null)
                .Cast<ItemModel>()
                .ToList();
            VisibleItemCount = visibleItems.Count;
            OfflineItemCount = visibleItems.Count(item => item.HealthState != ItemHealthState.Healthy);
            FavoriteItemCount = visibleItems.Count(item => item.IsFavorite);
        }

        OnPropertyChanged(nameof(VisibleItemCount));
        OnPropertyChanged(nameof(OfflineItemCount));
        OnPropertyChanged(nameof(FavoriteItemCount));
    }

    private async Task ExportDataAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export MyList Data",
            Filter = "MyList Package (*.zip)|*.zip",
            FileName = "MyList_export.zip"
        };

        if (dialog.ShowDialog() == true)
        {
            await _storageService.ExportCollectionsAsync(AppData, dialog.FileName);
            StatusText = $"Exported schema v{StorageService.CurrentSchemaVersion} package.";
        }
    }

    private async Task ImportDataAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import MyList Data",
            Filter = "MyList Package (*.zip)|*.zip|Legacy MyList Export (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var imported = await _storageService.ImportCollectionsAsync(dialog.FileName);
        if (imported is null)
        {
            var message = string.IsNullOrWhiteSpace(_storageService.LastImportError)
                ? "Import failed."
                : _storageService.LastImportError;
            MessageBox.Show(message, "Import", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ApplyImportedData(imported);
        StatusText = $"Imported schema v{imported.SchemaVersion}.";
    }

    private async Task RestoreBackupAsync()
    {
        var restored = await _storageService.RestoreLatestBackupAsync();
        if (restored is null)
        {
            MessageBox.Show("No backups found.", "Restore", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyImportedData(restored);
        StatusText = "Backup restored.";
        await Task.CompletedTask;
    }

    private void ApplyImportedData(AppData imported)
    {
        AppData.SchemaVersion = imported.SchemaVersion;
        CopySettings(imported.AppSettings, AppData.AppSettings);

        AppData.Collections.Clear();
        AppData.Items.Clear();
        AppData.ClipboardImportHistory.Clear();
        AppData.MigrationLogs.Clear();

        foreach (var item in imported.Items)
        {
            AppData.Items.Add(item);
        }

        foreach (var collection in imported.Collections)
        {
            AppData.Collections.Add(collection);
        }

        foreach (var historyEntry in imported.ClipboardImportHistory)
        {
            AppData.ClipboardImportHistory.Add(historyEntry);
        }

        foreach (var migrationLog in imported.MigrationLogs)
        {
            AppData.MigrationLogs.Add(migrationLog);
        }

        _clipboardAssetService.CleanupUnusedAssets(AppData.Items);

        Collections.Clear();
        foreach (var collection in AppData.Collections)
        {
            Collections.Add(new CollectionViewModel(collection, BuildCollectionEntries(collection)));
        }

        SelectedCollection = Collections.FirstOrDefault();
        BulkTargetCollection = Collections.FirstOrDefault(collection => !collection.IsSmart);

        Settings.ReloadFromSettings();
        RefreshAllIcons();
        RefreshSmartCollections();
        ApplySearch();

        _historyService.Clear();
        QueueSave();
    }

    private enum AddPathOutcome
    {
        Invalid,
        AddedNew,
        LinkedExisting,
        AlreadyInCollection
    }

    private sealed record CollectionLinkSnapshot(CollectionViewModel Collection, int Index);

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        target.FollowSystemTheme = source.FollowSystemTheme;
        target.Theme = source.Theme;
        target.ViewMode = source.ViewMode;
        target.LayoutMode = source.LayoutMode;
        target.CollectionsLayout = source.CollectionsLayout;
        target.AlwaysOnTop = source.AlwaysOnTop;
        target.AutoHide = source.AutoHide;
        target.MinimizeToTray = source.MinimizeToTray;
        target.StartWithWindows = source.StartWithWindows;
        target.GlobalHotkey = source.GlobalHotkey;
        target.UiDensity = source.UiDensity;
        target.EnableDebugMode = source.EnableDebugMode;
        target.WindowPlacement = source.WindowPlacement;
        target.LastBackupDate = source.LastBackupDate;
    }
}
