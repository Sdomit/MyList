using System.Windows;
using MyList.ViewModels;
using MyList.Models;

namespace MyList.Views;

public partial class EditItemWindow : Window
{
    private readonly EditItemViewModel _viewModel;

    public EditItemWindow(ItemModel item)
    {
        InitializeComponent();
        _viewModel = new EditItemViewModel(item);
        DataContext = _viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _viewModel.Apply();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
