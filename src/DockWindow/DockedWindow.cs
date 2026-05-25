using System.Drawing;

namespace DockWindow;

public enum DockState { Hidden, Peeking, Visible }

public sealed class DockedWindow : IDisposable
{
    public IntPtr     Hwnd         { get; }
    public Edge       Edge         { get; private set; }
    public Rect       OriginalRect { get; private set; }
    public Rect       HiddenRect   { get; private set; }
    public Rect       WorkArea     { get; private set; }
    public DockState  State        { get; private set; } = DockState.Hidden;

    public event Action<DockedWindow>? Dead;

    private const int AnimDurationMs = 150;
    private const int HideDelayMs    = 300;

    private readonly System.Windows.Forms.Timer _anim;
    private Rect      _animFrom;
    private Rect      _animTo;
    private bool      _animBringToTop;
    private DateTime  _animStart;
    private DateTime? _cursorLeftAt;

    public DockedWindow(IntPtr hwnd, Edge edge, Rect docked, Rect workArea)
    {
        Hwnd = hwnd;
        Reconfigure(edge, docked, workArea);
        _anim = new System.Windows.Forms.Timer { Interval = 15 };
        _anim.Tick += OnAnimTick;
    }

    public void Reconfigure(Edge edge, Rect docked, Rect workArea)
    {
        Edge         = edge;
        WorkArea     = workArea;
        OriginalRect = EdgeDetector.SnapFlush(docked, workArea, edge);
        HiddenRect   = EdgeDetector.HiddenRect(OriginalRect, workArea, edge);
    }

    public void StartHide()
    {
        State = DockState.Hidden;
        AnimateTo(HiddenRect, bringToTop: false);
    }

    public void StartPeek()
    {
        State = DockState.Peeking;
        AnimateTo(OriginalRect, bringToTop: true);
    }

    public void Tick(Point cursor)
    {
        if (!Win32.IsWindow(Hwnd)) { Dead?.Invoke(this); return; }

        if (Win32.TryGetWorkArea(Hwnd, out var freshWork) && freshWork != WorkArea)
            Reconfigure(Edge, OriginalRect, freshWork);

        var peek = EdgeDetector.PeekStripRect(OriginalRect, WorkArea, Edge);

        switch (State)
        {
            case DockState.Hidden:
                if (peek.Contains(cursor.X, cursor.Y))
                    StartPeek();
                break;

            case DockState.Peeking:
            case DockState.Visible:
                if (OriginalRect.Contains(cursor.X, cursor.Y))
                {
                    State = DockState.Visible;
                    _cursorLeftAt = null;
                }
                else
                {
                    _cursorLeftAt ??= DateTime.UtcNow;
                    if ((DateTime.UtcNow - _cursorLeftAt.Value).TotalMilliseconds >= HideDelayMs)
                    {
                        _cursorLeftAt = null;
                        StartHide();
                    }
                }
                break;
        }
    }

    public void RestoreImmediate()
    {
        _anim.Stop();
        Win32.MoveWindowToTopAsync(Hwnd, OriginalRect);
        State = DockState.Visible;
    }

    private void AnimateTo(Rect to, bool bringToTop)
    {
        if (!Win32.TryGetWindowRect(Hwnd, out var from))
        {
            Dead?.Invoke(this);
            return;
        }
        _animFrom       = from;
        _animTo         = to;
        _animBringToTop = bringToTop;
        _animStart      = DateTime.UtcNow;
        _anim.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (!Win32.IsWindow(Hwnd))
        {
            _anim.Stop();
            Dead?.Invoke(this);
            return;
        }

        var t = (DateTime.UtcNow - _animStart).TotalMilliseconds / AnimDurationMs;
        if (t >= 1) { t = 1; _anim.Stop(); }
        var r = Animation.Lerp(_animFrom, _animTo, t);
        if (_animBringToTop)
            Win32.MoveWindowToTopAsync(Hwnd, r);
        else
            Win32.MoveWindowAsync(Hwnd, r);
    }

    public void Dispose()
    {
        _anim.Stop();
        _anim.Dispose();
    }
}
