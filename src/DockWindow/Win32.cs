using System.Runtime.InteropServices;
using System.Text;

namespace DockWindow;

internal static class Win32
{
    public const uint WINEVENT_OUTOFCONTEXT      = 0x0000;
    public const uint EVENT_SYSTEM_MOVESIZEEND   = 0x000B;
    public const int  OBJID_WINDOW               = 0;
    public const uint MONITOR_DEFAULTTONEAREST   = 0x00000002;
    public const int  GWL_STYLE                  = -16;
    public const int  GWL_EXSTYLE                = -20;
    public const long WS_CAPTION                 = 0x00C00000;
    public const long WS_EX_TOOLWINDOW           = 0x00000080;
    public const uint GW_OWNER                   = 4;
    public const uint SWP_NOSIZE                 = 0x0001;
    public const uint SWP_NOMOVE                 = 0x0002;
    public const uint SWP_NOZORDER               = 0x0004;
    public const uint SWP_NOACTIVATE             = 0x0010;
    public const uint SWP_ASYNCWINDOWPOS         = 0x4000;
    public const uint DWMWA_CLOAKED              = 14;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    public static long GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex).ToInt64()
            : GetWindowLong32(hWnd, nIndex).ToInt64();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public static Rect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    public static bool TryGetWindowRect(IntPtr hwnd, out Rect rect)
    {
        rect = default;
        if (!GetWindowRect(hwnd, out var r)) return false;
        rect = ToRect(r);
        return true;
    }

    public static bool TryGetWorkArea(IntPtr hwnd, out Rect work)
    {
        work = default;
        var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (mon == IntPtr.Zero) return false;
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return false;
        work = ToRect(mi.rcWork);
        return true;
    }

    public static readonly IntPtr HWND_TOPMOST   = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public static bool MoveWindowAsync(IntPtr hwnd, Rect r) =>
        SetWindowPos(hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

    public static bool SetTopmost(IntPtr hwnd, bool topmost) =>
        SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

    public static bool TryGetProcessFileName(IntPtr hwnd, out string fileName)
    {
        fileName = string.Empty;
        if (GetWindowThreadProcessId(hwnd, out var processId) == 0 || processId == 0)
            return false;

        var processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (processHandle == IntPtr.Zero) return false;

        try
        {
            var path = new StringBuilder(32768);
            var size = (uint)path.Capacity;
            if (!QueryFullProcessImageName(processHandle, 0, path, ref size))
                return false;

            fileName = Path.GetFileName(path.ToString());
            return fileName.Length != 0;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static bool IsDockable(IntPtr hwnd)
    {
        if (!IsWindow(hwnd))        return false;
        if (!IsWindowVisible(hwnd)) return false;
        if (hwnd == GetShellWindow()) return false;
        if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return false;

        var style   = GetWindowLongPtr(hwnd, GWL_STYLE);
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if ((style   & WS_CAPTION)       == 0) return false;
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0
            && cloaked != 0) return false;

        return true;
    }
}
