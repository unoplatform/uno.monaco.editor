# Comprehensive Documentation Overhaul

## Overview

The uno.monaco.editor codebase has undergone 4 epics of significant refactoring: Desktop Skia target (fn-1), STJ migration (fn-2), CI modernization (fn-3), and type generation pipeline (fn-4). The documentation has not kept pace. This epic delivers complete documentation coverage across all surfaces.

**Prior NuGet version**: `2.0.0-dev.60` (released 7/19/24, package ID `Monaco.Editor`)
**Current**: unreleased v1.0 as `Uno.Monaco.Editor` — major breaking changes in serialization, architecture, and platform support.

## Scope

1. **PR Reviewer Guide** — Reading order and context for the massive refactoring PR (current branch → main)
2. **CHANGELOG** — Rewrite to Keep a Changelog format; document all breaking changes from 2.0.0-dev.60; note Monaco version shipped (0.54.0)
3. **Architecture Design Docs** — Mermaid diagrams for dual-platform interop (WASM JSExport vs Desktop JSON-RPC), lifecycle state machine, presenter pattern, serialization layer
4. **README Major Rewrite** — NuGet README standards, platform matrix, getting started, feature overview (absorbs fn-4.7 README scope)
5. **XML Documentation — Hand-written Code** — Full XML docs on CodeEditor partials, presenter types, bridge layer, serialization, helpers, extensions
6. **XML Documentation — Generated Monaco Types** — XML docs on emitter output (details TBD after fn-4 completes)
7. **Getting Started Guide & API Cookbook** — Step-by-step tutorials, common scenarios, code examples

## Approach

- Follow Uno Platform "Uno-only feature template" for control documentation structure
- Follow `dotnet/runtime` ILogger-level XML doc quality (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>`)
- Use Keep a Changelog 1.1.0 format with Conventional Commits integration
- Mermaid diagrams: sequence diagrams for interop flows, class diagrams for type hierarchy, stateDiagram-v2 for lifecycle
- PR reviewer guide follows dotnet/runtime 3-step review pattern with severity labels
- Cross-reference upstream Monaco TypeDoc API where applicable (`<see href="..."/>`)
- Platform-asymmetric APIs (e.g., `AddActionAsync` throws `PlatformNotSupportedException` on desktop) documented with explicit platform notes

## Coordination

- **fn-4 dependency**: This epic runs after fn-4 completes. Task fn-5.6 (generated type XML docs) needs fn-4.5 emitter output to be stable.
- **fn-4.7 absorbed**: README updates from fn-4.7 are absorbed into fn-5.4. Task fn-4.7 should skip README changes or be updated to exclude them.
- **Package rename**: Document the NuGet ID change from `Monaco.Editor` to `Uno.Monaco.Editor` as a breaking change.

## Quick commands

```bash
# Validate XML doc coverage after changes
dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591

# Build to verify docs don't break compilation
dotnet build MonacoEditorComponent.slnx --no-restore
```

## Acceptance

- [ ] PR reviewer guide exists and covers all 4 epics of refactoring with reading order
- [ ] CHANGELOG.md follows Keep a Changelog 1.1.0 format with all changes from 2.0.0-dev.60
- [ ] Architecture docs exist with Mermaid diagrams for: dual-platform interop flow, lifecycle state machine, presenter pattern, serialization layer
- [ ] README.md rewritten with: platform matrix, getting started, NuGet install, feature overview, badges
- [ ] All hand-written public members have XML docs (`<summary>`, `<param>`, `<returns>` minimum)
- [ ] Generated Monaco types have XML docs (after fn-4.5 emitter completes)
- [ ] Getting started guide with working code examples for WASM and Desktop targets
- [ ] API cookbook covers: set text/language, listen to changes, register providers, add decorations/markers

## References

- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
- [Uno Platform Uno-only feature template](https://github.com/unoplatform/uno/blob/master/doc/.feature-template-uno-only.md)
- [Microsoft Learn - XML Documentation Tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [Monaco Editor TypeDoc API](https://microsoft.github.io/monaco-editor/typedoc/index.html)
- [dotnet/runtime ILogger XML doc pattern](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/ILogger.cs)
- [Mermaid Sequence Diagrams](https://mermaid.js.org/syntax/sequenceDiagram.html)
- Existing: `MonacoEditorComponent/DesktopContent/bridge-protocol.md` (190 lines, thorough JSON-RPC spec)
