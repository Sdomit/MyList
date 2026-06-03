using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public enum ActionEditorType
{
    Command,
    Batch,
    PowerShell,
    PowerShellAdmin,
    Separator
}

public sealed class ActionItemEditorViewModel : ViewModelBase
{
    private string _dialogTitle;
    private ActionEditorType _selectedType;
    private string _name = string.Empty;
    private string _content = string.Empty;
    private string _workingDirectory = string.Empty;
    private bool _runAsAdmin;

    public ActionItemEditorViewModel(string dialogTitle, ActionEditorType initialType = ActionEditorType.Command)
    {
        _dialogTitle = dialogTitle;
        _selectedType = initialType;
        AvailableTypes = new ObservableCollection<ActionEditorType>(
            Enum.GetValues<ActionEditorType>());
        SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this, EventArgs.Empty), CanSave);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? SaveRequested;
    public event EventHandler? CancelRequested;

    public ObservableCollection<ActionEditorType> AvailableTypes { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetProperty(ref _dialogTitle, value);
    }

    public ActionEditorType SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                if (value == ActionEditorType.PowerShellAdmin)
                {
                    RunAsAdmin = true;
                }
                else if (value != ActionEditorType.Separator)
                {
                    RunAsAdmin = false;
                }

                OnPropertyChanged(nameof(IsSeparator));
                OnPropertyChanged(nameof(IsScriptAction));
                OnPropertyChanged(nameof(ConfirmButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set => SetProperty(ref _runAsAdmin, value);
    }

    public bool IsSeparator => SelectedType == ActionEditorType.Separator;

    public bool IsScriptAction => !IsSeparator;

    public string ConfirmButtonText => IsSeparator ? "Add Separator" : "Save Action";

    public bool CanSave()
    {
        if (IsSeparator)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Content);
    }

    public void Load(ItemModel item)
    {
        if (item.IsActionItem)
        {
            SelectedType = item.ActionKind switch
            {
                ActionKind.Batch => ActionEditorType.Batch,
                ActionKind.PowerShell when item.LaunchProfile.RunAsAdmin => ActionEditorType.PowerShellAdmin,
                ActionKind.PowerShell => ActionEditorType.PowerShell,
                _ => ActionEditorType.Command
            };
            Name = item.Name;
            Content = item.ActionContent;
            WorkingDirectory = item.LaunchProfile.WorkingDirectory;
            RunAsAdmin = item.LaunchProfile.RunAsAdmin;
        }
        else
        {
            SelectedType = ActionEditorType.Separator;
        }
    }

    public ActionKind GetActionKind()
    {
        return SelectedType switch
        {
            ActionEditorType.Batch => ActionKind.Batch,
            ActionEditorType.PowerShell => ActionKind.PowerShell,
            ActionEditorType.PowerShellAdmin => ActionKind.PowerShell,
            _ => ActionKind.Command
        };
    }
}
