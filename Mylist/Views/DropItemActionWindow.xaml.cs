using System.Windows;

namespace MyList.Views;

public enum DropItemActionChoice
{
    Cancel,
    CreateMtab,
    CreateCollection
}

public partial class DropItemActionWindow : Window
{
    public DropItemActionWindow(string prompt)
    {
        InitializeComponent();
        Prompt = prompt;
        DataContext = this;
    }

    public string Prompt { get; }

    public DropItemActionChoice SelectedAction { get; private set; } = DropItemActionChoice.Cancel;

    private void OnCreateMtab(object sender, RoutedEventArgs e)
    {
        SelectedAction = DropItemActionChoice.CreateMtab;
        DialogResult = true;
    }

    private void OnCreateCollection(object sender, RoutedEventArgs e)
    {
        SelectedAction = DropItemActionChoice.CreateCollection;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        SelectedAction = DropItemActionChoice.Cancel;
        DialogResult = false;
    }
}
