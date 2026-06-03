using System;
using System.Collections.Generic;

namespace MyList.Models;

public interface IUndoableAction
{
    string Name { get; }
    void Apply();
    void Revert();
}

public sealed class DelegateUndoableAction : IUndoableAction
{
    private readonly Action _apply;
    private readonly Action _revert;

    public DelegateUndoableAction(string name, Action apply, Action revert)
    {
        Name = name;
        _apply = apply;
        _revert = revert;
    }

    public string Name { get; }

    public void Apply() => _apply();

    public void Revert() => _revert();
}

public sealed class CompositeUndoableAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;

    public CompositeUndoableAction(string name, IReadOnlyList<IUndoableAction> actions)
    {
        Name = name;
        _actions = actions;
    }

    public string Name { get; }

    public void Apply()
    {
        foreach (var action in _actions)
        {
            action.Apply();
        }
    }

    public void Revert()
    {
        for (var index = _actions.Count - 1; index >= 0; index--)
        {
            _actions[index].Revert();
        }
    }
}
