using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using MyList.Helpers;
using MessageBox = System.Windows.MessageBox;

namespace MyList.Views;

public partial class MtabEditorWindow : Window, INotifyPropertyChanged
{
    private const int DefaultMaxPathRows = 10;
    private readonly List<string> _validPaths = new();
    private readonly int _pathRowLimit;
    private bool _isAdjustingRows;
    private string _mtabName = string.Empty;
    private string _summaryText = "No valid folders yet.";
    private readonly DispatcherTimer _validationTimer;

    public MtabEditorWindow(string dialogTitle, string suggestedName, IEnumerable<string>? initialPaths = null)
    {
        var existingPaths = initialPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .ToList() ?? new List<string>();
        _pathRowLimit = Math.Max(DefaultMaxPathRows, existingPaths.Count);

        PathEntries.CollectionChanged += OnPathEntriesCollectionChanged;

        _validationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _validationTimer.Tick += (_, _) =>
        {
            _validationTimer.Stop();
            RebuildValidation();
        };

        DialogTitle = dialogTitle;
        MtabName = suggestedName;
        InitializePathEntries(existingPaths);

        InitializeComponent();
        DataContext = this;
        RebuildValidation();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DialogTitle { get; }

    public string MtabName
    {
        get => _mtabName;
        set
        {
            if (_mtabName == value)
            {
                return;
            }

            _mtabName = value;
            OnPropertyChanged();
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set
        {
            if (_summaryText == value)
            {
                return;
            }

            _summaryText = value;
            OnPropertyChanged();
        }
    }

    public string ConfirmButtonText => $"Save ({_validPaths.Count})";

    public ObservableCollection<MtabPathEntry> PathEntries { get; } = new();

    public ObservableCollection<MtabValidationEntry> ValidationEntries { get; } = new();

    public IReadOnlyList<string> ValidPaths => _validPaths;

    private void InitializePathEntries(IReadOnlyList<string> existingPaths)
    {
        if (existingPaths.Count == 0)
        {
            AddPathEntry(string.Empty);
            return;
        }

        foreach (var path in existingPaths)
        {
            AddPathEntry(path);
        }

        if (PathEntries.Count < _pathRowLimit)
        {
            AddPathEntry(string.Empty);
        }

        RenumberPathEntries();
    }

    private void OnPathEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (MtabPathEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnPathEntryPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (MtabPathEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnPathEntryPropertyChanged;
            }
        }
    }

    private void OnPathEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MtabPathEntry.PathText))
        {
            NormalizePathRows();
        }
    }

    private void NormalizePathRows()
    {
        if (_isAdjustingRows)
        {
            return;
        }

        _isAdjustingRows = true;
        try
        {
            var removedAny = RemoveEmptyRows();
            var addedRow = EnsureTrailingEmptyRow();
            RenumberPathEntries();
            ScheduleValidation();

            if (addedRow && IsLoaded)
            {
                Dispatcher.BeginInvoke(new Action(() => PathRowsScrollViewer.ScrollToBottom()));
            }
        }
        finally
        {
            _isAdjustingRows = false;
        }
    }

    private bool RemoveEmptyRows()
    {
        var removedAny = false;
        for (var index = PathEntries.Count - 1; index >= 0; index--)
        {
            if (string.IsNullOrWhiteSpace(PathEntries[index].PathText))
            {
                PathEntries.RemoveAt(index);
                removedAny = true;
            }
        }

        return removedAny;
    }

    private bool EnsureTrailingEmptyRow()
    {
        if (PathEntries.Count == 0)
        {
            AddPathEntry(string.Empty);
            return true;
        }

        if (PathEntries.Count >= _pathRowLimit)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(PathEntries[^1].PathText))
        {
            AddPathEntry(string.Empty);
            return true;
        }

        return false;
    }

    private void AddPathEntry(string path)
    {
        PathEntries.Add(new MtabPathEntry
        {
            PathText = path?.Trim() ?? string.Empty
        });
    }

    private void RenumberPathEntries()
    {
        for (var index = 0; index < PathEntries.Count; index++)
        {
            PathEntries[index].Number = index + 1;
        }
    }

    private void ScheduleValidation()
    {
        _validationTimer.Stop();
        _validationTimer.Start();
    }

    private void RebuildValidation()
    {
        ValidationEntries.Clear();
        _validPaths.Clear();

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in PathEntries)
        {
            var raw = entry.PathText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                entry.ValidationMessage = string.Empty;
                continue;
            }

            if (!PathNormalizationHelper.TryNormalizeUserPath(raw, out var normalizedPath, out var key, out var status))
            {
                var message = ValidationMessageForStatus(status);
                entry.ValidationMessage = message;
                ValidationEntries.Add(new MtabValidationEntry(entry.Label, raw, message));
                continue;
            }

            if (!seenKeys.Add(key))
            {
                const string duplicateMessage = "Duplicate in payload.";
                entry.ValidationMessage = duplicateMessage;
                ValidationEntries.Add(new MtabValidationEntry(entry.Label, normalizedPath, duplicateMessage));
                continue;
            }

            if (!IsFolderPath(normalizedPath))
            {
                const string folderMessage = "Only folders/UNC paths are allowed.";
                entry.ValidationMessage = folderMessage;
                ValidationEntries.Add(new MtabValidationEntry(entry.Label, normalizedPath, folderMessage));
                continue;
            }

            entry.ValidationMessage = "Valid";
            _validPaths.Add(normalizedPath);
            ValidationEntries.Add(new MtabValidationEntry(entry.Label, normalizedPath, "Valid"));
        }

        var invalidCount = ValidationEntries.Count(entry => !string.Equals(entry.Message, "Valid", StringComparison.Ordinal));
        SummaryText = $"Valid: {_validPaths.Count}, invalid/skipped: {invalidCount}";
        OnPropertyChanged(nameof(ConfirmButtonText));
    }

    private static bool IsFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // UNC paths are accepted without touching the network; Directory.Exists on
        // an offline share would block the UI thread.
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        return Directory.Exists(path);
    }

    private static string ValidationMessageForStatus(PathValidationStatus status)
    {
        return status switch
        {
            PathValidationStatus.Empty => "Empty input.",
            PathValidationStatus.NotRooted => "Path is not rooted.",
            PathValidationStatus.InvalidFormat => "Invalid path format.",
            _ => "Invalid path."
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_validationTimer.IsEnabled)
        {
            _validationTimer.Stop();
            RebuildValidation();
        }

        if (string.IsNullOrWhiteSpace(MtabName))
        {
            MessageBox.Show("Mtab name is required.", "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_validPaths.Count < 2)
        {
            MessageBox.Show("Add at least 2 valid folder paths.", "Mtab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class MtabPathEntry : INotifyPropertyChanged
{
    private int _number;
    private string _pathText = string.Empty;
    private string _validationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Number
    {
        get => _number;
        set
        {
            if (_number == value)
            {
                return;
            }

            _number = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
        }
    }

    public string Label => $"Path {Number}";

    public string PathText
    {
        get => _pathText;
        set
        {
            var trimmedValue = value ?? string.Empty;
            if (_pathText == trimmedValue)
            {
                return;
            }

            _pathText = trimmedValue;
            OnPropertyChanged();
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            if (_validationMessage == value)
            {
                return;
            }

            _validationMessage = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record MtabValidationEntry(string Label, string Input, string Message);
