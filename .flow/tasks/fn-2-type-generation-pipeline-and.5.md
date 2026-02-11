# fn-2-type-generation-pipeline-and.5 Migrate serialization call sites and redesign ParentAccessor for AOT

## Description
Migrate all `JsonConvert.SerializeObject`/`DeserializeObject` call sites to `JsonSerializer` with `MonacoJsonContext`, and redesign `ParentAccessor` to eliminate AOT-hostile runtime type resolution.

**Size:** M
**Files:** MonacoEditorComponent/Extensions/WebViewExtensions.cs, MonacoEditorComponent/CodeEditor/CodeEditor.Methods.cs, MonacoEditorComponent/Monaco/LanguagesHelper.cs, MonacoEditorComponent/Monaco/ModelHelper.cs, MonacoEditorComponent/Helpers/ParentAccessor.cs

## Approach

- **WebViewExtensions.cs** (~3 call sites):
  - Replace `JsonConvert.DeserializeObject<T>(returnstring)` with `JsonSerializer.Deserialize<T>(returnstring, MonacoJsonContext.Default.Options)`
  - Replace `JsonConvert.ToString(item)` with `JsonSerializer.Serialize(item, MonacoJsonContext.Default.Options)`
  - Replace `JsonConvert.SerializeObject(item, _settings)` with `JsonSerializer.Serialize(item, MonacoJsonContext.Default.Options)`
  - Delete `_settings` field (CamelCasePropertyNamesContractResolver + NullValueHandling.Ignore) — now on context

- **CodeEditor.Methods.cs** (~10 call sites):
  - Replace inline `JsonConvert.SerializeObject(position)` in JS script strings with `JsonSerializer.Serialize(position, MonacoJsonContext.Default.Position)`
  - Use typed `MonacoJsonContext.Default.<Type>` overloads where possible for AOT
  - `AddCommandAsync` line 152: `DeserializeObject<object>` returns `JsonElement` instead of `JObject` — this is a breaking API change, document it

- **LanguagesHelper.cs** (~16 call sites):
  - Replace all `JsonConvert.DeserializeObject<T>(args[i])` with `JsonSerializer.Deserialize<T>(args[i], MonacoJsonContext.Default.Options)`
  - Replace `JsonConvert.SerializeObject(result)` with typed serialize calls

- **ModelHelper.cs** (~10 call sites):
  - Replace `JsonConvert.SerializeObject` calls used for building JS payloads (e.g., `setValue`, `normalizeIndentations`)
  - Replace `JsonConvert.ToString()` string-escaping calls with `JsonSerializer.Serialize<string>()`
  - Verify JS payload construction works identically after migration (escaping, quoting)

- **ParentAccessor.cs** (hardest — AOT redesign):
  - `GetJsonValue()` at line ~177: Replace with `JsonSerializer.Serialize(obj, obj.GetType(), MonacoJsonContext.Default.Options)`. Add explicit fail-fast: if the runtime type is not registered in MonacoJsonContext, catch `InvalidOperationException` from STJ and re-throw with a clear message indicating the type must be registered. Add tests for both known-type and unknown-type serialization.
  - `SetValue(name, value, type)` at line ~266: Replace with FQN-keyed `Dictionary<string, JsonTypeInfo>` lookup:
    - Primary keys: use `typeof(T).FullName` for all registered types (avoids hardcoded namespace errors)
    - Compatibility aliases: short names via `typeof(T).Name` for backward compatibility with existing JS callers
    - Fail-fast: throw `InvalidOperationException($"Type '{typeName}' is not registered for deserialization. Register it in MonacoJsonContext.")` for unknown types
    - Expose `RegisterTypeInfo(string name, JsonTypeInfo info)` method for external extension
  - Delete `LookForTypeByName` method (assembly scanning) — replaced by static type map
  - Populate the type map from `MonacoJsonContext.Default` at initialization

- Add callback round-trip contract tests for: Completion, CodeAction, Hover, Color, Markers — simulating the JS->C#->JS serialization path through LanguagesHelper
- Add ParentAccessor-specific tests: known type SetValue, unknown type SetValue (expect exception), GetJsonValue for registered and unregistered types

## Key context

- `JsonConvert.ToString(string)` produces a JSON-quoted string. STJ equivalent: `JsonSerializer.Serialize("hello")` produces same output.
- The JS interop pattern `"JSON.parse('" + serialize(obj) + "')"` is sensitive to escaping. Use `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on the context.
- With `JsonSerializerIsReflectionEnabledByDefault=false`, any type NOT registered in MonacoJsonContext will throw at runtime. Both `GetJsonValue` and `SetValue` must handle this gracefully with clear error messages.
- `ParentAccessor.SetValue` runtime type dispatch is the single hardest migration point. The a2a-dotnet `A2AJsonUtilities.cs` type registry pattern is a reference.

## Acceptance
- [ ] Zero `JsonConvert.SerializeObject` / `DeserializeObject` calls in codebase
- [ ] Zero `Newtonsoft.Json.JsonSerializerSettings` usage
- [ ] `ParentAccessor.SetValue` uses FQN-keyed type info lookup (no `Type.GetType()`)
- [ ] `ParentAccessor.GetJsonValue` handles unregistered types with clear error message
- [ ] `ParentAccessor.LookForTypeByName` method deleted
- [ ] Fail-fast `InvalidOperationException` for unregistered types in both SetValue and GetJsonValue
- [ ] `RegisterTypeInfo` extensibility API exposed on ParentAccessor
- [ ] All serialization uses `MonacoJsonContext.Default.Options` or typed overloads
- [ ] Callback round-trip contract tests for Completion, CodeAction, Hover, Color, Markers
- [ ] ParentAccessor tests for known/unknown type handling
- [ ] Golden baseline tests still pass
- [ ] Build succeeds for both TFMs

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
