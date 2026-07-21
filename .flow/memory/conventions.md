# Conventions

Project patterns discovered during work. Not in CLAUDE.md but important.

<!-- Entries added manually via `flowctl memory add` -->

## 2026-02-11 manual [convention]
Extract a single teardown method for lifecycle cleanup shared between unload and template re-apply paths to avoid ordering bugs with guard flags

## 2026-02-11 manual [convention]
Navigation allowlists must enforce full origin (scheme + exact host + default port), not just hostname, to prevent scheme/port bypass

## 2026-02-11 manual [convention]
Classes that subscribe to OS-level events (AccessibilitySettings, UISettings, CoreWindow) must implement IDisposable for deterministic cleanup -- finalizers alone are not sufficient for UI component lifecycles

## 2026-02-12 manual [convention]
When reading process stdout/stderr concurrently with timeout: (1) start ReadToEndAsync tasks, (2) call WaitForExit, (3) if timeout kill process FIRST, (4) then await stream tasks. Never await streams before checking timeout.

## 2026-02-13 manual [convention]
CDP bridge integration tests must verify both C# state (getJsonValue) AND Monaco/DOM state after bridge operations -- testing only one side can miss regressions in the C#->JS application path.
