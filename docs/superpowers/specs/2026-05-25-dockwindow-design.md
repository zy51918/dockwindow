# DockWindow — 设计文档

日期：2026-05-25

## 目标

一个 Windows 托盘工具：用户把任意窗口拖到屏幕边缘后，窗口会自动滑出屏幕，
只留下一条很窄的可见条。当鼠标移到这条可见条上时，窗口滑回到停靠位置；
鼠标离开后，窗口再次滑出隐藏。

灵感来自经典的"任务栏自动隐藏"行为，把它推广到任意窗口上。

## 用户体验

1. 用户拖动任意普通窗口，使其触碰（或越过）屏幕的 **左边缘**、**右边缘** 或 **顶部边缘**。
2. 松开鼠标后，窗口以动画形式滑出屏幕，只留下 4 像素宽的可见条。
3. 鼠标移入该可见条时，窗口以动画形式滑回原停靠矩形。
4. 鼠标离开可见的窗口超过 300 毫秒后，窗口滑回隐藏状态。
5. 右键托盘图标提供菜单：**Enabled**（启用开关）、**Restore all**（全部还原）、**Quit**（退出）。
6. 如果用户把一个已停靠的窗口拖离边缘，应用会"忘记"它，让它停留在用户拖到的位置；
   并清除应用先前设置的置顶标志。

v1 故意 **不** 支持底部边缘（避免与任务栏冲突）。

## v1 的非目标

- 不持久化状态。应用重启 = 干净的初始状态。
- 没有按窗口配置 / 排除名单 UI，过滤规则写死在代码里。
- 不支持底部边缘。
- 没有快捷键。
- 没有安装程序，直接提供单个 `.exe`。

## 技术栈

- **.NET 8**、C# 12、`net8.0-windows`、WinForms（用于 `NotifyIcon` + 消息循环）。
- 单文件发布（`dotnet publish -r win-x64 -c Release --self-contained false`）
  生成单个 `.exe`。
- DPI 感知：在 `app.manifest` 中声明 `PerMonitorV2`。
- 所有 Win32 调用通过 P/Invoke，没有第三方原生依赖。
- 测试：使用 xUnit 覆盖纯几何 / 纯数学单元。

## 架构

### 单元划分

| 单元 | 职责 | 依赖 |
|---|---|---|
| `TrayContext` | `Main()` 入口、NotifyIcon、生命周期、启用开关 | `DockManager` |
| `WindowEventHook` | 封装 `SetWinEventHook(EVENT_SYSTEM_MOVESIZEEND)`。在 UI 线程上抛出 `WindowMoved(IntPtr hwnd)` 事件。 | `Win32` |
| `EdgeDetector` | **纯函数。** 给定窗口矩形 + 显示器工作区，返回 `Edge` 以及目标矩形（隐藏 / 探出）。 | — |
| `DockedWindow` | 管理一个 HWND，拥有自己的滑动动画。方法：`StartHide()`、`StartPeek()`、`Tick(Point cursor)`。 | `Win32`、`EdgeDetector` |
| `DockManager` | `DockedWindow` 的注册表。处理 MoveSizeEnd + 鼠标轮询事件。 | `WindowEventHook`、`MousePoller`、`EdgeDetector` |
| `MousePoller` | 50 ms 的 `WinForms.Timer`，发出 `Moved(Point)`。全应用单例。 | `Win32` |
| `Win32` | P/Invoke 表面，不放业务逻辑。 | — |

### 数据流

```
WindowEventHook --MoveSizeEnd(hwnd)--> DockManager
                                         |
                                         v
                                  EdgeDetector.Classify(rect, workArea)
                                         |
                                         v
                              创建 / 更新 / 移除 DockedWindow

MousePoller --Tick(Point)--> DockManager --> 对每个 DockedWindow 调用 w.Tick(point)
                                                                       |
                                                                       v
                                                          隐藏 / 探出 / 无操作
```

### DockedWindow 的状态

```
enum DockState { Hidden, Peeking, Visible }
```

- `Hidden`：窗口已滑出屏幕，留 4 px 可见。停靠后的默认状态。
- `Peeking`：鼠标进入了可见条，窗口正在朝 `OriginalRect` 滑回或已经到位。
- `Visible`：鼠标已经进入 `OriginalRect`，离开延迟计时器尚未到期。

状态转移：

- `Hidden → Peeking`：鼠标进入探出条。
- `Peeking → Visible`：鼠标到达 `OriginalRect`（完整位置）。
- `Visible → Hidden`：鼠标离开 `OriginalRect` 持续 300 ms 后。
- 任意状态 → 被移除：当 `WindowEventHook` 报告窗口已被拖离任何边缘（`Edge.None`），
  或者 `Win32.IsWindow(hwnd)` 在某次 Tick 时返回 false。

### 几何运算（EdgeDetector — 有单元测试）

给定物理矩形 `R` 和显示器工作区 `W`：

- **吸附阈值** = 8 px。
- 当 `R.Left   <= W.Left   + 8`  时为 `Edge.Left`
- 当 `R.Right  >= W.Right  - 8`  时为 `Edge.Right`
- 当 `R.Top    <= W.Top    + 8`  时为 `Edge.Top`
- 否则为 `Edge.None`。

如果同时满足两条边缘（角落），按 **Left > Right > Top** 优先级取一个（确定性，单元测试依赖此规则）。
停靠矩形的宽高被保留。

**隐藏矩形**（保留 `STRIP = 4` 像素可见）：
- 左：  `X = W.Left - (Width - STRIP)`，Y / H / W 不变。
- 右：  `X = W.Right - STRIP`，Y / H / W 不变。
- 顶：  `Y = W.Top  - (Height - STRIP)`，X / H / W 不变。

**探出条矩形**（hover 触发区）：
- 左：  `(W.Left, R.Top, STRIP, R.Height)`
- 右：  `(W.Right - STRIP, R.Top, STRIP, R.Height)`
- 顶：  `(R.Left, W.Top, R.Width, STRIP)`

### 动画

每个 `DockedWindow` 拥有一个 `WinForms.Timer`，周期 15 ms（约 66 fps）。
缓动函数：ease-out cubic，时长 150 ms。
每次 tick 计算插值矩形并调用
`SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS)`。
当 t >= 1 时停止计时器。

### 失焦时仍能 hover 唤回 —— Z 序处理

如果只使用 `HWND_TOP`，会被 Windows 的前台锁定规则阻止：MSDN 明确指出
"要使用 SetWindowPos 把窗口置顶，调用方进程必须具备 SetForegroundWindow 权限。"
作为后台托盘进程，我们没有这个权限，调用会静默无效，于是窗口虽然在几何上探出了，
视觉上仍然被前台窗口压在下面。

我们改用 **`HWND_TOPMOST`**：它属于另一组 z-order 层级，不受同一前台锁定规则限制，
后台进程可以可靠地把其它进程的窗口提升到所有非 topmost 窗口之上，同时通过 `SWP_NOACTIVATE`
不抢占焦点。

应用规则：

- `StartPeek` 之前一次性调用 `Win32.SetTopmost(hwnd, true)`，动画过程只移动位置（`SWP_NOZORDER`）。
- `StartHide` 之前一次性调用 `Win32.SetTopmost(hwnd, false)`，确保隐藏后的窗口不再被强制置顶。
- `RestoreImmediate`（托盘 "Restore all" 或 Quit 时）和 `DockedWindow.Dispose()`
  （用户把窗口拖离边缘时）都会清除 topmost，避免拖走后还被钉在最前面。

### 窗口过滤（哪些窗口可以被停靠）

窗口必须 **同时** 满足：

- `IsWindowVisible(hwnd)`
- `GetWindow(hwnd, GW_OWNER)` 返回 `IntPtr.Zero`（顶层、无 owner）
- 样式包含 `WS_CAPTION`（有标题栏 —— 排除大多数 tool 窗口）
- 样式 **不包含** `WS_EX_TOOLWINDOW`
- 未被 DWM cloak（`DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, ...)` 返回 0）
- 不是 shell 窗口（`GetShellWindow()`）

### 多显示器

`MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)` → `GetMonitorInfo` →
使用 `rcWork`（已排除任务栏）来做边缘判定和隐藏矩形计算。
每次 `Tick` 时重新解析显示器，以应对用户在窗口探出期间把它拖到了另一台显示器。

## 错误处理

- 所有 P/Invoke 返回值都被检查；不产生 UI 噪音，只通过 `Trace.WriteLine` 记录。
- HWND 有效性：每次 `Tick` 和每次动画步进都先检查 `IsWindow(hwnd)`；
  返回 false 则把 `DockedWindow` 从注册表中移除。
- WinEvent hook 的回调在某些 shell 状态下可能不在 UI 线程触发；
  我们通过 `SynchronizationContext.Post` 转回 UI 线程后再变更状态。
- 托盘"Enabled"关闭：触发 `RestoreAll` 语义——把所有受管窗口滑回 `OriginalRect`，
  清除 topmost，然后清空注册表。重新开启 Enabled 不会回头去重新停靠之前的窗口。
- `DockedWindow.Dispose()` 中带 `IsWindow` 守卫地调用 `SetTopmost(false)`，
  确保所有释放路径（被拖离边缘、窗口被关闭、应用退出）都不会把目标窗口
  遗留在 topmost 状态。

## 测试

**xUnit 项目 `DockWindow.Tests`：**

- `EdgeDetectorTests`
  - 每条边的阈值边界正确分类（7 px → 吸附，9 px → 不吸附）。
  - 角落优先级 Left > Right > Top。
  - 每条边的隐藏矩形保留正确的 STRIP 像素。
  - 每条边的探出条矩形正确。
- `AnimationTests`
  - Ease-out cubic 单调递增；在 t = 1 时恰好到达终点。
  - 插值矩形：t = 0 等于起点，t = 1 等于终点。

手动冒烟清单（在 README 中）：

1. 把记事本拖到左边缘 → 隐藏，剩可见条。
2. 鼠标移到可见条上 → 滑回原位。
3. 鼠标离开 → 300 ms 后滑回隐藏。
4. 把停靠窗口拖离边缘 → 不再受管，停留原地。
5. 探出期间把窗口拖到另一台显示器 → 滑回该显示器的边缘隐藏。
6. 通过任务管理器关闭已隐藏的窗口 → 应用不崩溃。
7. 托盘"Enabled"关闭 → 所有窗口被还原，topmost 被清除。

## 工程布局

```
dockwindow/
  DockWindow.sln
  src/
    DockWindow/
      DockWindow.csproj          (net8.0-windows, WinExe)
      Program.cs                 (入口)
      TrayContext.cs             (ApplicationContext + NotifyIcon)
      DockManager.cs
      DockedWindow.cs
      WindowEventHook.cs
      MousePoller.cs
      EdgeDetector.cs            (纯函数，不依赖 Win32)
      Animation.cs               (纯数学：缓动 + 插值)
      Win32.cs                   (P/Invoke)
      Geometry.cs                (Rect、Edge)
      app.manifest               (PerMonitorV2)
      Resources/
        tray.ico                 (托盘图标，多尺寸 PNG 嵌入 ICO)
  tests/
    DockWindow.Tests/
      DockWindow.Tests.csproj    (xUnit, net8.0-windows)
      EdgeDetectorTests.cs
      AnimationTests.cs
  tools/
    IconGen/                     (离线工具：重新生成 tray.ico)
      IconGen.csproj
      Program.cs
  docs/
    superpowers/
      specs/2026-05-25-dockwindow-design.md
      plans/2026-05-25-dockwindow.md
    tray-preview.png             (icon 设计预览)
  README.md
```
