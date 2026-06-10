using System;
using System.Linq;
using System.Windows;
using MyList.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

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

        Activate();
        Focus();
        SearchBox.SelectAll();
        SearchBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var first = _viewModel.Items.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        first.ActivateCommand.Execute(null);
        e.Handled = true;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }
}
