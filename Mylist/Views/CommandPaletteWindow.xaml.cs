using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyList.ViewModels;

namespace MyList.Views;

public partial class CommandPaletteWindow : Window
{
    private readonly CommandPaletteViewModel _viewModel;

    public CommandPaletteWindow(
        MainViewModel mainVm,
        IReadOnlyList<CommandRow> commands,
        IReadOnlyList<SettingsRow> settings)
    {
        InitializeComponent();
        _viewModel = new CommandPaletteViewModel(mainVm, commands, settings);
        DataContext = _viewModel;
        Loaded += (_, _) => QueryBox.Focus();
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var shouldClose = _viewModel.ExecuteFocused();
            if (shouldClose)
            {
                DialogResult = true;
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            _viewModel.MoveFocus(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            _viewModel.MoveFocus(-1);
            e.Handled = true;
        }
    }

    private void OnRowMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListBox)
        {
            return;
        }

        var hit = e.OriginalSource as DependencyObject;
        while (hit is not null and not ListBoxItem)
        {
            hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
        }

        if (hit is ListBoxItem container && container.DataContext is IPaletteRow row && !row.IsOverflow)
        {
            row.Execute();
            if (!row.KeepPaletteOpen)
            {
                DialogResult = true;
            }
            e.Handled = true;
        }
    }
}
