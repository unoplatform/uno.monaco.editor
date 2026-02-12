# Comprehensive Documentation Overhaul

## Overview

The uno.monaco.editor codebase has undergone 4 epics of significant refactoring: Desktop Skia target (fn-1), STJ migration (fn-2), CI modernization (fn-3), and type generation pipeline (fn-4). The documentation has not kept pace. This epic delivers complete documentation coverage across all surfaces.

**Prior NuGet version**: `2.0.0-dev.60` (released 7/19/24, package ID `Monaco.Editor`)
**Current**: unreleased v1.0 as `Uno.Monaco.Editor` — major breaking changes in serialization, architecture, and platform support.

## Scope

1. **PR Reviewer Guide** — Reading order and context for the massive refactoring PR (current branch → main), pinned to exact commit range
2. **CHANGELOG** — Rewrite to Keep a Changelog format; document all breaking changes from 2.0.0-dev.60; verify and note Monaco version shipped
3. **Architecture Design Docs** — Mermaid diagrams for dual-platform interop (WASM JSExport vs Desktop JSON-RPC), lifecycle state machine, presenter pattern, serialization layer
4. **README Major Rewrite** — NuGet README standards, platform matrix, getting started, feature overview (absorbs fn-4.7 README scope)
5. **XML Documentation — Hand-written Code** — Full XML docs on all hand-written public APIs; discovery pass to ensure 0 undocumented symbols; enforced via CS1591
6. **XML Documentation Strategy — Generated Monaco Types** — Decide and document strategy (emitter-driven vs post-process) with frozen fn-4 output baseline
7. **Getting Started Guide & API Cookbook** — Step-by-step tutorials, common scenarios, validated code examples
8. **XML Documentation Implementation — Generated Monaco Types** — Execute chosen strategy from task 6, regenerate and validate docs

## Approach

- Follow Uno Platform "Uno-only feature template" for control documentation structure
- Follow `dotnet/runtime` ILogger-level XML doc quality (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>`)
- Use Keep a Changelog 1.1.0 format with Conventional Commits integration
- Mermaid diagrams: sequence diagrams for interop flows, class diagrams for type hierarchy, stateDiagram-v2 for lifecycle
- PR reviewer guide follows dotnet/runtime 3-step review pattern with severity labels
- Cross-reference upstream Monaco TypeDoc API where applicable (`<see href="..."/>`)
- Platform-asymmetric APIs (e.g., `AddActionAsync` throws `PlatformNotSupportedException` on desktop) documented with explicit platform notes
- **Monaco version verification**: Before any task references the Monaco version, verify from root `package.json` (declared: `^0.52.2`) and `node_modules/monaco-editor/package.json` (resolved version) — never hard-code without checking

## Coordination

- **fn-4 dependency**: This epic runs after fn-4 completes. Tasks fn-5.6 and fn-5.8 need fn-4.5 emitter output to be stable.
- **fn-4.7 absorbed**: README updates from fn-4.7 are absorbed into fn-5.4. **Action required**: Update fn-4.7 spec to remove README scope.
- **Package rename**: Document the NuGet ID change from `Monaco.Editor` to `Uno.Monaco.Editor` as a breaking change.

## Quick commands

```bash
# Verify Monaco version from source of truth
cat package.json | grep monaco-editor
cat node_modules/monaco-editor/package.json | grep '"version"'

# Validate XML doc coverage (enforces CS1591)
dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591

# Build to verify docs don't break compilation
dotnet build MonacoEditorComponent.slnx --no-restore
```

## Acceptance

- [ ] PR reviewer guide exists and covers all 4 epics with reading order, pinned to exact commit range
- [ ] CHANGELOG.md follows Keep a Changelog 1.1.0 format with all changes from 2.0.0-dev.60
- [ ] Architecture docs exist with Mermaid diagrams for: dual-platform interop, lifecycle, presenter pattern, serialization
- [ ] README.md rewritten with: platform matrix, getting started, NuGet install, feature overview, badges
- [ ] 0 undocumented hand-written public symbols; verified via `dotnet build /warnaserror:CS1591`
- [ ] Generated Monaco type XML doc strategy decided and documented
- [ ] Generated Monaco types have XML docs (after strategy execution in task 8)
- [ ] Getting started guide with validated code examples for WASM and Desktop
- [ ] API cookbook covers: set text/language, listen to changes, register providers, add decorations/markers
- [ ] fn-4.7 spec updated to remove absorbed README scope

## References

- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
- [Uno Platform Uno-only feature template](https://github.com/unoplatform/uno/blob/master/doc/.feature-template-uno-only.md)
- [Microsoft Learn - XML Documentation Tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [Monaco Editor TypeDoc API](https://microsoft.github.io/monaco-editor/typedoc/index.html)
- [dotnet/runtime ILogger XML doc pattern](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/ILogger.cs)
- [Mermaid Sequence Diagrams](https://mermaid.js.org/syntax/sequenceDiagram.html)
- Existing: `MonacoEditorComponent/DesktopContent/bridge-protocol.md` (190 lines, thorough JSON-RPC spec)
