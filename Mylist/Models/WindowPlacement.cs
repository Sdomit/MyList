namespace MyList.Models;

public sealed class WindowPlacement
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 460;
    public double Height { get; set; } = 720;
    public bool IsMaximized { get; set; }
    public WindowDockEdge DockEdge { get; set; } = WindowDockEdge.Left;
}
