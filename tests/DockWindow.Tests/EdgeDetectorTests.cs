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
}
