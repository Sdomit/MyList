using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;
using MyList.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using Screen = System.Windows.Forms.Screen;

namespace MyList.Views;

public partial class MainWindow : Window
{
    private const int AutoHideMarginPixels = 24;
    private Point _dragStart;
    private bool _autoHidden;
    private bool _isDragging;
    private bool _isCollectionEntryDragActive;
    private bool _isClampingWindowBounds;
    private Point _collectionDragStart;
    private readonly DispatcherTimer _pathCopyToastTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private System.Windows.Input.Cursor? _deleteCursor;

    public MainWindow()
    {
        InitializeComponent();
        _pathCopyToastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _pathCopyToastTimer.Tick += (_, _) =>
        {
            _pathCopyToastTimer.Stop();
            PathCopyToast.IsOpen = false;
        };
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _autoHideTimer.Tick += (_, _) => EvaluateAutoHide();
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
        Activated += (_, _) => _autoHidden = false;
        LocationChanged += OnWindowBoundsChanged;
        SizeChanged += OnWindowBoundsChanged;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        PreviewTextInput += OnWindowPreviewTextInput;
        GiveFeedback += OnWindowGiveFeedback;
        QueryContinueDrag += OnWindowQueryContinueDrag;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ApplyWindowPlacement(ViewModel.Settings.AppSettings.WindowPlacement);
        ApplyLayoutMode(ViewModel.Settings.LayoutMode);
        EnsureWindowVisible();
        RestoreAndActivate();
        ApplyMicaBackdrop();
        ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void ApplyMicaBackdrop()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (MicaHelper.TryApply(this, ViewModel.Settings.IsDarkMode))
        {
            // System backdrop owns the composition; clear the solid fallback brush so
            // Mica shows through. WPF still composites theme tokens above on child surfaces.
            Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    private void EnsureWindowVisible()
    {
        var workArea = GetCurrentScreenWorkArea();
        if (double.IsNaN(Width) || Width < MinWidth)
        {
            Width = MinWidth;
        }
        if (double.IsNaN(Height) || Height < MinHeight)
        {
            Height = MinHeight;
        }
        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
            return;
        }

        var rect = new Rect(Left, Top, Width, Height);
        var isVisibleOnAnyScreen = Screen.AllScreens.Any(screen =>
        {
            var area = screen.WorkingArea;
            var screenRect = new Rect(area.Left, area.Top, area.Width, area.Height);
            return rect.IntersectsWith(screenRect);
        });

        if (!isVisibleOnAnyScreen)
        {
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        ClampWindowToWorkArea(workArea);
    }

    public void RestoreAndActivate()
    {
        if (_autoHidden)
        {
            _autoHidden = false;
        }

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        EnsureWindowVisible();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SW_RESTORE);
            var makeTopmost = ViewModel?.Settings.AlwaysOnTop == true;
            if (makeTopmost)
            {
                SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SetWindowPosFlags.NoMove | SetWindowPosFlags.NoSize | SetWindowPosFlags.ShowWindow);
            }
            else
            {
                SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SetWindowPosFlags.NoMove | SetWindowPosFlags.NoSize | SetWindowPosFlags.ShowWindow);
                SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SetWindowPosFlags.NoMove | SetWindowPosFlags.NoSize | SetWindowPosFlags.ShowWindow);
            }

            SetForegroundWindow(handle);
        }

        Activate();
        Focus();
    }

    public void HideToTray()
    {
        _autoHidden = false;

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Hide();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(SettingsViewModel.LayoutMode))
        {
            ApplyLayoutMode(ViewModel.Settings.LayoutMode);
        }
        else if (e.PropertyName == nameof(SettingsViewModel.AutoHide) && !ViewModel.Settings.AutoHide)
        {
            _autoHideTimer.Stop();
            _autoHidden = false;
        }
        else if (e.PropertyName == nameof(SettingsViewModel.IsDarkMode))
        {
            // Re-apply Mica's immersive-dark-mode flag so the title bar tracks the theme.
            ApplyMicaBackdrop();
        }
    }

    private void ApplyWindowPlacement(WindowPlacement placement)
    {
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        if (placement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void ApplyLayoutMode(LayoutMode mode)
    {
        var workArea = SystemParameters.WorkArea;
        switch (mode)
        {
            case LayoutMode.Docked:
                MinWidth = 320;
                MinHeight = 420;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.SingleBorderWindow;
                Width = 420;
                Height = workArea.Height;
                Top = workArea.Top;
                var edge = ViewModel?.Settings.AppSettings.WindowPlacement.DockEdge ?? WindowDockEdge.Left;
                Left = edge == WindowDockEdge.Left ? workArea.Left : workArea.Right - Width;
                break;
            case LayoutMode.Floating:
                MinWidth = 320;
                MinHeight = 420;
                ResizeMode = ResizeMode.NoResize;
                // ToolWindow style can become hard to recover (no taskbar button / Alt-Tab entry),
                // especially when combined with auto-hide behaviors. Keep a normal window style.
                WindowStyle = WindowStyle.SingleBorderWindow;
                if (Width < MinWidth) Width = MinWidth;
                if (Height < MinHeight) Height = MinHeight;
                break;
            default:
                MinWidth = 990;
                MinHeight = 770;
                ResizeMode = ResizeMode.CanResize;
                WindowStyle = WindowStyle.SingleBorderWindow;
                if (Width < MinWidth) Width = MinWidth;
                if (Height < MinHeight) Height = MinHeight;
                break;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var shouldHideToTray = ViewModel.Settings.MinimizeToTray &&
            RuntimeStatus.TrayInitialized &&
            (System.Windows.Application.Current is not App app || !app.IsShutdownRequested);

        if (shouldHideToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        var placement = ViewModel.Settings.AppSettings.WindowPlacement;
        placement.Left = Left;
        placement.Top = Top;
        placement.Width = Width;
        placement.Height = Height;
        placement.IsMaximized = WindowState == WindowState.Maximized;
        ViewModel.QueueSave();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (ViewModel?.Settings.MinimizeToTray == true &&
            RuntimeStatus.TrayInitialized &&
            WindowState == WindowState.Minimized)
        {
            HideToTray();
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            EnsureWindowVisible();
        }
    }

    private void OnWindowBoundsChanged(object? sender, EventArgs e)
    {
        if (_isClampingWindowBounds || WindowState != WindowState.Normal || !IsVisible)
        {
            return;
        }

        ClampWindowToWorkArea(GetCurrentScreenWorkArea());
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel?.Settings.AutoHide != true || _autoHidden || !IsVisible)
        {
            return;
        }

        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();

        _autoHidden = false;
    }

    private void EvaluateAutoHide()
    {
        _autoHideTimer.Stop();

        if (ViewModel?.Settings.AutoHide != true || _autoHidden || !IsVisible)
        {
            return;
        }

        if (IsCursorInsideAutoHideBounds())
        {
            return;
        }

        AutoHideWindow();
        _autoHidden = true;
    }

    private void AutoHideWindow()
    {
        if (!IsVisible)
        {
            return;
        }

        if (ViewModel?.Settings.MinimizeToTray == true && RuntimeStatus.TrayInitialized)
        {
            HideToTray();
            return;
        }

        // Minimize instead of pushing to HWND_BOTTOM so the window is always recoverable via taskbar or hotkey.
        if (WindowState != WindowState.Minimized)
        {
            WindowState = WindowState.Minimized;
        }
    }

    public void SendBehindOtherWindows()
    {
        if (!IsVisible)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SetWindowPosFlags.NoMove | SetWindowPosFlags.NoSize | SetWindowPosFlags.NoActivate);
        SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SetWindowPosFlags.NoMove | SetWindowPosFlags.NoSize | SetWindowPosFlags.NoActivate);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            ViewModel.ShowCommandPalette();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            ViewModel.OpenInlineAddCommand.Execute(null);
            Dispatcher.BeginInvoke(() =>
            {
                InlineAddBox.Focus();
                Keyboard.Focus(InlineAddBox);
            });
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
        {
            ViewModel.ToggleSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.V)
        {
            ViewModel.OpenClipboardReviewPaneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
        {
            ViewModel.OpenDuplicateManagerPaneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.M)
        {
            ViewModel.CaptureExplorerWindowsAsMtabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
        {
            ViewModel.ToggleThemeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.M)
        {
            RestoreAndActivate();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Space)
        {
            RestoreAndActivate();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && TryGetItemShortcutIndex(e.Key, out var shortcutIndex))
        {
            OpenItemByShortcutIndex(shortcutIndex);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            if (ViewModel.UndoCommand.CanExecute(null))
            {
                ViewModel.UndoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
        {
            if (ViewModel.RedoCommand.CanExecute(null))
            {
                ViewModel.RedoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && ViewModel.IsSettingsOpen)
        {
            if (ViewModel.CloseSettingsCommand.CanExecute(null))
            {
                ViewModel.CloseSettingsCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && (ViewModel.IsClipboardReviewPaneOpen || ViewModel.IsDuplicateManagerPaneOpen))
        {
            ViewModel.CloseToolPaneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && ViewModel.IsAddFormOpen)
        {
            ViewModel.CancelInlineAddCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            if (ViewModel.OpenClipboardReviewPaneCommand.CanExecute(null))
            {
                ViewModel.OpenClipboardReviewPaneCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            if (ViewModel.BeginQuickEditCommand.CanExecute(null))
            {
                ViewModel.BeginQuickEditCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && ViewModel.SelectedItem is not null)
        {
            ViewModel.OpenItemCommand.Execute(ViewModel.SelectedItem);
            e.Handled = true;
        }
    }

    private void OnInlineAddOpenClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.OpenInlineAddCommand.Execute(null);
        Dispatcher.BeginInvoke(() =>
        {
            InlineAddBox.Focus();
            Keyboard.Focus(InlineAddBox);
        });
    }

    private void OnSearchClearClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void OnInlineAddKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && ViewModel.SaveInlineAddCommand.CanExecute(null))
        {
            ViewModel.SaveInlineAddCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.CancelInlineAddCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OpenItemByShortcutIndex(int index)
    {
        if (ViewModel?.SelectedCollection is null)
        {
            return;
        }

        var item = ViewModel.SelectedCollection.FilteredEntries
            .Cast<object>()
            .OfType<CollectionEntryViewModel>()
            .Where(entry => !entry.IsSeparator && entry.Item is not null)
            .Select(entry => entry.Item!)
            .ElementAtOrDefault(index);

        if (item is null)
        {
            return;
        }

        ViewModel.SetSelectedItems(new[] { item });
        ViewModel.OpenItemCommand.Execute(item);
    }

    private static bool TryGetItemShortcutIndex(Key key, out int index)
    {
        index = key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };

        return index >= 0;
    }

    private void OnWindowPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if (string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl))
        {
            return;
        }

        SearchBox.Focus();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void OnMoreActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(CollectionEntryDragData)) ||
            e.Data.GetDataPresent(typeof(CollectionEntryViewModel)) ||
            e.Data.GetDataPresent(typeof(CollectionItemDragData)) ||
            e.Data.GetDataPresent(typeof(ItemModel)) ||
            e.Data.GetDataPresent(typeof(CollectionViewModel)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                foreach (var path in paths)
                {
                    ViewModel.AddItemFromPath(path);
                }
            }

            e.Handled = true;
        }
    }

    private void OnItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryHandleShortcutPathCopy(e))
        {
            e.Handled = true;
            return;
        }

        // Click on empty space (not on a row) clears the selection, collapsing the details pane.
        if (sender is ListBox listBox &&
            FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is null)
        {
            listBox.UnselectAll();
        }

        _dragStart = e.GetPosition(null);
    }

    private void OnItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not ListBox listBox || listBox.SelectedItem is not CollectionEntryViewModel entry)
        {
            return;
        }

        _isDragging = true;
        var data = new System.Windows.DataObject();
        var sourceCollection = ResolveSourceCollection(listBox, entry);
        data.SetData(typeof(CollectionEntryViewModel), entry);
        data.SetData(typeof(CollectionEntryDragData), new CollectionEntryDragData(entry, sourceCollection));
        if (entry.Item is not null)
        {
            data.SetData(typeof(ItemModel), entry.Item);
            data.SetData(typeof(CollectionItemDragData), new CollectionItemDragData(entry.Item, sourceCollection));
        }
        _isCollectionEntryDragActive = true;
        ShowDragDeleteHint(false);

        DragDropEffects dragResult;
        try
        {
            dragResult = DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            ClearCollectionTabDragIndicators();
            HideDragDeleteHint();
            _isCollectionEntryDragActive = false;
            _isDragging = false;
        }

        if (dragResult == DragDropEffects.None && ViewModel is not null && IsCursorOutsideWindow())
        {
            ViewModel.DeleteDraggedEntry(entry, sourceCollection);
        }
    }

    private void OnItemDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || sender is not ListBox listBox)
        {
            return;
        }

        if (e.Data.GetDataPresent(typeof(CollectionEntryViewModel)))
        {
            var droppedEntry = (CollectionEntryViewModel)e.Data.GetData(typeof(CollectionEntryViewModel))!;
            var dragData = e.Data.GetDataPresent(typeof(CollectionEntryDragData))
                ? (CollectionEntryDragData?)e.Data.GetData(typeof(CollectionEntryDragData))
                : null;
            var targetEntry = GetEntryAt(listBox, e.GetPosition(listBox));
            if (droppedEntry.Item is not null && targetEntry?.Item is not null &&
                ViewModel.TryHandleInternalItemDropAction(droppedEntry.Item, targetEntry.Item))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            var newIndex = targetEntry is null ? listBox.Items.Count - 1 : listBox.Items.IndexOf(targetEntry);
            var targetCollection = listBox.DataContext as CollectionViewModel ?? ViewModel.SelectedCollection;
            var sourceCollection = dragData?.SourceCollection;

            if (targetCollection is not null && sourceCollection is not null && ReferenceEquals(targetCollection, sourceCollection))
            {
                ViewModel.MoveCollectionEntry(targetCollection, droppedEntry, newIndex);
            }
            else if (droppedEntry.Item is not null)
            {
                if (targetCollection is not null)
                {
                    ViewModel.MoveItem(targetCollection, droppedEntry.Item, newIndex);
                }
                else if (ViewModel.SelectedCollection is not null)
                {
                    ViewModel.MoveItem(ViewModel.SelectedCollection, droppedEntry.Item, newIndex);
                }
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(ItemModel)))
        {
            var droppedItem = (ItemModel)e.Data.GetData(typeof(ItemModel))!;
            var targetItem = GetItemAt(listBox, e.GetPosition(listBox));
            if (targetItem is not null && ViewModel.TryHandleInternalItemDropAction(droppedItem, targetItem))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            var newIndex = targetItem is null ? listBox.Items.Count - 1 : listBox.Items.IndexOf(targetItem);

            if (listBox.DataContext is CollectionViewModel collection)
            {
                ViewModel.MoveItem(collection, droppedItem, newIndex);
            }
            else if (ViewModel.SelectedCollection is not null)
            {
                ViewModel.MoveItem(ViewModel.SelectedCollection, droppedItem, newIndex);
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var targetItem = GetItemAt(listBox, e.GetPosition(listBox));
                if (targetItem is not null && ViewModel.TryHandleExternalFolderDropAction(paths, targetItem))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }

                foreach (var path in paths)
                {
                    ViewModel.AddItemFromPath(path);
                }
            }

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }
    }

    private void OnCollectionPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _collectionDragStart = e.GetPosition(null);
    }

    private void OnCollectionPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _collectionDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _collectionDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not ListBox listBox || listBox.SelectedItem is not CollectionViewModel collection)
        {
            return;
        }

        var data = new System.Windows.DataObject();
        data.SetData(typeof(CollectionViewModel), collection);
        try
        {
            DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move);
        }
        finally
        {
            ClearCollectionTabDragIndicators();
        }
    }

    private void OnCollectionDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || sender is not ListBox listBox)
        {
            return;
        }

        if (e.Data.GetDataPresent(typeof(CollectionItemDragData)))
        {
            var dragData = (CollectionItemDragData)e.Data.GetData(typeof(CollectionItemDragData))!;
            var targetCollection = GetCollectionAt(listBox, e.GetPosition(listBox));
            var transferAction = CollectionTransferAction.Cancel;

            if (ViewModel.Settings.CollectionsLayout == CollectionsLayout.Tabs &&
                dragData.SourceCollection is not null &&
                targetCollection is not null &&
                ViewModel.CanTransferItemBetweenCollections(dragData.SourceCollection, targetCollection, dragData.Item))
            {
                transferAction = ShowCollectionTransferDialog(dragData.Item, dragData.SourceCollection, targetCollection);
                ViewModel.TryTransferItemBetweenCollections(dragData.SourceCollection, targetCollection, dragData.Item, transferAction);
            }

            ClearCollectionTabDragIndicators();
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(CollectionViewModel)))
        {
            var dropped = (CollectionViewModel)e.Data.GetData(typeof(CollectionViewModel))!;
            var newIndex = GetCollectionDropIndex(listBox, e.GetPosition(listBox), dropped, out _, out _);
            ViewModel.MoveCollection(dropped, newIndex);
            ClearCollectionTabDragIndicators();
            e.Handled = true;
        }
    }

    private void OnTabCollectionPreviewDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel is null || sender is not ListBox listBox)
        {
            return;
        }

        ClearCollectionTabDragIndicators();

        if (e.Data.GetDataPresent(typeof(CollectionItemDragData)))
        {
            var dragData = (CollectionItemDragData)e.Data.GetData(typeof(CollectionItemDragData))!;
            var targetCollection = GetCollectionAt(listBox, e.GetPosition(listBox));
            if (targetCollection is null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (ViewModel.CanTransferItemBetweenCollections(dragData.SourceCollection, targetCollection, dragData.Item))
            {
                targetCollection.IsTabDragTarget = true;
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                targetCollection.IsTabDropBlocked = true;
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(CollectionViewModel)))
        {
            var droppedCollection = (CollectionViewModel)e.Data.GetData(typeof(CollectionViewModel))!;
            GetCollectionDropIndex(listBox, e.GetPosition(listBox), droppedCollection, out var markerCollection, out var insertAfter);
            if (markerCollection is not null)
            {
                if (insertAfter)
                {
                    markerCollection.IsTabInsertionAfter = true;
                }
                else
                {
                    markerCollection.IsTabInsertionBefore = true;
                }

                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
    }

    private void OnTabCollectionDragLeave(object sender, DragEventArgs e)
    {
        ClearCollectionTabDragIndicators();
    }

    private void OnItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || ViewModel is null || !ViewModel.Settings.OpenItemsOnSingleClick)
        {
            return;
        }

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not CollectionEntryViewModel entry || entry.Item is not ItemModel item)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.SelectedItem is not CollectionEntryViewModel selectedEntry || selectedEntry.Item?.Id != item.Id)
        {
            listBox.SelectedItem = entry;
        }

        ViewModel.OpenItemCommand.Execute(item);
        e.Handled = true;
    }

    private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A single click has already launched the item when this option is on.
        if (_isDragging || ViewModel is null || ViewModel.Settings.OpenItemsOnSingleClick)
        {
            return;
        }

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not CollectionEntryViewModel entry || entry.Item is not ItemModel item)
        {
            return;
        }

        ViewModel.OpenItemCommand.Execute(item);
        e.Handled = true;
    }

    private void OnClearSelectionClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.SetSelectedItems(Array.Empty<ItemModel>());
    }

    private void OnItemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || sender is not ListBox listBox)
        {
            return;
        }

        ViewModel.SetSelectedItems(
            listBox.SelectedItems
                .Cast<object>()
                .OfType<CollectionEntryViewModel>()
                .Select(entry => entry.Item)
                .Where(item => item is not null)
                .Cast<ItemModel>());
    }

    private void OnWindowGiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
    {
        if (!_isCollectionEntryDragActive)
        {
            return;
        }

        var armed = IsCursorOutsideWindow();
        ShowDragDeleteHint(armed);

        var deleteCursor = armed ? GetDeleteCursor() : null;
        if (deleteCursor is not null)
        {
            Mouse.OverrideCursor = deleteCursor;
            Mouse.SetCursor(deleteCursor);
            e.UseDefaultCursors = false;
            e.Handled = true;
            return;
        }

        Mouse.OverrideCursor = null;
        e.UseDefaultCursors = true;
    }

    private void OnWindowQueryContinueDrag(object sender, System.Windows.QueryContinueDragEventArgs e)
    {
        if (!_isCollectionEntryDragActive)
        {
            return;
        }

        if (e.Action is System.Windows.DragAction.Cancel or System.Windows.DragAction.Drop)
        {
            HideDragDeleteHint();
        }
    }

    private static ItemModel? GetItemAt(ItemsControl listBox, Point position)
    {
        return GetEntryAt(listBox, position)?.Item;
    }

    private static CollectionEntryViewModel? GetEntryAt(ItemsControl listBox, Point position)
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element is not null && element is not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return (element as ListBoxItem)?.DataContext as CollectionEntryViewModel;
    }

    private static CollectionViewModel? GetCollectionAt(ItemsControl listBox, Point position)
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element is not null && element is not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }
        return (element as ListBoxItem)?.DataContext as CollectionViewModel;
    }

    private CollectionViewModel? ResolveSourceCollection(ListBox listBox, CollectionEntryViewModel entry)
    {
        if (listBox.DataContext is CollectionViewModel collection && collection.Entries.Contains(entry))
        {
            return collection;
        }

        if (entry.Item is not null && ViewModel?.SelectedCollection is not null && ViewModel.SelectedCollection.Items.Contains(entry.Item))
        {
            return ViewModel.SelectedCollection;
        }

        return entry.Item is null
            ? ViewModel?.Collections.FirstOrDefault(existing => existing.Entries.Contains(entry))
            : ViewModel?.Collections.FirstOrDefault(existing => existing.Items.Contains(entry.Item));
    }

    private bool TryHandleShortcutPathCopy(MouseButtonEventArgs e)
    {
        if (ViewModel is null || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return false;
        }

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is not CollectionEntryViewModel entry || entry.Item is not ItemModel item || !IsShortcutPathCopyCandidate(item))
        {
            return false;
        }

        if (!ViewModel.TryCopyItemReference(item))
        {
            return false;
        }

        ShowPathCopyToast(container);
        return true;
    }

    private void ShowPathCopyToast(ListBoxItem container)
    {
        PathCopyToast.IsOpen = false;
        PathCopyToast.PlacementTarget = container;
        PathCopyToast.Placement = PlacementMode.Top;
        PathCopyToast.HorizontalOffset = 0;
        PathCopyToast.VerticalOffset = -8;
        PathCopyToast.IsOpen = true;

        _pathCopyToastTimer.Stop();
        _pathCopyToastTimer.Start();
    }

    private static bool IsShortcutPathCopyCandidate(ItemModel item)
    {
        if (item.IsClipboardText || item.IsClipboardImage || item.IsMtab || item.IsActionItem || string.IsNullOrWhiteSpace(item.Path))
        {
            return false;
        }

        return item.Type is ItemType.Folder or ItemType.File or ItemType.App;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null && source is not T)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        return source as T;
    }

    private bool IsCursorOutsideWindow()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return false;
        }

        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        var windowBounds = new Rect(topLeft, bottomRight);
        return !windowBounds.Contains(new Point(cursorPosition.X, cursorPosition.Y));
    }

    private bool IsCursorInsideAutoHideBounds()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return false;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var windowRect))
        {
            var topLeft = PointToScreen(new Point(0, 0));
            var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
            var fallbackBounds = new Rect(topLeft, bottomRight);
            fallbackBounds.Inflate(AutoHideMarginPixels, AutoHideMarginPixels);
            return fallbackBounds.Contains(new Point(cursorPosition.X, cursorPosition.Y));
        }

        var fullBounds = new Rect(
            windowRect.Left - AutoHideMarginPixels,
            windowRect.Top - AutoHideMarginPixels,
            (windowRect.Right - windowRect.Left) + (AutoHideMarginPixels * 2),
            (windowRect.Bottom - windowRect.Top) + (AutoHideMarginPixels * 2));

        return fullBounds.Contains(new Point(cursorPosition.X, cursorPosition.Y));
    }

    private Rect GetCurrentScreenWorkArea()
    {
        System.Drawing.Rectangle bounds;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero && GetWindowRect(handle, out var windowRect))
        {
            bounds = System.Drawing.Rectangle.FromLTRB(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom);
        }
        else
        {
            var safeWidth = double.IsNaN(Width) ? MinWidth : Width;
            var safeHeight = double.IsNaN(Height) ? MinHeight : Height;
            bounds = new System.Drawing.Rectangle(
                (int)Math.Round(Left),
                (int)Math.Round(Top),
                (int)Math.Round(Math.Max(MinWidth, safeWidth)),
                (int)Math.Round(Math.Max(MinHeight, safeHeight)));
        }

        var area = Screen.FromRectangle(bounds).WorkingArea;
        return new Rect(area.Left, area.Top, area.Width, area.Height);
    }

    private void ClampWindowToWorkArea(Rect workArea)
    {
        if (_isClampingWindowBounds || WindowState != WindowState.Normal)
        {
            return;
        }

        var safeWidth = double.IsNaN(Width) ? MinWidth : Width;
        var safeHeight = double.IsNaN(Height) ? MinHeight : Height;
        var clampedWidth = Math.Min(Math.Max(safeWidth, MinWidth), workArea.Width);
        var clampedHeight = Math.Min(Math.Max(safeHeight, MinHeight), workArea.Height);
        var maxLeft = Math.Max(workArea.Left, workArea.Right - clampedWidth);
        var maxTop = Math.Max(workArea.Top, workArea.Bottom - clampedHeight);
        var clampedLeft = Math.Min(Math.Max(Left, workArea.Left), maxLeft);
        var clampedTop = Math.Min(Math.Max(Top, workArea.Top), maxTop);

        var requiresUpdate =
            !AreClose(Width, clampedWidth) ||
            !AreClose(Height, clampedHeight) ||
            !AreClose(Left, clampedLeft) ||
            !AreClose(Top, clampedTop);

        if (!requiresUpdate)
        {
            return;
        }

        _isClampingWindowBounds = true;
        try
        {
            Width = clampedWidth;
            Height = clampedHeight;
            Left = clampedLeft;
            Top = clampedTop;
        }
        finally
        {
            _isClampingWindowBounds = false;
        }
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.5;
    }

    private void ShowDragDeleteHint(bool armed)
    {
        DragDeleteHintHost.Visibility = Visibility.Visible;
        DragDeleteHintHost.Background = armed
            ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 110, 28, 28))
            : (System.Windows.Media.Brush)FindResource("SurfaceElevated");
        DragDeleteHintHost.BorderBrush = armed
            ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 220, 86, 86))
            : (System.Windows.Media.Brush)FindResource("BorderBrush");
        DragDeleteHintIcon.Text = armed ? "Delete" : "Drag";
        DragDeleteHintIcon.Foreground = armed
            ? System.Windows.Media.Brushes.White
            : (System.Windows.Media.Brush)FindResource("Accent");
        DragDeleteHintText.Text = armed
            ? "Release to delete"
            : "Drag outside window to delete";
        DragDeleteHintText.Foreground = armed
            ? System.Windows.Media.Brushes.White
            : (System.Windows.Media.Brush)FindResource("TextPrimary");
    }

    private void HideDragDeleteHint()
    {
        DragDeleteHintHost.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;
    }

    private System.Windows.Input.Cursor? GetDeleteCursor()
    {
        if (_deleteCursor is not null)
        {
            return _deleteCursor;
        }

        try
        {
            var cursorPath = Path.Combine(AppContext.BaseDirectory, "icons", "delete.cur");
            if (!File.Exists(cursorPath))
            {
                return null;
            }

            using var stream = File.OpenRead(cursorPath);
            _deleteCursor = new System.Windows.Input.Cursor(stream);
            return _deleteCursor;
        }
        catch
        {
            return null;
        }
    }

    private void ClearCollectionTabDragIndicators()
    {
        if (ViewModel is null)
        {
            return;
        }

        foreach (var collection in ViewModel.Collections)
        {
            collection.IsTabDragTarget = false;
            collection.IsTabDropBlocked = false;
            collection.IsTabInsertionBefore = false;
            collection.IsTabInsertionAfter = false;
        }
    }

    private static int GetCollectionDropIndex(
        ListBox listBox,
        Point position,
        CollectionViewModel droppedCollection,
        out CollectionViewModel? markerCollection,
        out bool insertAfter)
    {
        markerCollection = null;
        insertAfter = false;

        if (listBox.Items.Count == 0)
        {
            return 0;
        }

        var targetContainer = GetCollectionContainerAt(listBox, position);
        if (targetContainer?.DataContext is not CollectionViewModel targetCollection)
        {
            markerCollection = listBox.Items[listBox.Items.Count - 1] as CollectionViewModel;
            insertAfter = markerCollection is not null;
            return AdjustCollectionDropIndex(listBox.Items.Count, listBox, droppedCollection);
        }

        markerCollection = targetCollection;
        var targetOrigin = targetContainer.TranslatePoint(new Point(0, 0), listBox);
        insertAfter = position.X > targetOrigin.X + (targetContainer.ActualWidth / 2);

        var insertIndex = listBox.Items.IndexOf(targetCollection) + (insertAfter ? 1 : 0);
        return AdjustCollectionDropIndex(insertIndex, listBox, droppedCollection);
    }

    private static int AdjustCollectionDropIndex(int insertIndex, ListBox listBox, CollectionViewModel droppedCollection)
    {
        var droppedIndex = listBox.Items.IndexOf(droppedCollection);
        if (droppedIndex >= 0 && insertIndex > droppedIndex)
        {
            insertIndex--;
        }

        return Math.Clamp(insertIndex, 0, Math.Max(0, listBox.Items.Count - 1));
    }

    private static ListBoxItem? GetCollectionContainerAt(ItemsControl listBox, Point position)
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element is not null && element is not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
    }

    private CollectionTransferAction ShowCollectionTransferDialog(
        ItemModel item,
        CollectionViewModel sourceCollection,
        CollectionViewModel targetCollection)
    {
        var dialog = new CollectionTransferWindow(
            $"Transfer '{item.Name}' from '{sourceCollection.Name}' to '{targetCollection.Name}'.")
        {
            Owner = this
        };

        return dialog.ShowDialog() == true
            ? dialog.SelectedAction
            : CollectionTransferAction.Cancel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoSize = 0x0001,
        NoMove = 0x0002,
        NoActivate = 0x0010,
        ShowWindow = 0x0040
    }

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags uFlags);
}
