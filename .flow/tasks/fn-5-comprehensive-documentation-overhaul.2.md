# fn-5-comprehensive-documentation-overhaul.2 Rewrite CHANGELOG to Keep a Changelog format with breaking changes

## Description
Rewrite the existing `changelog.md` to follow [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) format. Document all changes from the prior NuGet release `2.0.0-dev.60` (package `Monaco.Editor`, released 7/19/24) to the current unreleased version (`Uno.Monaco.Editor`). Include Monaco Editor version notes.

**Size:** M
**Files:** `CHANGELOG.md` (rename from `changelog.md`)

## Approach

- Analyze git history: `git log --oneline` from the 2.0.0-dev.60 tag/release through HEAD
- Restructure existing entries into Keep a Changelog categories: Added, Changed, Deprecated, Removed, Fixed, Security
- Use ISO 8601 dates (`YYYY-MM-DD`)
- Add comparison links at bottom (`[Unreleased]: https://github.com/.../compare/v1.0.0...HEAD`)
- Document breaking changes prominently:
  - **Package rename**: `Monaco.Editor` → `Uno.Monaco.Editor`
  - **Serialization**: Newtonsoft.Json → System.Text.Json (STJ with source generators)
  - **CommandHandler**: receives `JsonElement` instead of `JObject`
  - **Custom converters**: must be rewritten for STJ
  - **Desktop target**: new `ICodeEditorPresenter` abstraction, JSON-RPC bridge
  - **Lifecycle**: `EditorLifecycleState` state machine replaces ad-hoc flags
  - **`AddAssemblyForTypeLookup`**: obsoleted, replaced by `RegisterTypeInfo`
- Note Monaco Editor version shipped: 0.54.0 (upgraded from previous). Include brief highlights of significant Monaco upstream changes if notable.
- Preserve historical entries (v0.1–v0.9) but reformat to Keep a Changelog structure
- Fix known typo ("Sergvice" at line 21 of existing changelog)

## Key context

- Existing changelog at `changelog.md` (112 lines, informal format, no category grouping)
- Conventional Commits used in this repo (per AGENTS.md) — entries can be derived from commit messages
- Git tags: `1.0`, `1.1.0`, `v0.1.0`–`v0.7.0` — may not perfectly align with changelog version entries
## Acceptance
- [ ] File renamed to `CHANGELOG.md` (uppercase, standard convention)
- [ ] Follows Keep a Changelog 1.1.0 format with header boilerplate
- [ ] `[Unreleased]` section at top
- [ ] All changes from 2.0.0-dev.60 categorized into Added/Changed/Deprecated/Removed/Fixed/Security
- [ ] Breaking changes clearly marked in Changed and Removed sections
- [ ] Package rename documented (Monaco.Editor → Uno.Monaco.Editor)
- [ ] STJ migration documented with migration guidance
- [ ] Desktop target addition documented
- [ ] Monaco Editor version (0.54.0) noted with brief upstream highlights
- [ ] Historical entries (v0.1–v0.9) reformatted
- [ ] ISO 8601 dates on all version entries
- [ ] Comparison links at bottom of file
- [ ] "Sergvice" typo fixed
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
