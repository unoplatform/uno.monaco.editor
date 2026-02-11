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
