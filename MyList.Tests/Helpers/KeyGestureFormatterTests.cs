using System.Windows.Input;
using MyList.Helpers;
using Xunit;

namespace MyList.Tests.Helpers;

public class KeyGestureFormatterTests
{
    [Fact]
    public void Format_NoModifiers_ReturnsKeyOnly()
    {
        Assert.Equal("F1", KeyGestureFormatter.Format(ModifierKeys.None, Key.F1));
    }

    [Fact]
    public void Format_CtrlN_ReturnsCtrlPlusN()
    {
        Assert.Equal("Ctrl+N", KeyGestureFormatter.Format(ModifierKeys.Control, Key.N));
    }

    [Fact]
    public void Format_CtrlShiftV_OrdersCtrlBeforeShift()
    {
        Assert.Equal("Ctrl+Shift+V", KeyGestureFormatter.Format(ModifierKeys.Control | ModifierKeys.Shift, Key.V));
    }

    [Fact]
    public void Format_AllModifiers_OrdersCtrlShiftAltWin()
    {
        var combo = ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows;
        Assert.Equal("Ctrl+Shift+Alt+Win+K", KeyGestureFormatter.Format(combo, Key.K));
    }

    [Fact]
    public void FromInputBindings_NullBindings_ReturnsNull()
    {
        Assert.Null(KeyGestureFormatter.FromInputBindings(null, null));
    }

    [Fact]
    public void FromInputBindings_NullCommand_ReturnsNull()
    {
        var bindings = new InputBindingCollection();
        Assert.Null(KeyGestureFormatter.FromInputBindings(bindings, null));
    }
}
