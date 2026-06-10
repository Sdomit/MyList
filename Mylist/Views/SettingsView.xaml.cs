using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using MyList.Models;
using MyList.Services;
using MyList.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MyList.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private SettingsViewModel? _subscribedVm;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        if (DataContext is SettingsViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyPendingAnchor(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.PendingSectionAnchor) && sender is SettingsViewModel vm)
        {
            ApplyPendingAnchor(vm);
        }
    }

    private void ApplyPendingAnchor(SettingsViewModel vm)
    {
        var anchor = vm.PendingSectionAnchor;
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return;
        }

        var header = MapAnchorToTabHeader(anchor);
        var tab = SettingsTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Header?.ToString(), header, System.StringComparison.OrdinalIgnoreCase));

        if (tab is not null)
        {
            SettingsTabs.SelectedItem = tab;
        }

        // Clear after consuming so subsequent identical anchor reuses fire again.
        vm.PendingSectionAnchor = null;
    }

    private static string MapAnchorToTabHeader(string anchor) => anchor switch
    {
        "Appearance" => "Appearance",
        "Density" => "Appearance",
        "Hotkeys" => "Hotkeys",
        "Startup" => "General",
        "Storage" => "Data & sync",
        "Diagnostics" => "Tools & diagnostics",
        "About" => "About",
        _ => anchor,
    };

    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        viewModel.UpdateHotkey(new HotkeySettings { Key = key, Modifiers = modifiers });
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl
               || key == Key.LeftAlt || key == Key.RightAlt
               || key == Key.LeftShift || key == Key.RightShift
               || key == Key.LWin || key == Key.RWin;
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (System.Exception ex)
        {
            LogService.Instance.Log(ex, $"Failed to open URL: {e.Uri}");
        }
    }
}
