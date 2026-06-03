using System.Windows;
using MyList.ViewModels;

namespace MyList.Views;

public partial class ActionItemWindow : Window
{
    private readonly ActionItemEditorViewModel _viewModel;

    public ActionItemWindow(string dialogTitle, ActionEditorType initialType = ActionEditorType.Command)
    {
        InitializeComponent();
        _viewModel = new ActionItemEditorViewModel(dialogTitle, initialType);
        _viewModel.SaveRequested += (_, _) => TrySave();
        _viewModel.CancelRequested += (_, _) => Close();
        DataContext = _viewModel;
    }

    public ActionItemEditorViewModel ViewModel => _viewModel;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        TrySave();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TrySave()
    {
        if (!_viewModel.CanSave())
        {
            return;
        }

        DialogResult = true;
    }
}
