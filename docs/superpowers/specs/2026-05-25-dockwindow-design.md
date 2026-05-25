# DockWindow — Design

Date: 2026-05-25

## Goal

A Windows tray utility that auto-hides any window the user drags to a screen edge,
leaving a thin visible strip. Hovering the strip slides the window back to its
docked position; moving the mouse away slides it back out of view.

Inspired by classic "auto-hide taskbar" behavior, generalized to arbitrary windows.

## User Experience

1. User drags any normal window so it touches (or overlaps) the **left**, **right**,
   or **top** edge of the screen.
2. On mouse-up, the window animates off-screen, leaving a 4-pixel strip visible.
3. When the mouse moves into that strip, the window animates back to its original
   docked rect.
4. When the mouse leaves the visible window for more than 300 ms, the window slides
   back into hiding.
5. Right-clicking the tray icon offers: **Enabled** (toggle), **Restore all**, **Quit**.
6. If the user drags a hidden window away from the edge, it is forgotten and stays
   where they put it.

Bottom edge is intentionally **not** supported in v1 (taskbar conflict).

## Non-goals (v1)

- No persistence across reboots. App relaunch = clean slate.
- No per-window settings / exclusions UI. Filter is built in.
- No bottom edge.
- No hotkeys.
- No installer. Ship the single-file `.exe`.

## Tech Stack

- **.NET 8**, C# 12, `net8.0-windows`, WinForms (for `NotifyIcon` + message loop).
- Single-file publish (`dotnet publish -r win-x64 -c Release --self-contained false`)
  produces one `.exe`.
- DPI awareness: `PerMonitorV2` declared in `app.manifest`.
- All Win32 calls via P/Invoke. No third-party native deps.
- Tests: xUnit on the pure-geometry units.

## Architecture

### Units

| Unit | Responsibility | Depends on |
|---|---|---|
| `TrayApp` | `Main()`, NotifyIcon, lifecycle, toggle enabled state | `DockManager` |
| `WindowEventHook` | Wraps `SetWinEventHook(EVENT_SYSTEM_MOVESIZEEND)`. Raises `WindowMoved(IntPtr hwnd)` on the UI thread. | `Win32` |
| `EdgeDetector` | **Pure.** Given a window rect + monitor work area, returns `Edge` and target rects (hidden / peeking). | — |
| `DockedWindow` | One managed HWND. Owns its slide animation. Methods: `Hide()`, `Peek()`, `Tick(Point cursor)`. | `Win32`, `EdgeDetector` |
| `DockManager` | Registry of `DockedWindow`s. Handles MoveSizeEnd + mouse-tick events. | `WindowEventHook`, `MousePoller`, `EdgeDetector` |
| `MousePoller` | 50 ms `WinForms.Timer`. Fires `MouseMoved(Point)`. Single instance app-wide. | `Win32` |
| `Win32` | P/Invoke surface. No logic. | — |

### Data flow

```
WindowEventHook --MoveSizeEnd(hwnd)--> DockManager
                                         |
                                         v
                                  EdgeDetector.Classify(rect, workArea)
                                         |
                                         v
                              create / update / drop DockedWindow

MousePoller --Tick(Point)--> DockManager --> for each DockedWindow: w.Tick(point)
                                                                       |
                                                                       v
                                                                Hide / Peek / no-op
```

### State per DockedWindow

```
enum State { Hidden, Peeking, Visible }
```

- `Hidden`: window has been slid off-screen, 4 px showing. Default after docking.
- `Peeking`: cursor entered the strip; animation toward `OriginalRect` in progress
  or complete.
- `Visible`: cursor inside `OriginalRect`; mouse-leave timer not yet fired.

Transitions:

- `Hidden → Peeking` when cursor enters the peek strip.
- `Peeking → Visible` when cursor reaches the original (full) rect.
- `Visible → Hidden` after cursor has been outside `OriginalRect` for 300 ms.
- Any state → forgotten when `WindowEventHook` reports the window has moved
  to `Edge.None`, or when `Win32.IsWindow(hwnd)` returns false on tick.

### Geometry (EdgeDetector — unit-tested)

Given physical rect `R` and monitor work area `W`:

- **Snap threshold** = 8 px.
- `Edge.Left`  if `R.Left   <= W.Left   + 8`
- `Edge.Right` if `R.Right  >= W.Right  - 8`
- `Edge.Top`   if `R.Top    <= W.Top    + 8`
- Else `Edge.None`.

If two edges qualify (corner), prefer **Left > Right > Top** (deterministic, matters
for unit tests). Width/height of the docked rect is preserved.

**Hidden rect** (4-px strip remains, called `STRIP = 4`):
- Left:  `X = W.Left - (Width - STRIP)`, same Y/H/W.
- Right: `X = W.Right - STRIP`, same Y/H/W.
- Top:   `Y = W.Top  - (Height - STRIP)`, same X/H/W.

**Peek strip rect** (hover target):
- Left:  `(W.Left, R.Top, STRIP, R.Height)`
- Right: `(W.Right - STRIP, R.Top, STRIP, R.Height)`
- Top:   `(R.Left, W.Top, R.Width, STRIP)`

### Animation

Per-`DockedWindow` `WinForms.Timer`, interval 15 ms (~66 fps).
Easing: ease-out cubic, duration 150 ms.
Each tick computes lerped rect and calls
`SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS)`.
Timer stops when t >= 1.

### Window filter (which windows are eligible to dock)

A window is dockable iff **all** of:

- `IsWindowVisible(hwnd)`
- `GetWindow(hwnd, GW_OWNER)` returns `IntPtr.Zero` (top-level, no owner)
- Style includes `WS_CAPTION` (has a title bar — excludes most tool windows)
- Style does **not** include `WS_EX_TOOLWINDOW`
- Not cloaked (`DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, ...)` returns 0)
- Not the shell window (`GetShellWindow()`)
- Not the foreground window of our own process

### Multi-monitor

`MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)` → `GetMonitorInfo` → use
`rcWork` (excludes taskbar) for both the edge detection and the hidden-rect math.
On each `Tick`, re-resolve the monitor in case the user moved the window across
monitors while it was visible.

## Error handling

- All P/Invoke return values checked; logging via `Trace.WriteLine` only (no UI noise).
- HWND validity: every `Tick` and every animation step starts with `IsWindow(hwnd)`;
  on false, the `DockedWindow` is removed from the registry.
- WinEvent hook callback can fire on a non-UI thread under some shell conditions;
  it is marshaled to the UI thread via `SynchronizationContext.Post` before any
  state mutation.
- App tray-toggle "Disabled" calls `Restore all` semantics: slide every managed
  window back to its `OriginalRect`, then clear the registry. Re-enabling does
  not re-dock retroactively.

## Testing

**xUnit project `DockWindow.Tests`:**

- `EdgeDetectorTests`
  - Classifies each edge correctly at threshold boundary (7 px → snap, 9 px → none).
  - Corner precedence Left > Right > Top.
  - Computes hidden rect with STRIP px remaining for each edge.
  - Computes peek strip rect for each edge.
- `AnimationMathTests`
  - Ease-out cubic monotonic; reaches end exactly at t = 1.
  - Lerped rect at t = 0 equals start; at t = 1 equals end.

Manual smoke checklist (in README):

1. Drag Notepad to left edge → hides with strip visible.
2. Mouse over strip → slides back.
3. Mouse away → slides out after 300 ms.
4. Drag away from edge → forgotten, stays put.
5. Move docked window to another monitor while peeked → hides to that monitor's edge.
6. Close hidden window → no crash, entry removed silently.
7. Toggle tray "Enabled" off → all windows restored.

## Project layout

```
dockwindow/
  DockWindow.sln
  src/
    DockWindow/
      DockWindow.csproj          (net8.0-windows, WinExe, single-file)
      Program.cs                 (TrayApp entry)
      TrayApp.cs
      DockManager.cs
      DockedWindow.cs
      WindowEventHook.cs
      MousePoller.cs
      EdgeDetector.cs            (pure, no Win32)
      Animation.cs               (pure math: easing, lerp)
      Win32.cs                   (P/Invoke)
      app.manifest               (PerMonitorV2)
      Resources/
        tray.ico
  tests/
    DockWindow.Tests/
      DockWindow.Tests.csproj    (xUnit, net8.0)
      EdgeDetectorTests.cs
      AnimationMathTests.cs
  docs/superpowers/specs/
    2026-05-25-dockwindow-design.md
  README.md
```
