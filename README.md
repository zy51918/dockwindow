# DockWindow

Auto-hide any Windows window by dragging it to a screen edge. A 4-pixel strip
stays visible; mouse it to peek the window back in, mouse away to hide it again.

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

1. Drag a normal window (e.g. Notepad) so its edge touches the **left**, **right**, or **top** of the screen.
2. On mouse-up it slides off-screen, leaving a 4-pixel strip.
3. Mouse over the strip → it slides back in.
4. Move the mouse away → it slides out again after ~300 ms.
5. Drag a hidden window away from the edge to forget it.

Bottom edge is not supported (taskbar).

## Manual smoke checklist

- [ ] Drag Notepad to **left** edge → hides with strip visible.
- [ ] Mouse over strip → slides back to docked position.
- [ ] Mouse away → slides out after ~300 ms.
- [ ] Drag to **right** edge → hides at right.
- [ ] Drag to **top** edge → hides at top.
- [ ] Drag a docked window away from the edge → no longer managed; stays put.
- [ ] Close a hidden window via Task Manager → DockWindow does not crash.
- [ ] Multi-monitor: move docked window between monitors → re-hides on new monitor's edge.
- [ ] Right-click tray → uncheck **Enabled** → all docked windows restored.
- [ ] Right-click tray → **Quit** → all windows restored, process exits cleanly.

## Tests

```powershell
dotnet test
```

## Design

See [docs/superpowers/specs/2026-05-25-dockwindow-design.md](docs/superpowers/specs/2026-05-25-dockwindow-design.md)
and [docs/superpowers/plans/2026-05-25-dockwindow.md](docs/superpowers/plans/2026-05-25-dockwindow.md).
