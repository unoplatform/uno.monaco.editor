# Pitfalls

Lessons learned from NEEDS_WORK feedback. Things models tend to miss.

<!-- Entries added automatically by hooks or manually via `flowctl memory add` -->

## 2026-02-11 manual [pitfall]
DispatcherQueue.TryEnqueue returns bool; ignoring it can leave TaskCompletionSource permanently incomplete, causing infinite awaits
