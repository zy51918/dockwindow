# DockWindow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app that auto-hides any window dragged to a screen edge, leaving a 4-px peek strip; hovering the strip slides it back in, mouse-leave slides it out.

**Architecture:** .NET 8 WinForms tray app. Pure-geometry units (`EdgeDetector`, `Animation`) are TDD'd with xUnit. Win32 interop is isolated in `Win32.cs`. A `WindowEventHook` raises C# events on `EVENT_SYSTEM_MOVESIZEEND`; a `MousePoller` raises tick events; `DockManager` wires them to `DockedWindow` state machines that drive per-window animation timers.

**Tech Stack:** .NET 8 (`net8.0-windows`), C# 12, WinForms (NotifyIcon + Timer), Win32 P/Invoke (user32, dwmapi), xUnit + Microsoft.NET.Test.Sdk.

---

## File Structure

```
dockwindow/
  DockWindow.sln
  src/DockWindow/
    DockWindow.csproj
    Program.cs                (entry, [STAThread])
    TrayContext.cs            (ApplicationContext + NotifyIcon)
    DockManager.cs            (registry, wires events to DockedWindows)
    DockedWindow.cs           (per-window state machine + animation timer)
    WindowEventHook.cs        (SetWinEventHook wrapper)
    MousePoller.cs            (50 ms cursor poll timer)
    EdgeDetector.cs           (pure geometry — no Win32)
    Animation.cs              (pure math: ease-out, lerp)
    Win32.cs                  (P/Invoke)
    Geometry.cs               (Rect record + Point alias usage)
    app.manifest              (PerMonitorV2)
  tests/DockWindow.Tests/
    DockWindow.Tests.csproj
    EdgeDetectorTests.cs
    AnimationTests.cs
  README.md
```

The csproj for `DockWindow` exposes `InternalsVisibleTo("DockWindow.Tests")` so tests can reach `internal` helpers without making them `public`.

---

## Task 1: Solution + project scaffolding

**Files:**
- Create: `DockWindow.sln`
- Create: `src/DockWindow/DockWindow.csproj`
- Create: `src/DockWindow/app.manifest`
- Create: `tests/DockWindow.Tests/DockWindow.Tests.csproj`
- Create: `.gitignore`

- [ ] **Step 1: Create solution and project scaffolding**

Run from `d:\projects\mywork\dockwindow`:

```powershell
dotnet new sln -n DockWindow
dotnet new winforms -n DockWindow -o src/DockWindow --framework net8.0-windows
dotnet new xunit   -n DockWindow.Tests -o tests/DockWindow.Tests --framework net8.0
dotnet sln add src/DockWindow/DockWindow.csproj
dotnet sln add tests/DockWindow.Tests/DockWindow.Tests.csproj
dotnet add tests/DockWindow.Tests/DockWindow.Tests.csproj reference src/DockWindow/DockWindow.csproj
```

- [ ] **Step 2: Replace `DockWindow.csproj` with the final form**

Write `src/DockWindow/DockWindow.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>DockWindow</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>DockWindow</AssemblyName>
    <LangVersion>12</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="DockWindow.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Delete the default `Form1.cs`, `Form1.Designer.cs`, `Form1.resx`, and stock `Program.cs`**

```powershell
Remove-Item src/DockWindow/Form1.cs, src/DockWindow/Form1.Designer.cs, src/DockWindow/Form1.resx, src/DockWindow/Program.cs -Force
```

- [ ] **Step 4: Write `app.manifest` with PerMonitorV2 DPI awareness**

Write `src/DockWindow/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="DockWindow.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 5: Write `.gitignore`**

Write `.gitignore`:

```
bin/
obj/
.vs/
*.user
*.suo
TestResults/
```

- [ ] **Step 6: Verify solution builds (empty WinForms shell will fail because we deleted Program.cs; that's fine — only the test project needs to build right now)**

Run:

```powershell
dotnet build tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: succeeds. The main project will not build until Task 9; that's expected.

- [ ] **Step 7: Commit**

```powershell
git init
git add .
git commit -m "chore: scaffold solution, projects, manifest"
```

---

## Task 2: Geometry primitives

**Files:**
- Create: `src/DockWindow/Geometry.cs`

- [ ] **Step 1: Write the `Rect` record**

Write `src/DockWindow/Geometry.cs`:

```csharp
namespace DockWindow;

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Left   => X;
    public int Top    => Y;
    public int Right  => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int px, int py) =>
        px >= Left && px < Right && py >= Top && py < Bottom;
}

public enum Edge { None, Left, Right, Top }
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: fails only on missing `Program.cs` entry point. Compile of `Geometry.cs` itself should succeed (no errors referencing `Geometry.cs`). If errors are unrelated to missing entry point, fix.

(Workaround for entry-point error: temporarily add `internal static class Program { static void Main() { } }` somewhere — but skip; we will add `Program.cs` in Task 9. For now, building the test project in Task 3 is sufficient verification.)

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/Geometry.cs
git commit -m "feat: add Rect record and Edge enum"
```

---

## Task 3: EdgeDetector — Classify (TDD)

**Files:**
- Create: `tests/DockWindow.Tests/EdgeDetectorTests.cs`
- Create: `src/DockWindow/EdgeDetector.cs`

- [ ] **Step 1: Write the failing tests for `Classify`**

Write `tests/DockWindow.Tests/EdgeDetectorTests.cs`:

```csharp
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
        var w = new Rect(7, 500, 400, 300); // 7 px from left, within 8-px threshold
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
        var w = new Rect(1920 - 400 + 1, 500, 400, 300); // Right = 1921, work.Right = 1920, within threshold
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
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: compile error "EdgeDetector does not exist."

- [ ] **Step 3: Implement `EdgeDetector.Classify`**

Write `src/DockWindow/EdgeDetector.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests, verify pass**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: all 7 pass.

- [ ] **Step 5: Commit**

```powershell
git add tests/DockWindow.Tests/EdgeDetectorTests.cs src/DockWindow/EdgeDetector.cs
git commit -m "feat: EdgeDetector.Classify with TDD"
```

---

## Task 4: EdgeDetector — SnapFlush, HiddenRect, PeekStripRect (TDD)

**Files:**
- Modify: `tests/DockWindow.Tests/EdgeDetectorTests.cs`
- Modify: `src/DockWindow/EdgeDetector.cs`

- [ ] **Step 1: Append failing tests**

Append to `tests/DockWindow.Tests/EdgeDetectorTests.cs` (inside the class):

```csharp
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
        // X = workArea.Left - (Width - STRIP) = 0 - (400 - 4) = -396
        Assert.Equal(new Rect(-396, 500, 400, 300), hidden);
    }

    [Fact]
    public void HiddenRect_Right_leaves_strip_visible()
    {
        var docked = new Rect(1520, 500, 400, 300);
        var hidden = EdgeDetector.HiddenRect(docked, Work, Edge.Right);
        // X = workArea.Right - STRIP = 1920 - 4 = 1916
        Assert.Equal(new Rect(1916, 500, 400, 300), hidden);
    }

    [Fact]
    public void HiddenRect_Top_leaves_strip_visible()
    {
        var docked = new Rect(500, 0, 400, 300);
        var hidden = EdgeDetector.HiddenRect(docked, Work, Edge.Top);
        // Y = workArea.Top - (Height - STRIP) = 0 - (300 - 4) = -296
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
```

- [ ] **Step 2: Run tests, verify the new ones fail**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: compile error — `SnapFlush`, `HiddenRect`, `PeekStripRect` not defined.

- [ ] **Step 3: Add the three methods**

Append to `src/DockWindow/EdgeDetector.cs` (inside the class):

```csharp
    public static Rect SnapFlush(Rect window, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => window with { X = workArea.Left },
        Edge.Right => window with { X = workArea.Right - window.Width },
        Edge.Top   => window with { Y = workArea.Top },
        _          => window,
    };

    public static Rect HiddenRect(Rect docked, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => docked with { X = workArea.Left  - (docked.Width  - StripPx) },
        Edge.Right => docked with { X = workArea.Right - StripPx },
        Edge.Top   => docked with { Y = workArea.Top   - (docked.Height - StripPx) },
        _          => docked,
    };

    public static Rect PeekStripRect(Rect docked, Rect workArea, Edge edge) => edge switch
    {
        Edge.Left  => new Rect(workArea.Left,             docked.Top, StripPx,      docked.Height),
        Edge.Right => new Rect(workArea.Right - StripPx,  docked.Top, StripPx,      docked.Height),
        Edge.Top   => new Rect(docked.Left, workArea.Top, docked.Width, StripPx),
        _          => docked,
    };
```

- [ ] **Step 4: Run tests, verify all pass**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: 16 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add tests/DockWindow.Tests/EdgeDetectorTests.cs src/DockWindow/EdgeDetector.cs
git commit -m "feat: SnapFlush, HiddenRect, PeekStripRect"
```

---

## Task 5: Animation math (TDD)

**Files:**
- Create: `tests/DockWindow.Tests/AnimationTests.cs`
- Create: `src/DockWindow/Animation.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/DockWindow.Tests/AnimationTests.cs`:

```csharp
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
        // ease-out at 0.5 = 1 - 0.5^3 = 0.875
        Assert.Equal(88, r.X);
        Assert.Equal(88, r.Y);
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: compile error — `Animation` not defined.

- [ ] **Step 3: Implement `Animation`**

Write `src/DockWindow/Animation.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests, verify all pass**

```powershell
dotnet test tests/DockWindow.Tests/DockWindow.Tests.csproj
```

Expected: 24 tests pass (16 EdgeDetector + 8 Animation).

- [ ] **Step 5: Commit**

```powershell
git add tests/DockWindow.Tests/AnimationTests.cs src/DockWindow/Animation.cs
git commit -m "feat: ease-out cubic + rect lerp"
```

---

## Task 6: Win32 P/Invoke surface

**Files:**
- Create: `src/DockWindow/Win32.cs`

No tests — this is a P/Invoke surface with no logic worth mocking. It is exercised end-to-end by manual smoke testing in Task 11.

- [ ] **Step 1: Write `Win32.cs`**

Write `src/DockWindow/Win32.cs`:

```csharp
using System.Runtime.InteropServices;

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
    public const uint SWP_NOZORDER               = 0x0004;
    public const uint SWP_NOACTIVATE             = 0x0010;
    public const uint SWP_ASYNCWINDOWPOS         = 0x4000;
    public const uint DWMWA_CLOAKED              = 14;

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

    public static bool MoveWindowAsync(IntPtr hwnd, Rect r) =>
        SetWindowPos(hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

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
```

- [ ] **Step 2: Build to verify compile**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: still fails on missing entry point. The `Win32.cs` itself must produce **no** compile errors. If it does, fix them before proceeding.

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/Win32.cs
git commit -m "feat: Win32 P/Invoke surface"
```

---

## Task 7: WindowEventHook

**Files:**
- Create: `src/DockWindow/WindowEventHook.cs`

- [ ] **Step 1: Write `WindowEventHook.cs`**

Write `src/DockWindow/WindowEventHook.cs`:

```csharp
namespace DockWindow;

public sealed class WindowEventHook : IDisposable
{
    public event Action<IntPtr>? WindowMoved;

    private readonly Win32.WinEventDelegate _delegate; // kept alive
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
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: still fails only on missing entry point.

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/WindowEventHook.cs
git commit -m "feat: SetWinEventHook wrapper"
```

---

## Task 8: MousePoller

**Files:**
- Create: `src/DockWindow/MousePoller.cs`

- [ ] **Step 1: Write `MousePoller.cs`**

Write `src/DockWindow/MousePoller.cs`:

```csharp
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
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: still fails only on missing entry point.

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/MousePoller.cs
git commit -m "feat: cursor-position poller"
```

---

## Task 9: DockedWindow (per-window state machine)

**Files:**
- Create: `src/DockWindow/DockedWindow.cs`

- [ ] **Step 1: Write `DockedWindow.cs`**

Write `src/DockWindow/DockedWindow.cs`:

```csharp
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
        AnimateTo(HiddenRect);
    }

    public void StartPeek()
    {
        State = DockState.Peeking;
        AnimateTo(OriginalRect);
    }

    public void Tick(Point cursor)
    {
        if (!Win32.IsWindow(Hwnd)) { Dead?.Invoke(this); return; }

        // Re-resolve work area each tick — window may have moved monitors.
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
        Win32.MoveWindowAsync(Hwnd, OriginalRect);
        State = DockState.Visible;
    }

    private void AnimateTo(Rect to)
    {
        if (!Win32.TryGetWindowRect(Hwnd, out var from))
        {
            Dead?.Invoke(this);
            return;
        }
        _animFrom  = from;
        _animTo    = to;
        _animStart = DateTime.UtcNow;
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
        Win32.MoveWindowAsync(Hwnd, r);
    }

    public void Dispose()
    {
        _anim.Stop();
        _anim.Dispose();
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: still fails only on missing entry point.

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/DockedWindow.cs
git commit -m "feat: DockedWindow state machine + animation"
```

---

## Task 10: DockManager

**Files:**
- Create: `src/DockWindow/DockManager.cs`

- [ ] **Step 1: Write `DockManager.cs`**

Write `src/DockWindow/DockManager.cs`:

```csharp
using System.Drawing;

namespace DockWindow;

public sealed class DockManager : IDisposable
{
    private readonly Dictionary<IntPtr, DockedWindow> _docked = new();
    private readonly WindowEventHook _hook;
    private readonly MousePoller     _poller;

    public bool Enabled { get; set; } = true;

    public DockManager(WindowEventHook hook, MousePoller poller)
    {
        _hook   = hook;
        _poller = poller;
        _hook.WindowMoved += OnWindowMoved;
        _poller.Moved     += OnMouseMoved;
    }

    private void OnWindowMoved(IntPtr hwnd)
    {
        if (!Enabled) return;

        // If we already managed it, recompute or drop based on new edge.
        var isKnown = _docked.ContainsKey(hwnd);

        if (!Win32.IsDockable(hwnd))
        {
            if (isKnown) Remove(hwnd);
            return;
        }

        if (!Win32.TryGetWindowRect(hwnd, out var rect)) return;
        if (!Win32.TryGetWorkArea(hwnd, out var work))   return;

        var edge = EdgeDetector.Classify(rect, work);

        if (edge == Edge.None)
        {
            // Moved away from any edge → forget.
            if (isKnown) Remove(hwnd);
            return;
        }

        // Ignore spurious MoveSizeEnd events fired by our own SetWindowPos animations.
        // Heuristic: if the window already exists in our registry and its current rect
        // is very close to either its HiddenRect or its OriginalRect, skip.
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
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/DockWindow/DockWindow.csproj
```

Expected: still fails only on missing entry point.

- [ ] **Step 3: Commit**

```powershell
git add src/DockWindow/DockManager.cs
git commit -m "feat: DockManager wires hook + poller to docked windows"
```

---

## Task 11: TrayContext + Program (entry)

**Files:**
- Create: `src/DockWindow/Program.cs`
- Create: `src/DockWindow/TrayContext.cs`

- [ ] **Step 1: Write `Program.cs`**

Write `src/DockWindow/Program.cs`:

```csharp
using System.Windows.Forms;

namespace DockWindow;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}
```

- [ ] **Step 2: Write `TrayContext.cs`**

Write `src/DockWindow/TrayContext.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace DockWindow;

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon         _icon;
    private readonly DockManager        _manager;
    private readonly WindowEventHook    _hook;
    private readonly MousePoller        _poller;
    private readonly ToolStripMenuItem  _enabledItem;

    public TrayContext()
    {
        // Ensure a WinForms-friendly SynchronizationContext exists on this thread.
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        _hook    = new WindowEventHook(SynchronizationContext.Current!);
        _poller  = new MousePoller();
        _manager = new DockManager(_hook, _poller);
        _poller.Start();

        _enabledItem = new ToolStripMenuItem("Enabled")
        {
            Checked      = true,
            CheckOnClick = true,
        };
        _enabledItem.CheckedChanged += (_, _) =>
        {
            _manager.Enabled = _enabledItem.Checked;
            if (!_enabledItem.Checked) _manager.RestoreAll();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add("Restore all", null, (_, _) => _manager.RestoreAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon             = SystemIcons.Application,
            Text             = "DockWindow",
            ContextMenuStrip = menu,
            Visible          = true,
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager.RestoreAll();
            _icon.Visible = false;
            _icon.Dispose();
            _manager.Dispose();
            _poller.Dispose();
            _hook.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

- [ ] **Step 3: Build the whole solution**

```powershell
dotnet build DockWindow.sln
```

Expected: succeeds with 0 warnings, 0 errors.

- [ ] **Step 4: Run the full test suite**

```powershell
dotnet test DockWindow.sln
```

Expected: 24/24 pass.

- [ ] **Step 5: Commit**

```powershell
git add src/DockWindow/Program.cs src/DockWindow/TrayContext.cs
git commit -m "feat: tray icon entry point"
```

---

## Task 12: README + manual smoke test

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write `README.md`**

Write `README.md`:

```markdown
# DockWindow

Auto-hide any Windows window by dragging it to a screen edge.

## Build

```powershell
dotnet build -c Release
```

## Run

```powershell
dotnet run --project src/DockWindow -c Release
```

A tray icon appears. Right-click for **Enabled / Restore all / Quit**.

## Usage

1. Drag a normal window (e.g., Notepad) so its edge touches the left, right, or top of the screen.
2. On mouse-up it slides off-screen, leaving a 4-pixel strip.
3. Mouse the strip → it slides back in.
4. Move the mouse away → it slides out again.
5. Drag a hidden window away from the edge to forget it.

Bottom edge is not supported (taskbar).

## Manual smoke checklist

- [ ] Drag Notepad to **left** edge → hides with strip visible at x=0.
- [ ] Mouse over strip → slides back to docked position.
- [ ] Mouse away → slides out after ~300 ms.
- [ ] Drag Notepad to **right** edge → hides at right.
- [ ] Drag Notepad to **top** edge → hides at top.
- [ ] Drag a docked window away from the edge → no longer managed; stays put.
- [ ] Close a hidden window via Task Manager → DockWindow does not crash.
- [ ] Multi-monitor: move docked window between monitors → re-hides on new monitor's edge.
- [ ] Right-click tray → uncheck **Enabled** → all docked windows restored.
- [ ] Right-click tray → **Quit** → all windows restored, process exits cleanly.

## Tests

```powershell
dotnet test
```
```

- [ ] **Step 2: Run the app for real (manual smoke test)**

```powershell
dotnet run --project src/DockWindow -c Release
```

Walk through every item in the smoke checklist. Tick them off as they pass. If any fail, file a bug, fix, add a regression test where possible.

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: README with usage + smoke checklist"
```

---

## Self-Review Notes

**Spec coverage:**

- Drag-to-edge auto-dock → Tasks 7 (hook), 10 (manager logic), 3 (classify).
- 4-px strip visible → Task 4 (HiddenRect math) + EdgeDetector constants.
- Mouse-over peek strip → Task 8 (poller) + Task 9 (Tick state machine).
- 300 ms hide delay → Task 9.
- 150 ms ease-out animation → Task 5 (math) + Task 9 (timer).
- Multi-monitor → Task 9 `Tick` re-resolves work area; Task 6 `TryGetWorkArea`.
- DPI PerMonitorV2 → Task 1 manifest.
- Window filter → Task 6 `IsDockable`.
- Tray UI (Enabled / Restore all / Quit) → Task 11.
- Drag-away forgets → Task 10 `OnWindowMoved` Edge.None branch.
- Closed-window safety → Task 9 `IsWindow` checks; `Dead` event drops from registry.
- Re-entry guard against our own SetWindowPos triggering MoveSizeEnd → Task 10 `NearlyEqual` heuristic.

**Placeholder scan:** none.

**Type consistency:** `Rect`, `Edge`, `DockState`, `Animation.Lerp(Rect, Rect, double)`, `EdgeDetector.{Classify, SnapFlush, HiddenRect, PeekStripRect}` are used consistently across tasks 3–11. `WindowEventHook.WindowMoved` and `MousePoller.Moved` are the only two events `DockManager` subscribes to, and both names match.

**Ambiguity check:** corner precedence is pinned by test in Task 3 (Left > Right > Top). Strip math is pinned by explicit numeric expectations in Task 4.
