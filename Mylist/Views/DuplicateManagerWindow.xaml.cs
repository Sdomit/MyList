using System.Windows;
using MyList.ViewModels;

namespace MyList.Views;

public partial class DuplicateManagerWindow : Window
{
    public DuplicateManagerWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = new DuplicateManagerViewModel(mainViewModel);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
