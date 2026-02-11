# Pitfalls

Lessons learned from NEEDS_WORK feedback. Things models tend to miss.

<!-- Entries added automatically by hooks or manually via `flowctl memory add` -->

## 2026-02-11 manual [pitfall]
DispatcherQueue.TryEnqueue returns bool; ignoring it can leave TaskCompletionSource permanently incomplete, causing infinite awaits

## 2026-02-11 manual [pitfall]
When adding idempotency guards to init methods, ensure teardown cleans up based on actual field state (not just guard flags), and include rollback in catch blocks to handle partial initialization failures
