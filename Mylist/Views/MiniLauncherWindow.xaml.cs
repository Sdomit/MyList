using System;
using System.Windows;
using MyList.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace MyList.Views;

public partial class MiniLauncherWindow : Window
{
    private readonly MiniLauncherViewModel _viewModel;

    public MiniLauncherWindow(MiniLauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void Summon()
    {
        _viewModel.Refresh();
        if (!IsVisible)
        {
            Show();
        }

        PositionAtCursor();
        Activate();
        Focus();
        SearchBox.SelectAll();
        SearchBox.Focus();
    }

    // Spring up centred on the mouse, clamped to the cursor's monitor work area.
    private void PositionAtCursor()
    {
        var mouse = System.Windows.Forms.Control.MousePosition; // device pixels
        var source = PresentationSource.FromVisual(this);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var work = System.Windows.Forms.Screen.FromPoint(mouse).WorkingArea; // device pixels

        var left = mouse.X / dpiX - ActualWidth / 2;
        var top = mouse.Y / dpiY - ActualHeight / 2;

        var minLeft = work.Left / dpiX;
        var minTop = work.Top / dpiY;
        var maxLeft = work.Right / dpiX - ActualWidth;
        var maxTop = work.Bottom / dpiY - ActualHeight;

        Left = Math.Max(minLeft, Math.Min(left, maxLeft));
        Top = Math.Max(minTop, Math.Min(top, maxTop));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Modifier combos are reserved (none used today); let them pass through.
        if (Keyboard.Modifiers is not ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.ActivateSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_viewModel.IsSearching)
                {
                    ClearSearch();
                }
                else if (_viewModel.IsInCollection)
                {
                    _viewModel.DrillUpCommand.Execute(null);
                }
                else
                {
                    _viewModel.CloseCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.Back:
                // Only hijack Backspace when the search box is empty, so it still
                // edits text mid-query. Empty + inside a collection → drill up.
                if (!_viewModel.IsSearching && _viewModel.IsInCollection)
                {
                    _viewModel.DrillUpCommand.Execute(null);
                    e.Handled = true;
                }

                break;

            case Key.Left:
                if (!_viewModel.IsSearching)
                {
                    _viewModel.RotateCommand.Execute("-1");
                    e.Handled = true;
                }

                break;

            case Key.Right:
                if (!_viewModel.IsSearching)
                {
                    _viewModel.RotateCommand.Execute("1");
                    e.Handled = true;
                }

                break;

            default:
                if (!_viewModel.IsSearching && TryGetDigit(e.Key, out var digit) && digit is >= 1 and <= 6)
                {
                    _viewModel.OpenIndexedCommand.Execute(digit.ToString());
                    e.Handled = true;
                }

                break;
        }
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private static bool TryGetDigit(Key key, out int digit)
    {
        digit = key switch
        {
            >= Key.D0 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => -1,
        };

        return digit >= 0;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }
}
