using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Views;

public partial class MtabPickerWindow : Window
{
    public MtabPickerWindow(IEnumerable<ItemModel> mtabs, IReadOnlyList<string> incomingPaths)
    {
        InitializeComponent();
        ViewModel = new MtabPickerViewModel(mtabs ?? Enumerable.Empty<ItemModel>());
        DataContext = ViewModel;

        var incomingCount = incomingPaths?.Count ?? 0;
        IncomingSummaryText.Text = incomingCount == 1
            ? "1 valid folder will be appended."
            : $"{incomingCount} valid folders will be appended.";
    }

    public MtabPickerViewModel ViewModel { get; }

    public ItemModel? SelectedMtab => ViewModel.SelectedMtab;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ErrorText is not null) ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMtab is null)
        {
            ErrorText.Text = "Select an Mtab first.";
            ErrorText.Visibility = Visibility.Visible;
            SearchTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
