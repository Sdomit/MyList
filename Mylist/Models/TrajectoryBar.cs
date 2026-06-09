namespace MyList.Models;

public sealed record TrajectoryBar(double Height, double Opacity)
{
    public int DayOffset { get; init; }
    public int AccessCount { get; init; }
}
