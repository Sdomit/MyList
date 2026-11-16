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
            ShowValidationError("Collection name is required.");
            return;
        }

        if (IsCollectionNameAvailable is not null && !IsCollectionNameAvailable(trimmedName))
        {
            ShowValidationError($"A collection named '{trimmedName}' already exists.");
            return;
        }

        CollectionName = trimmedName;
        _collection.Name = trimmedName;
        _collection.IsSmart = IsSmart;
        _collection.SmartType = SmartType;
        _collection.SmartValue = SmartValue;
        DialogResult = true;
    }

    private void ShowValidationError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        NameTextBox.Focus();
    }

    private void OnNameTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ErrorText is not null) ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OnNameLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.SelectAll();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
