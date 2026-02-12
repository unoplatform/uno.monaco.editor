# fn-5-comprehensive-documentation-overhaul.9 Audit and fix broken documentation links across all docs and source

## Description
Audit all documentation and source code for broken URLs, then fix or replace them. The primary issue is ~30+ references to the old Monaco Editor API documentation at `microsoft.github.io/monaco-editor/api/...` which now 404 — Monaco migrated to TypeDoc at `/typedoc/`.

**Size:** S-M
**Files:** `README.md`, `CHANGELOG.md`, `docs/*.md`, `MonacoEditorComponent/**/*.cs` (XML comments)

## Approach

### 1. Inventory all external URLs
Grep all `.md` and `.cs` files for `https?://` links. Categorize:
- Old Monaco API URLs (`/api/interfaces/`, `/api/modules/`, `/api/classes/`) — **all confirmed 404**
- Other external URLs (GitHub issues, external docs) — verify reachability

### 2. Map old Monaco API URLs to new TypeDoc URLs
The old URL pattern was: `microsoft.github.io/monaco-editor/api/{type}/{namespace}.{TypeName}.html`
The new TypeDoc site is at: `microsoft.github.io/monaco-editor/typedoc/`

Navigate the TypeDoc site to find the correct replacement URL for each referenced type. Key types to map:

**Interfaces (from README/CHANGELOG/source):**
- `IEditorOptions`, `IMarkerData`, `IMarker`, `IModelDeltaDecoration`
- `IStandaloneCodeEditor` (addAction, addCommand)
- `ITextModel` (findMatches), `IModel`, `IWordAtPosition`
- `IContextKey`, `IEditorFindOptions`
- `IColorPresentation`, `DocumentColorProvider`

**Modules:**
- `monaco.editor` (setModelMarkers, deltadecorations)
- `monaco.languages` (registerCodeActionProvider, registerCodeLensProvider, registerColorProvider, registerCompletionItemProvider, registerHoverProvider)

**Classes:**
- `KeyMod`

### 3. Fix all URLs
- **Markdown files**: Replace inline links and bare URLs
- **C# XML comments**: Update `/// <seealso href="...">` and `/// https://...` comment URLs
- **If a TypeDoc equivalent cannot be found**: Link to the TypeDoc index page with a descriptive anchor text rather than leaving a broken link

### 4. Verify
- Spot-check a sample of replaced URLs to confirm they resolve (200 OK)
- `dotnet build MonacoEditorComponent.slnx --no-restore` still succeeds

## Affected files (known)

**Markdown (old `/api/` URLs):**
- `CHANGELOG.md` — ~10 links (lines 113-116, 174-175, 224, 237-239)
<!-- Updated by plan-sync: README.md was fully rewritten by fn-5.4 and no longer contains old /api/ URLs; removed from this list -->

**C# XML comments (old `/api/` URLs):**
- `MonacoEditorComponent/Monaco/Editor/IMarker.cs`
- `MonacoEditorComponent/Monaco/Editor/MarkerData.cs`
- `MonacoEditorComponent/Monaco/Editor/Marker.cs`
- `MonacoEditorComponent/Monaco/Editor/IContextKey.cs`
- `MonacoEditorComponent/Monaco/Editor/IEditorFindOptions.cs`
- `MonacoEditorComponent/Monaco/Editor/WordAtPosition.cs`
- `MonacoEditorComponent/Monaco/Editor/IWordAtPosition.cs`
- `MonacoEditorComponent/Monaco/Editor/IModel.cs`
- `MonacoEditorComponent/Monaco/LanguagesHelper.cs`
- `MonacoEditorComponent/Monaco/Languages/ColorPresentation.cs`
- `MonacoEditorComponent/Monaco/Languages/DocumentColorProvider.cs`
- `MonacoEditorComponent/Monaco/KeyMod.cs`
- `MonacoEditorComponent/Monaco/ModelHelper.cs`
<!-- Updated by plan-sync: fn-5.5 already replaced CodeEditor/CodeEditor.Methods.cs URLs with new TypeDoc pattern; removed from this list -->

## Key context
- Old Monaco API docs (`/api/`) are fully removed — all return 404
- New docs at `microsoft.github.io/monaco-editor/typedoc/` use TypeDoc with JS rendering
- The epic spec already references the TypeDoc URL (line 70)
- Task fn-5.4 (README rewrite) will fully replace README content — but CHANGELOG and C# source links are NOT covered by any other task
- Task fn-5.5 (XML docs) added new docs to hand-written APIs using the correct TypeDoc URL pattern (e.g., `https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor.ICodeEditor.html`). CodeEditor files no longer have old `/api/` URLs. Remaining old `/api/` URLs are exclusively in `Monaco/` generated type files.
- Establish the correct URL pattern here so tasks 7 can follow it
<!-- Updated by plan-sync: fn-5.5 already used TypeDoc URLs in CodeEditor; old /api/ URLs remain only in Monaco/ generated files -->

## Acceptance
- [ ] All old `/api/` Monaco URLs replaced with working TypeDoc equivalents (or TypeDoc index fallback)
- [ ] No broken external URLs remain in markdown files
- [ ] No broken URLs remain in C# XML comments
- [ ] Sample of replaced URLs verified as reachable
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
