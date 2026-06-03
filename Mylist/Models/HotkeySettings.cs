using System.Windows.Input;

namespace MyList.Models;

public sealed class HotkeySettings
{
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
    public Key Key { get; set; } = Key.Q;

    public override string ToString()
    {
        if (Key == Key.None && Modifiers == HotkeyModifiers.None)
        {
            return "None";
        }

        return $"{Modifiers}+{Key}";
    }
}
