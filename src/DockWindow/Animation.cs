namespace DockWindow;

public static class Animation
{
    public static double EaseOutCubic(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        var inv = 1.0 - t;
        return 1.0 - inv * inv * inv;
    }

    public static Rect Lerp(Rect a, Rect b, double t)
    {
        var e = EaseOutCubic(t);
        return new Rect(
            (int)Math.Round(a.X      + (b.X      - a.X)      * e),
            (int)Math.Round(a.Y      + (b.Y      - a.Y)      * e),
            (int)Math.Round(a.Width  + (b.Width  - a.Width)  * e),
            (int)Math.Round(a.Height + (b.Height - a.Height) * e));
    }
}
