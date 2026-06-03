using System;
using System.Windows;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Views;

public partial class RenameCollectionWindow : Window
{
    private readonly CollectionViewModel _collection;

    public RenameCollectionWindow(CollectionViewModel collection, string dialogTitle = "Edit Collection")
    {
        InitializeComponent();
        _collection = collection;
        DialogTitle = dialogTitle;
        Title = dialogTitle;
        CollectionName = collection.Name;
        IsSmart = collection.IsSmart;
        SmartType = collection.SmartType;
        SmartValue = collection.SmartValue;
        SmartTypes = Enum.GetValues<SmartCollectionType>();
        DataContext = this;
    }

    public string DialogTitle { get; }
    public string CollectionName { get; set; }
    public bool IsSmart { get; set; }
    public SmartCollectionType SmartType { get; set; }
    public string SmartValue { get; set; } = string.Empty;
    public Array SmartTypes { get; }
    public Func<string, bool>? IsCollectionNameAvailable { get; init; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var trimmedName = CollectionName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            System.Windows.MessageBox.Show("Collection name is required.", DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsCollectionNameAvailable is not null && !IsCollectionNameAvailable(trimmedName))
        {
            System.Windows.MessageBox.Show(
                $"A collection named '{trimmedName}' already exists.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        CollectionName = trimmedName;
        _collection.Name = trimmedName;
        _collection.IsSmart = IsSmart;
        _collection.SmartType = SmartType;
        _collection.SmartValue = SmartValue;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
