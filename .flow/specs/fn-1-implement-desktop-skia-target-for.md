# Implement Desktop (Skia) Target for Monaco Editor

## Problem

The MonacoEditorComponent currently only works on WASM via `BrowserHtmlElement` and JSImport/JSExport. The MonacoEditorTestApp builds and runs on desktop (Skia) but renders nothing because all presenter/bridge code is guarded behind `#if __WASM__` directives. Desktop (Skia) support is urgently needed across all platforms (Windows, macOS, Linux).

## Background

- Originally a UWP-based project, ported to Uno WASM — UWP code was trimmed during port
- Architecture is already abstracted via `ICodeEditorPresenter` interface
- Platform partials pattern established (`*.wasm.cs` files)
- WASM version is fully functional with all features
- Desktop target configured in test app (net10.0-desktop with SkiaRenderer)
- Desktop debugging/launch already works — just no Monaco rendering

## Key Decisions

- **All desktop platforms**: Windows (Win32), macOS, Linux (X11/FrameBuffer) via Skia
- **Full feature parity with WASM**: All language services (CodeLens, Hover, Completion, Color providers), themes, decorations, markers, commands — everything
- **Production quality from the start**: Proper architecture, error handling, tests
- **Same NuGet package**: Desktop support added to existing `Uno.Monaco.Editor` package
- **Keep generic TFMs**: Library stays net9.0/net10.0 — use runtime detection for platform differences
- **Local resources only**: No CDN fallback — Monaco JS/CSS always embedded/local
- **Auto-resolve Source URI**: Presenter determines correct resource URI per platform automatically
- **Expose rendering backend**: Add property so consumers can detect which backend is active
- **CodeEditor only**: DiffEditor support deferred to future work
- **Multiple instances required**: TabView with multiple editors must work (each with own WebView)
- **Performance**: Must feel native — typing lag < 16ms, smooth editing experience
- **Prefer System.Text.Json**: Migrate from Newtonsoft.Json where possible (applies to new + existing code)
- **Monaco loader (Loader.js)**: AMD loader for Monaco — will need adaptation for desktop WebView resource loading
- **Drag-and-drop**: Deferred to later
- **Accessibility**: Monaco's built-in a11y features expected to work via WebView — no extra work
- **Uno version**: Can upgrade Uno.WinUI if newer versions have better desktop WebView support
- **Use Uno CommunityToolkit**: Leverage toolkit components where useful
- **Follow both existing code patterns AND Uno Platform guidelines**

## Research Areas (Must Be Resolved Before/During Implementation)

These areas were identified during interview as needing investigation:

1. **WebView approach for Skia desktop**: What does Uno provide? Built-in WebView2 on Skia? Platform-native alternatives?
2. **JS interop mechanism**: How does C#<->JS communication work in Uno's desktop WebView? Message passing? ExecuteScriptAsync?
3. **Resource serving strategy**: How to serve Monaco HTML/JS/CSS to desktop WebView? File URI? Virtual host? Local HTTP server?
4. **Platform file naming convention**: What convention does Uno use for Skia-targeted partials? `*.skia.cs`? `*.desktop.cs`?
5. **Bridge design (ParentAccessor)**: Can the reflection-based approach be replicated, or must it be redesigned for desktop?
6. **JS compatibility**: Will existing `uno-monaco-helpers.js` (using `Module.getAssemblyExports()`) work in desktop WebView, or need refactoring?
7. **JS helper strategy**: Single abstracted JS file for both platforms, or separate WASM/desktop JS files?
8. **Desktop-specific events**: Are additional events needed beyond the existing set?
9. **Keyboard handling**: How does Uno's desktop WebView handle keyboard focus and event routing?
10. **XAML template approach**: Should WebView be in Generic.xaml or created programmatically by presenter?
11. **Theme synchronization**: How to detect and sync system theme on Skia desktop?
12. **Error handling**: Best practices for WebView load failures on desktop
13. **Threading model**: Uno's desktop threading model for WebView interactions (STA thread concerns?)
14. **Uno's desktop WebView capabilities and limitations**: Overall assessment of what's possible
15. **UWP git history**: Quick check for useful patterns from removed UWP WebView implementation
16. **Full ifdef audit**: Catalog every `#if __WASM__` and platform-specific code path in the codebase

## Edge Cases

- WebView2 runtime not installed on Windows
- Multiple editor instances sharing resources but needing independent state
- Theme changes mid-session (system dark/light mode toggle)
- Large files — performance implications in WebView vs native
- Editor resize/layout changes (window resize, panel splits)
- Focus management between Uno controls and WebView content
- Clipboard operations crossing WebView boundary
- High DPI / multi-monitor scaling differences
- Linux without X11 (framebuffer-only environments)
- macOS sandboxing restrictions on WebView resource access
- Simultaneous keyboard input in multiple editor tabs

## Dependencies (Separate Epics)

- **Monaco version upgrade**: Current embedded Monaco should be upgraded to latest — separate epic
- **Monaco typings regeneration**: GenerateMonacoTypings tool is outdated, cannot handle modern TS syntax — needs AI skill approach — separate epic
- **Documentation updates**: README and API docs for desktop support — separate task/epic

## Open Questions

- What specific Uno.WinUI version provides best Skia WebView support?
- Are there Uno community examples of WebView-heavy controls on Skia desktop?
- Should the research phase produce a written spike document or go straight to implementation?
- How to handle Uno's MCP tools for UI test automation on desktop?

## Acceptance

- [ ] Monaco editor renders and is fully functional on Windows desktop (Skia)
- [ ] Monaco editor renders and is fully functional on macOS desktop (Skia)
- [ ] Monaco editor renders and is fully functional on Linux desktop (Skia/X11)
- [ ] All language services work on desktop: CodeLens, Hover, Completion, Color providers
- [ ] Theme switching works (System/Light/Dark) on all desktop platforms
- [ ] All EditorControl.xaml test scenarios pass on desktop
- [ ] Multiple editor instances work in TabView
- [ ] Keyboard events work correctly (typing, shortcuts, commands)
- [ ] Performance feels native — no perceptible input lag
- [ ] WASM functionality is not regressed
- [ ] RenderingBackend property exposed on CodeEditor control
- [ ] CI/CD updated with desktop build and test jobs
- [ ] Unit tests for presenter and bridge logic
- [ ] UI test automation investigated and implemented where possible
- [ ] Code follows both existing patterns and Uno Platform guidelines
