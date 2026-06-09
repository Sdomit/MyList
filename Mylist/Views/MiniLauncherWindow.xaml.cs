using System;
using System.Windows;
using MyList.ViewModels;

namespace MyList.Views;

public partial class MiniLauncherWindow : Window
{
    private readonly MiniLauncherViewModel _viewModel;

    public MiniLauncherWindow(MiniLauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
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
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }
}
