using DockWindow;
using Xunit;

public class EdgeDetectorTests
{
    private static readonly Rect Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Classify_returns_None_when_window_is_centered()
    {
        var w = new Rect(500, 500, 400, 300);
        Assert.Equal(Edge.None, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_returns_Left_at_threshold()
    {
        var w = new Rect(7, 500, 400, 300);
        Assert.Equal(Edge.Left, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_returns_None_just_past_threshold()
    {
        var w = new Rect(9, 500, 400, 300);
        Assert.Equal(Edge.None, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_returns_Right()
    {
        var w = new Rect(1920 - 400 + 1, 500, 400, 300);
        Assert.Equal(Edge.Right, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_returns_Top()
    {
        var w = new Rect(500, 7, 400, 300);
        Assert.Equal(Edge.Top, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_prefers_Left_over_Top_at_corner()
    {
        var w = new Rect(0, 0, 400, 300);
        Assert.Equal(Edge.Left, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void Classify_prefers_Right_over_Top_at_corner()
    {
        var w = new Rect(1920 - 400, 0, 400, 300);
        Assert.Equal(Edge.Right, EdgeDetector.Classify(w, Work));
    }

    [Fact]
    public void SnapFlush_Left_aligns_X_to_workarea_left()
    {
        var w = new Rect(5, 500, 400, 300);
        var snapped = EdgeDetector.SnapFlush(w, Work, Edge.Left);
        Assert.Equal(new Rect(0, 500, 400, 300), snapped);
    }

    [Fact]
    public void SnapFlush_Right_aligns_right_edge()
    {
        var w = new Rect(1500, 500, 400, 300);
        var snapped = EdgeDetector.SnapFlush(w, Work, Edge.Right);
        Assert.Equal(new Rect(1920 - 400, 500, 400, 300), snapped);
    }

    [Fact]
    public void SnapFlush_Top_aligns_Y_to_workarea_top()
    {
        var w = new Rect(500, 5, 400, 300);
        var snapped = EdgeDetector.SnapFlush(w, Work, Edge.Top);
        Assert.Equal(new Rect(500, 0, 400, 300), snapped);
    }

    [Fact]
    public void HiddenRect_Left_leaves_strip_visible()
    {
        var docked = new Rect(0, 500, 400, 300);
        var hidden = EdgeDetector.HiddenRect(docked, Work, Edge.Left);
        Assert.Equal(new Rect(-396, 500, 400, 300), hidden);
    }

    [Fact]
    public void HiddenRect_Right_leaves_strip_visible()
    {
        var docked = new Rect(1520, 500, 400, 300);
        var hidden = EdgeDetector.HiddenRect(docked, Work, Edge.Right);
        Assert.Equal(new Rect(1916, 500, 400, 300), hidden);
    }

    [Fact]
    public void HiddenRect_Top_leaves_strip_visible()
    {
        var docked = new Rect(500, 0, 400, 300);
        var hidden = EdgeDetector.HiddenRect(docked, Work, Edge.Top);
        Assert.Equal(new Rect(500, -296, 400, 300), hidden);
    }

    [Fact]
    public void PeekStripRect_Left_is_strip_wide_full_height()
    {
        var docked = new Rect(0, 500, 400, 300);
        var strip = EdgeDetector.PeekStripRect(docked, Work, Edge.Left);
        Assert.Equal(new Rect(0, 500, 4, 300), strip);
    }

    [Fact]
    public void PeekStripRect_Right_hugs_right_edge()
    {
        var docked = new Rect(1520, 500, 400, 300);
        var strip = EdgeDetector.PeekStripRect(docked, Work, Edge.Right);
        Assert.Equal(new Rect(1916, 500, 4, 300), strip);
    }

    [Fact]
    public void PeekStripRect_Top_full_width_strip_high()
    {
        var docked = new Rect(500, 0, 400, 300);
        var strip = EdgeDetector.PeekStripRect(docked, Work, Edge.Top);
        Assert.Equal(new Rect(500, 0, 400, 4), strip);
    }
}
