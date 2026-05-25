namespace DockWindow;

public sealed class WindowEventHook : IDisposable
{
    public event Action<IntPtr>? WindowMoved;

    private readonly Win32.WinEventDelegate _delegate;
    private readonly IntPtr _hook;
    private readonly SynchronizationContext _ui;

    public WindowEventHook(SynchronizationContext uiContext)
    {
        _ui = uiContext;
        _delegate = OnEvent;
        _hook = Win32.SetWinEventHook(
            Win32.EVENT_SYSTEM_MOVESIZEEND,
            Win32.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _delegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("SetWinEventHook failed");
    }

    private void OnEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                         int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != Win32.OBJID_WINDOW) return;
        if (hwnd == IntPtr.Zero) return;

        var captured = hwnd;
        _ui.Post(_ => WindowMoved?.Invoke(captured), null);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            Win32.UnhookWinEvent(_hook);
    }
}
