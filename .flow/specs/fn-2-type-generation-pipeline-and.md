# Type Generation Pipeline and System.Text.Json Migration

## Overview

Migrate `Uno.Monaco.Editor` from Newtonsoft.Json to System.Text.Json with AOT-compatible source generation, and modernize the dead TypedocConverter-based type generation pipeline to emit STJ-attributed C# from current Monaco typings.

**Current state:**
- 103 files with `using Newtonsoft.Json`, 512+ `[JsonProperty]` attributes, 22+ custom converter classes
- TypedocConverter is dead/unmaintained, pinned to `monaco-editor@0.21.3` (vendored runtime is v0.54.0)
- AOT-hostile patterns in `ParentAccessor.cs` (runtime `Type.GetType()` + `DeserializeObject(string, Type)`)
- No STJ usage in application code (fn-1 will introduce first `[JsonSerializable]` context for bridge DTOs)

**Target state:**
- Zero Newtonsoft.Json dependency in MonacoEditorComponent (library); repo-wide cleanup completed by task 7
- All serialization via STJ source-generated `MonacoJsonContext`
- String-backed enum converters replaced by `[JsonStringEnumMemberName]` + per-enum `[JsonConverter]`; numeric enums preserved as-is
- AOT-safe `ParentAccessor` with FQN-keyed pre-registered type map
- Interim type generation pipeline (post-processing approach) that either generates STJ-compatible C# from `monaco.d.ts` via TypedocConverter+postprocessor, or transforms existing `.cs` files as a standalone tool. A full pipeline replacement is a follow-up.
- Serialization contract tests proving round-trip parity with Newtonsoft for all JS interop types

## Scope

**In scope:** STJ migration of all Monaco model types, domain converter rewrites, call-site migration, ParentAccessor AOT redesign, type generation pipeline replacement, serialization contract tests.

**Out of scope:** Full regeneration of all Monaco types from 0.54.0 (deferred — generator produces correct output to isolated `.generated` directory, full regeneration is a follow-up). Test app updates beyond build verification.

## Key decisions

1. **Migration-first approach**: Migrate existing files mechanically, then modernize generator. Lower risk than regenerating everything at once.
2. **NO global `UseStringEnumConverter`**: Monaco has both string-backed enums (e.g., `CursorBlinking` → `"blink"`) and numeric enums (e.g., `MarkerSeverity`, `CompletionItemKind`, `TrackedRangeStickiness`). Global string conversion would break JS contract for numeric enums. Instead: opt-in `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` only on string-backed enums.
3. **Global CamelCase naming policy** on `MonacoJsonContext`. Explicit `[JsonPropertyName]` only for names that don't follow PascalCase→camelCase convention.
4. **Global `DefaultIgnoreCondition = WhenWritingNull`** replaces per-property `NullValueHandling.Ignore`. Properties that MUST serialize null need explicit `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`.
5. **ParentAccessor redesign**: Replace `Type.GetType()` + `DeserializeObject(string, Type)` with a `Dictionary<string, JsonTypeInfo>` lookup keyed by fully-qualified type names. Fail-fast with `InvalidOperationException` for unregistered types. Expose explicit registration API for external extension types.
6. **InterfaceToClassConverter replacement**: Custom `JsonConverter<TInterface>` that delegates to `JsonSerializer.Deserialize<TClass>()` (no `Populate()` equivalent in STJ).
7. **This is a breaking change** — requires major version bump. Newtonsoft.Json transitive dependency removed; `CommandHandler` receives `JsonElement` instead of `JObject`.
8. **Type generation pipeline (interim)**: Post-processing approach (Option B) — run TypedocConverter (if it works on 0.54.0), then post-process output to replace Newtonsoft attributes with STJ attributes. If TypedocConverter fails on 0.54.0, the post-processor also works as a standalone tool on existing `.cs` files. Output goes to isolated `GenerateMonacoTypings/output/` directory; hand-tuned files never overwritten. A full pipeline replacement (custom generator or TypedocConverter fork) is a potential follow-up once the interim pipeline proves the attribute transformation patterns.

## fn-1 dependency edges

This epic depends on fn-1 (Desktop Skia) for:
- **fn-1 task 1**: TFM consolidation and `OperatingSystem.IsBrowser()` runtime detection must be stable before refactoring serialization call sites
- **fn-1 task 5**: Establishes `BridgeSerializerContext` with `SystemTextJsonFormatter` — the pattern this epic extends to all Monaco model types
- **WebViewExtensions.cs**: fn-1 task 5 touches this file for desktop bridge; serial edits required to avoid conflicts

Tasks 1–4 and 7 of this epic are technically independent of fn-1 (they don't touch bridge code), but task 5 (call-site migration) depends on fn-1's stabilization of the interop layer.

## Key references

- BlazorMonaco (STJ-native Monaco wrapper): `serdarciplak/BlazorMonaco` — reference for STJ Monaco patterns
- Telegram.Bot STJ migration: `TelegramBots/Telegram.Bot` — thorough migration reference with source gen
- Uno Platform FontManifest pattern: `unoplatform/uno` `src/Uno.UI/UI/Xaml/FontManifest.cs` — canonical Uno STJ source gen
- [MS migration guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft)
- [STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

## Quick commands

```bash
# Verify build after migration
dotnet build MonacoEditorComponent.slnx --no-restore

# Verify both app targets
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop

# Check no Newtonsoft references remain in library
grep -r "Newtonsoft" MonacoEditorComponent/ --include="*.cs" --include="*.csproj"

# Run serialization contract tests
dotnet test MonacoEditorComponent.slnx --filter "Category=Serialization"
```

## Acceptance

- [ ] Zero `using Newtonsoft.Json` in MonacoEditorComponent/
- [ ] Zero `Newtonsoft.Json` in Directory.Packages.props and .csproj
- [ ] `MonacoJsonContext` source-gen context covers all cross-boundary types
- [ ] `JsonSerializerIsReflectionEnabledByDefault` set to `false`
- [ ] String-backed enums use `[JsonStringEnumMemberName]` + per-enum converter; numeric enums remain numeric
- [ ] `ParentAccessor` is AOT-compatible (no runtime `Type.GetType()`), uses FQN-keyed type map, fails fast on unregistered types
- [ ] Serialization contract tests verify round-trip parity for Position, Range, Selection, CompletionItem, CodeAction, Hover, ColorInformation, IMarkerData
- [ ] Build succeeds for `net10.0-browserwasm` and `net10.0-desktop` targets
- [ ] Type generation pipeline has at least one working path that produces compilable STJ-attributed C# to isolated output directory (either `monaco.d.ts`-based via TypedocConverter+postprocessor, or standalone postprocessor on existing `.cs` files — both are valid for this epic; a full `.d.ts`-based pipeline is a follow-up)
- [ ] changelog.md documents breaking change
- [ ] Zero Newtonsoft references repo-wide after task 7 completes
