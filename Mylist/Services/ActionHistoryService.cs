using System;
using System.Collections.Generic;
using MyList.Models;

namespace MyList.Services;

public sealed class ActionHistoryService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string UndoLabel => CanUndo ? _undoStack.Peek().Name : string.Empty;
    public string RedoLabel => CanRedo ? _redoStack.Peek().Name : string.Empty;

    public void Execute(IUndoableAction action)
    {
        action.Apply();
        _undoStack.Push(action);
        _redoStack.Clear();
        OnStateChanged();
    }

    public bool TryUndo()
    {
        if (!CanUndo)
        {
            return false;
        }

        var action = _undoStack.Pop();
        action.Revert();
        _redoStack.Push(action);
        OnStateChanged();
        return true;
    }

    public bool TryRedo()
    {
        if (!CanRedo)
        {
            return false;
        }

        var action = _redoStack.Pop();
        action.Apply();
        _undoStack.Push(action);
        OnStateChanged();
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
