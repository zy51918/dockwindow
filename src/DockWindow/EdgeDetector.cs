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
}
