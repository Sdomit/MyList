using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using MyList.ViewModels;

namespace MyList.Views;

public partial class CommandPaletteWindow : Window
{
    private readonly CommandPaletteViewModel _viewModel;

    public CommandPaletteWindow(IReadOnlyList<CommandPaletteEntry> entries)
    {
        InitializeComponent();
        _viewModel = new CommandPaletteViewModel(entries);
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
            _viewModel.ExecuteSelected();
            DialogResult = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (CommandList.SelectedIndex < CommandList.Items.Count - 1)
            {
                CommandList.SelectedIndex++;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (CommandList.SelectedIndex > 0)
            {
                CommandList.SelectedIndex--;
            }
            e.Handled = true;
        }
    }
}
