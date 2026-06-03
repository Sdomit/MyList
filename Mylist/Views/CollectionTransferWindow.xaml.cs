using System.Windows;

namespace MyList.Views;

public enum CollectionTransferAction
{
    Cancel,
    CopyToCollection,
    MoveToCollection
}

public partial class CollectionTransferWindow : Window
{
    public CollectionTransferWindow(string prompt)
    {
        InitializeComponent();
        Prompt = prompt;
        DataContext = this;
    }

    public string Prompt { get; }

    public CollectionTransferAction SelectedAction { get; private set; } = CollectionTransferAction.Cancel;

    private void OnCopyToCollection(object sender, RoutedEventArgs e)
    {
        SelectedAction = CollectionTransferAction.CopyToCollection;
        DialogResult = true;
    }

    private void OnMoveToCollection(object sender, RoutedEventArgs e)
    {
        SelectedAction = CollectionTransferAction.MoveToCollection;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        SelectedAction = CollectionTransferAction.Cancel;
        DialogResult = false;
    }
}
