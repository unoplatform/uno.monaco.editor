# fn-16-fixspike-add-stathread-and.1 Convert spike Program.cs to STAThread Main with SynchronizationContext

## Description
Convert the spike's `Program.cs` from top-level statements to an explicit `[STAThread] static void Main()` and install a custom `PostMessage`-based `SynchronizationContext` on the UI thread so async continuations from `FireAndForget` marshal back to STA.

**Size:** M
**Files:** `spike/WebView2FlickerSpike/Program.cs`, `spike/WebView2FlickerSpike/MainWindow.cs`, potentially `spike/WebView2FlickerSpike/Win32SynchronizationContext.cs` (new)

## Approach

### Program.cs conversion
- Follow the pattern at `MonacoEditorTestApp/Platforms/Desktop/Program.cs` for `[STAThread]` declaration
- Replace top-level statements with `class Program { [STAThread] static void Main() { ... } }`
- Must use synchronous `void Main()` — `async Task Main()` defeats `[STAThread]` (dotnet/runtime#73099)
- Add startup verification: `Console.WriteLine` logging apartment state (`Thread.CurrentThread.GetApartmentState()`) and `SynchronizationContext.Current` at startup

### Custom PostMessage-based SynchronizationContext
- **Do NOT use `WindowsFormsSynchronizationContext`** — it relies on `Control.BeginInvoke` which requires `Application.Run()` and WinForms message infrastructure. It will silently fail (callbacks never execute) with a raw Win32 message loop.
- Implement a custom `SynchronizationContext` (~30-50 lines) that:
  - Registers a custom `WM_APP+N` message for callback dispatch
  - `Post()`: enqueues callback to a `ConcurrentQueue<(SendOrPostCallback, object?)>` and calls Win32 `PostMessage` with the custom message ID to the main HWND
  - `Send()`: if on UI thread, invoke directly; otherwise marshal via `SendMessage`
  - `CreateCopy()`: returns new instance pointing to same HWND and queue
- Can be a nested class in `Program.cs` or a separate `Win32SynchronizationContext.cs` file in the spike directory
- Install via `SynchronizationContext.SetSynchronizationContext(...)` before `window.Create()` or before the message loop
- **Integration point in MainWindow.cs**: Add a `WM_APP+N` case in `WndProc` that drains the callback queue and invokes each callback. The sync context must provide a method (e.g. `DrainCallbacks()`) that MainWindow calls.

### Disposal
- Convert `await window.DisposeAsync()` to `window.DisposeAsync().AsTask().GetAwaiter().GetResult()`
- This runs **after** `RunMessageLoop()` returns (message loop exited via `PostQuitMessage`)
- Safe because no further message pumping is needed and no continuations need to marshal back
- No `.csproj` changes required — the custom sync context uses only Win32 P/Invoke already present in the spike

## Key context

- `MainWindow.FireAndForget` in `MainWindow.cs` dispatches async WebView2 init — these continuations are the secondary threading concern after the STA fix
- The spike's Win32 message loop is a raw `GetMessage`/`DispatchMessage` loop — no WinForms `Application.Run` — so no built-in `SynchronizationContext` is installed
- DComp COM interfaces (`DCompInterop.cs`) are all STA-affine via `[ComImport]`

## Acceptance
- [ ] `Program.cs` has explicit `[STAThread] static void Main()` (no top-level statements)
- [ ] Spike runs without `RPC_E_CHANGED_MODE` COM exception
- [ ] Custom `PostMessage`-based `SynchronizationContext` installed before message loop
- [ ] `SynchronizationContext.Current` is non-null on the UI thread before WebView2 init (verified by startup console output)
- [ ] Async continuations from `FireAndForget` execute on the STA thread (verified by startup console output logging apartment state)
- [ ] Window cleanup/disposal runs correctly via `.GetAwaiter().GetResult()` after message loop exit
- [ ] No changes outside `spike/WebView2FlickerSpike/`
- [ ] No `.csproj` changes required

## Done summary
Converted spike Program.cs from top-level statements to explicit [STAThread] static void Main() and added a custom PostMessage-based Win32SynchronizationContext that marshals async continuations to the STA UI thread via WM_APP+0 message dispatch in WndProc.
## Evidence
- Commits: d04b1a13bd5b0a2f2f15b8890c600ba3c81ff865
- Tests: dotnet build spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj
- PRs: