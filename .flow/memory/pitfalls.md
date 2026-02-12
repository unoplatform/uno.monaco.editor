# Pitfalls

Lessons learned from NEEDS_WORK feedback. Things models tend to miss.

<!-- Entries added automatically by hooks or manually via `flowctl memory add` -->

## 2026-02-11 manual [pitfall]
DispatcherQueue.TryEnqueue returns bool; ignoring it can leave TaskCompletionSource permanently incomplete, causing infinite awaits

## 2026-02-11 manual [pitfall]
When adding idempotency guards to init methods, ensure teardown cleans up based on actual field state (not just guard flags), and include rollback in catch blocks to handle partial initialization failures

## 2026-02-11 manual [pitfall]
When extracting interfaces from public concrete types with public delegates, the new interface must match the accessibility of the delegate parameter type (public delegate needs public interface)

## 2026-02-11 manual [pitfall]
WebView2 WebMessageReceived: use WebMessageAsJson (not TryGetWebMessageAsString) to handle both string and JSON object payloads from JS postMessage

## 2026-02-11 manual [pitfall]
URL allowlisting must use parsed URI exact host match (not string.Contains) to prevent subdomain and query-string bypass attacks

## 2026-02-11 manual [pitfall]
file:// URIs on macOS/Linux have empty host -- navigation allowlists must split validation by scheme (https checks host+port, file checks path prefix or allows all local)

## 2026-02-11 manual [pitfall]
Set idempotency guards (_isInitialized) AFTER all setup steps complete, not before — otherwise failure leaves permanently half-initialized state

## 2026-02-11 manual [pitfall]
WinRT event args types (WebViewNavigationCompletedEventArgs, WebViewNewWindowRequestedEventArgs) cannot be constructed - use portable wrapper types in cross-platform interfaces

## 2026-02-11 manual [pitfall]
WebView2 Source must not be set before EnsureCoreWebView2Async completes -- buffer the URI and apply after security settings/handlers are attached to prevent allowlist bypass

## 2026-02-11 manual [pitfall]
Window.Current.SizeChanged subscriptions must be unsubscribed in Unloaded handlers -- missing unsubscribe causes handler accumulation and control lifetime leaks across load/unload cycles

## 2026-02-11 manual [pitfall]
async void event handlers calling methods that re-throw need try/catch -- unhandled exceptions in async void crash the UI thread instead of propagating to callers

## 2026-02-11 manual [pitfall]
When init-time code calls helpers gated by an _initialized flag, that flag must be set BEFORE the calls -- otherwise init-time setup silently no-ops

## 2026-02-11 manual [pitfall]
In templated controls, child element event handlers must only be detached when the child is replaced (OnApplyTemplate), not on Unloaded -- children survive unload/load cycles and handler detach without reattach leaves the control non-functional

## 2026-02-11 manual [pitfall]
When keeping event handlers attached across unload/load cycles, add IsLoaded guards in handlers to prevent late callbacks from re-initializing a control that is unloaded

## 2026-02-11 manual [pitfall]
When a method computes a transformed value into a local variable but then writes the original parameter, this silently persists untransformed data -- always verify the correct variable is used in the final assignment

## 2026-02-11 manual [pitfall]
WebView2 on Windows delivers host-to-page messages via chrome.webview 'message' events, NOT window 'message'. Must subscribe to chrome.webview.addEventListener when available.

## 2026-02-11 manual [pitfall]
When multiple init methods can create the same resource (e.g., JsonRpc), ensure single-owner creation -- having two call sites that both call SetupX() will create orphaned instances without disposal

## 2026-02-11 manual [pitfall]
When JSON-RPC notifications are emitted during initialization, gate them on transport readiness -- early lifecycle events can fire before the underlying transport (WebView2/CoreWebView2) is initialized, causing faulted tasks

## 2026-02-11 manual [pitfall]
Use Channel.CreateBounded (not Unbounded) for inbound message queues from untrusted sources to prevent DoS via memory exhaustion

## 2026-02-11 manual [pitfall]
Playwright NuGet build/buildTransitive targets conflict with UseArtifactsOutput+OutputType=Exe on macOS/Linux; exclude those assets and install browsers from NuGet cache path instead

## 2026-02-11 manual [pitfall]
When starting external processes with fallback candidates, verify the process survives briefly (check HasExited after short delay) before returning -- commands like 'dotnet serve' can start then exit immediately if a tool is not installed

## 2026-02-11 manual [pitfall]
When polling for external service readiness (HTTP server, CDP endpoint), always check whether the backing process has died between polls -- otherwise timeout gives a generic error instead of a fast diagnostic with process exit code and stderr

## 2026-02-11 manual [pitfall]
RP epic completion reviewer blocks on platform-evidence gaps that are environment constraints (e.g. no Linux machine) — pre-emptively mark these as Known Gaps in the epic spec acceptance criteria to avoid review loops

## 2026-02-11 manual [pitfall]
STJ source generator SYSLIB1031 diagnostics cannot be suppressed via #pragma in user code -- they are emitted on generated files. Must use project-level NoWarn with documented rationale and a safety test.

## 2026-02-11 manual [pitfall]
Expression-bodied ReadJson that returns 'new NotSupportedException()' instead of 'throw new' silently returns the exception as data -- always use throw for unsupported operations

## 2026-02-11 manual [pitfall]
When converting float channel values (0-1) to byte (0-255), always clamp and round before casting to byte -- raw cast of out-of-range values wraps/truncates silently

## 2026-02-11 manual [pitfall]
When replacing 'using Newtonsoft.Json' with 'using System.Text.Json.Serialization', [JsonIgnore] silently changes namespace - Newtonsoft no longer recognizes it. Add explicit [Newtonsoft.Json.JsonIgnore] on any property that must be ignored by both serializers during dual-stack period.

## 2026-02-11 manual [pitfall]
When building runtime type registries (Dictionary<string, T>) that are written via registration APIs and read during request processing, use ConcurrentDictionary to prevent data races between concurrent registration and lookup.

## 2026-02-11 manual [pitfall]
Catch-all exception handlers in interop/bridge code should exclude STJ metadata exceptions (InvalidOperationException, NotSupportedException) so AOT registration failures surface immediately instead of being silently swallowed.

## 2026-02-11 manual [pitfall]
STJ source-gen AOT: always serialize concrete types (not interfaces like IPosition/IRange) - use Lift() pattern to convert. IEnumerable<T> also needs typed array overloads.

## 2026-02-11 manual [pitfall]
When migrating encoding/decoding logic across layers (e.g., JSExport boundary vs shared accessor), verify decode operations happen at exactly one layer to prevent double-encoding/decoding that corrupts data with escape sequences.

## 2026-02-11 manual [pitfall]
linguist-generated markers must target only machine-generated/vendored files — never hand-authored specs or docs even if in same directory tree

## 2026-02-12 manual [pitfall]
dotnet restore evaluates ALL TFMs even with -f flag; multi-TFM projects (e.g. browserwasm+desktop) need wasm-tools workload even for desktop-only builds unless the multi-TFM project is excluded from the build
