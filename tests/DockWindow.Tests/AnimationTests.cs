using DockWindow;
using Xunit;

public class AnimationTests
{
    [Fact]
    public void EaseOutCubic_at_zero_is_zero()
    {
        Assert.Equal(0.0, Animation.EaseOutCubic(0));
    }

    [Fact]
    public void EaseOutCubic_at_one_is_one()
    {
        Assert.Equal(1.0, Animation.EaseOutCubic(1));
    }

    [Fact]
    public void EaseOutCubic_clamps_negative_to_zero()
    {
        Assert.Equal(0.0, Animation.EaseOutCubic(-0.5));
    }

    [Fact]
    public void EaseOutCubic_clamps_above_one_to_one()
    {
        Assert.Equal(1.0, Animation.EaseOutCubic(1.5));
    }

    [Fact]
    public void EaseOutCubic_is_monotonic()
    {
        double prev = -1;
        for (var t = 0.0; t <= 1.0; t += 0.05)
        {
            var v = Animation.EaseOutCubic(t);
            Assert.True(v >= prev, $"non-monotonic at t={t}, v={v}, prev={prev}");
            prev = v;
        }
    }

    [Fact]
    public void Lerp_at_zero_returns_start()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(500, 500, 200, 200);
        Assert.Equal(a, Animation.Lerp(a, b, 0));
    }

    [Fact]
    public void Lerp_at_one_returns_end()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(500, 500, 200, 200);
        Assert.Equal(b, Animation.Lerp(a, b, 1));
    }

    [Fact]
    public void Lerp_at_half_is_between()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(100, 100, 100, 100);
        var r = Animation.Lerp(a, b, 0.5);
        Assert.Equal(88, r.X);
        Assert.Equal(88, r.Y);
    }
}
