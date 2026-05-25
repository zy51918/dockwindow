using System.Drawing;

namespace DockWindow;

public sealed class MousePoller : IDisposable
{
    public event Action<Point>? Moved;

    private readonly System.Windows.Forms.Timer _timer;

    public MousePoller(int intervalMs = 50)
    {
        _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
        _timer.Tick += (_, _) =>
        {
            if (Win32.GetCursorPos(out var p))
                Moved?.Invoke(new Point(p.X, p.Y));
        };
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
