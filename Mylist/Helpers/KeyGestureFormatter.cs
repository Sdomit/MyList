using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace MyList.Helpers;

public static class KeyGestureFormatter
{
    public static string? FromInputBindings(InputBindingCollection? bindings, ICommand? command)
    {
        if (bindings is null || command is null)
        {
            return null;
        }

        foreach (var binding in bindings.OfType<KeyBinding>())
        {
            if (ReferenceEquals(binding.Command, command))
            {
                return Format(binding.Modifiers, binding.Key);
            }
        }

        return null;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
