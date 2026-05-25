namespace DockWindow;

public static class EdgeDetector
{
    public const int SnapThresholdPx = 8;
    public const int StripPx = 4;

    public static Edge Classify(Rect window, Rect workArea)
    {
        if (window.Left  <= workArea.Left  + SnapThresholdPx) return Edge.Left;
        if (window.Right >= workArea.Right - SnapThresholdPx) return Edge.Right;
        if (window.Top   <= workArea.Top   + SnapThresholdPx) return Edge.Top;
        return Edge.None;
    }

    public static Rect SnapFlush(Rect window, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => window with { X = workArea.Left },
        Edge.Right => window with { X = workArea.Right - window.Width },
        Edge.Top   => window with { Y = workArea.Top },
        _          => window,
    };

    public static Rect HiddenRect(Rect docked, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => docked with { X = workArea.Left  - (docked.Width  - StripPx) },
        Edge.Right => docked with { X = workArea.Right - StripPx },
        Edge.Top   => docked with { Y = workArea.Top   - (docked.Height - StripPx) },
        _          => docked,
    };

    public static Rect PeekStripRect(Rect docked, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => new Rect(workArea.Left,            docked.Top, StripPx,      docked.Height),
        Edge.Right => new Rect(workArea.Right - StripPx, docked.Top, StripPx,      docked.Height),
        Edge.Top   => new Rect(docked.Left, workArea.Top, docked.Width, StripPx),
        _          => docked,
    };
}
