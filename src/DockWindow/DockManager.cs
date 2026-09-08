using System.Drawing;

namespace DockWindow;

public sealed class DockManager : IDisposable
{
    private readonly Dictionary<IntPtr, DockedWindow> _docked = new();
    private readonly WindowEventHook _hook;
    private readonly MousePoller     _poller;
    private DockPolicy _policy;

    public bool Enabled { get; set; } = true;

    public DockManager(WindowEventHook hook, MousePoller poller, DockPolicy policy)
    {
        _hook   = hook;
        _poller = poller;
        _policy = policy;
        _hook.WindowMoved += OnWindowMoved;
        _poller.Moved     += OnMouseMoved;
    }

    private void OnWindowMoved(IntPtr hwnd)
    {
        if (!Enabled) return;

        var isKnown = _docked.ContainsKey(hwnd);

        if (!Win32.IsDockable(hwnd))
        {
            if (isKnown) Remove(hwnd);
            return;
        }

        if (_policy.HasRules &&
            Win32.TryGetProcessFileName(hwnd, out var processName) &&
            !_policy.IsAllowed(processName))
        {
            if (isKnown)
            {
                _docked[hwnd].RestoreImmediate();
                Remove(hwnd);
            }
            return;
        }

        if (!Win32.TryGetWindowRect(hwnd, out var rect)) return;
        if (!Win32.TryGetWorkArea(hwnd, out var work))   return;

        var edge = EdgeDetector.Classify(rect, work);

        if (edge == Edge.None)
        {
            if (isKnown) Remove(hwnd);
            return;
        }

        if (isKnown)
        {
            var existing = _docked[hwnd];
            if (NearlyEqual(rect, existing.HiddenRect, 6) ||
                NearlyEqual(rect, existing.OriginalRect, 6))
                return;

            existing.Reconfigure(edge, rect, work);
            existing.StartHide();
            return;
        }

        var dw = new DockedWindow(hwnd, edge, rect, work);
        dw.Dead += w => Remove(w.Hwnd);
        _docked[hwnd] = dw;
        dw.StartHide();
    }

    private static bool NearlyEqual(Rect a, Rect b, int tol) =>
        Math.Abs(a.X - b.X) <= tol &&
        Math.Abs(a.Y - b.Y) <= tol &&
        Math.Abs(a.Width  - b.Width)  <= tol &&
        Math.Abs(a.Height - b.Height) <= tol;

    private void OnMouseMoved(Point cursor)
    {
        if (!Enabled) return;
        foreach (var dw in _docked.Values.ToList())
            dw.Tick(cursor);
    }

    private void Remove(IntPtr hwnd)
    {
        if (_docked.Remove(hwnd, out var dw))
            dw.Dispose();
    }

    public void ApplyPolicy(DockPolicy policy)
    {
        _policy = policy;
        foreach (var dw in _docked.Values.ToList())
        {
            if (!policy.HasRules ||
                !Win32.TryGetProcessFileName(dw.Hwnd, out var processName) ||
                policy.IsAllowed(processName))
                continue;

            dw.RestoreImmediate();
            Remove(dw.Hwnd);
        }
    }

    public void RestoreAll()
    {
        foreach (var dw in _docked.Values.ToList())
        {
            dw.RestoreImmediate();
            dw.Dispose();
        }
        _docked.Clear();
    }

    public void Dispose()
    {
        _hook.WindowMoved -= OnWindowMoved;
        _poller.Moved     -= OnMouseMoved;
        foreach (var dw in _docked.Values)
            dw.Dispose();
        _docked.Clear();
    }
}
