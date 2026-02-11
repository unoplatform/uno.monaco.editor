using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Monaco.Editor;
using Monaco.Languages;

namespace Monaco.Serialization;

/// <summary>
/// STJ source-generated serialization context for all Monaco model types
/// that cross the JS interop boundary.
/// </summary>
/// <remarks>
/// <para>
/// This context uses <c>CamelCase</c> naming and <c>WhenWritingNull</c> ignore
/// semantics to match the legacy serialization behavior. <c>UnsafeRelaxedJsonEscaping</c>
/// is used because Monaco content includes code with characters (&lt;, &gt;, &amp;)
/// that STJ escapes by default but the prior serializer did not.
/// </para>
/// <para>
/// Numeric enums (MarkerSeverity, CompletionItemKind, TrackedRangeStickiness, etc.)
/// are intentionally kept as integers. String-backed enums use per-enum
/// <c>[JsonConverter(typeof(JsonStringEnumConverter&lt;T&gt;))]</c> with
/// <c>[JsonStringEnumMemberName]</c> attributes on each member.
/// </para>
/// <para>
/// <b>Do NOT set <c>UseStringEnumConverter = true</c></b> — that would break numeric
/// enum contracts with the Monaco JS runtime.
/// </para>
/// <para>
/// <b>SYSLIB1031 suppression</b>: <c>Monaco.Uri</c> and <c>System.Uri</c> both appear as
/// discovered types (from <c>IMarkdownString.Uris</c> and <c>IRelatedInformation.Resource</c>
/// respectively). The source generator picks the first and warns about the TypeInfo property
/// name collision. This is safe; both types serialize correctly via their own metadata.
/// The suppression is in .csproj because SG diagnostics cannot be suppressed via pragma.
/// A dedicated test (<c>UriCollision_BothTypesSerializeCorrectly</c>) validates correctness.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
// --- Primitive / core types ---
[JsonSerializable(typeof(Position))]
[JsonSerializable(typeof(Range))]
[JsonSerializable(typeof(Selection))]
[JsonSerializable(typeof(IMarkdownString))]
// --- Editor types ---
[JsonSerializable(typeof(Marker))]
[JsonSerializable(typeof(MarkerData))]
[JsonSerializable(typeof(IModelDeltaDecoration))]
[JsonSerializable(typeof(IModelDecorationOptions))]
[JsonSerializable(typeof(IModelDecorationMinimapOptions))]
[JsonSerializable(typeof(IModelDecorationOverviewRulerOptions))]
[JsonSerializable(typeof(ISingleEditOperation))]
[JsonSerializable(typeof(IRelatedInformation))]
[JsonSerializable(typeof(StandaloneEditorConstructionOptions))]
[JsonSerializable(typeof(ContextKey))]
[JsonSerializable(typeof(FindMatch))]
[JsonSerializable(typeof(WordAtPosition))]
// --- Language types ---
[JsonSerializable(typeof(CompletionItem))]
[JsonSerializable(typeof(CompletionList))]
[JsonSerializable(typeof(CompletionContext))]
[JsonSerializable(typeof(CodeAction))]
[JsonSerializable(typeof(CodeActionList))]
[JsonSerializable(typeof(CodeActionContext))]
[JsonSerializable(typeof(CodeLens))]
[JsonSerializable(typeof(CodeLensList))]
[JsonSerializable(typeof(Hover))]
[JsonSerializable(typeof(ColorInformation))]
[JsonSerializable(typeof(ColorPresentation))]
[JsonSerializable(typeof(Command))]
[JsonSerializable(typeof(TextEdit))]
[JsonSerializable(typeof(WorkspaceEdit))]
[JsonSerializable(typeof(WorkspaceTextEdit))]
[JsonSerializable(typeof(ILanguageExtensionPoint))]
// --- Deserialization helper types ---
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
// --- Collection variants for array/list interop ---
[JsonSerializable(typeof(Position[]))]
[JsonSerializable(typeof(Range[]))]
[JsonSerializable(typeof(Selection[]))]
[JsonSerializable(typeof(IMarkdownString[]))]
[JsonSerializable(typeof(Marker[]))]
[JsonSerializable(typeof(MarkerData[]))]
[JsonSerializable(typeof(IModelDeltaDecoration[]))]
[JsonSerializable(typeof(ISingleEditOperation[]))]
[JsonSerializable(typeof(IRelatedInformation[]))]
[JsonSerializable(typeof(CompletionItem[]))]
[JsonSerializable(typeof(CodeAction[]))]
[JsonSerializable(typeof(CodeLens[]))]
[JsonSerializable(typeof(ColorInformation[]))]
[JsonSerializable(typeof(ColorPresentation[]))]
[JsonSerializable(typeof(WorkspaceTextEdit[]))]
[JsonSerializable(typeof(TextEdit[]))]
[JsonSerializable(typeof(FindMatch[]))]
[JsonSerializable(typeof(ILanguageExtensionPoint[]))]
[JsonSerializable(typeof(IList<ILanguageExtensionPoint>))]
[JsonSerializable(typeof(IEnumerable<Marker>))]
[JsonSerializable(typeof(IEnumerable<FindMatch>))]
[JsonSerializable(typeof(IEnumerable<string>))]
internal partial class MonacoJsonContext : JsonSerializerContext
{
    private static MonacoJsonContext? _relaxedInstance;

    /// <summary>
    /// Gets a singleton instance configured with <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>
    /// for Monaco interop. Use this when serializing content that may contain code characters.
    /// </summary>
    internal static MonacoJsonContext Relaxed =>
        _relaxedInstance ??= new MonacoJsonContext(CreateRelaxedOptions());

    private static JsonSerializerOptions CreateRelaxedOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return options;
    }

    /// <summary>
    /// Builds a type info lookup dictionary keyed by both fully-qualified name and short name.
    /// Used by <see cref="Monaco.Helpers.ParentAccessor"/> and <see cref="Monaco.Helpers.ParentAccessorDesktop"/>
    /// for AOT-safe deserialization in SetValue.
    /// Thread-safe: returns a <see cref="ConcurrentDictionary{TKey,TValue}"/> since the map
    /// may be read during SetValue while written via RegisterTypeInfo.
    /// </summary>
    internal static ConcurrentDictionary<string, JsonTypeInfo> BuildTypeInfoMap()
    {
        var context = Default;
        var map = new ConcurrentDictionary<string, JsonTypeInfo>(StringComparer.Ordinal);

        // Register all concrete types that may arrive from JS via setValueWithType.
        // Primary key: FullName; compatibility alias: Name (short name for JS callers).
        RegisterType<Position>(context, map);
        RegisterType<Range>(context, map);
        RegisterType<Selection>(context, map);
        RegisterType<IMarkdownString>(context, map);
        RegisterType<Marker>(context, map);
        RegisterType<MarkerData>(context, map);
        RegisterType<IModelDeltaDecoration>(context, map);
        RegisterType<IModelDecorationOptions>(context, map);
        RegisterType<ISingleEditOperation>(context, map);
        RegisterType<StandaloneEditorConstructionOptions>(context, map);
        RegisterType<ContextKey>(context, map);
        RegisterType<FindMatch>(context, map);
        RegisterType<WordAtPosition>(context, map);
        RegisterType<CompletionItem>(context, map);
        RegisterType<CompletionList>(context, map);
        RegisterType<CompletionContext>(context, map);
        RegisterType<CodeAction>(context, map);
        RegisterType<CodeActionList>(context, map);
        RegisterType<CodeActionContext>(context, map);
        RegisterType<CodeLens>(context, map);
        RegisterType<CodeLensList>(context, map);
        RegisterType<Hover>(context, map);
        RegisterType<ColorInformation>(context, map);
        RegisterType<ColorPresentation>(context, map);
        RegisterType<Command>(context, map);
        RegisterType<TextEdit>(context, map);
        RegisterType<WorkspaceEdit>(context, map);
        RegisterType<WorkspaceTextEdit>(context, map);
        RegisterType<ILanguageExtensionPoint>(context, map);

        return map;
    }

    private static void RegisterType<T>(MonacoJsonContext context, ConcurrentDictionary<string, JsonTypeInfo> map)
    {
        var typeInfo = context.GetTypeInfo(typeof(T));
        if (typeInfo is null) return;

        var type = typeof(T);
        if (type.FullName is not null)
        {
            map[type.FullName] = typeInfo;
        }

        // Short name alias for backward compatibility with JS callers
        // that use unqualified type names (e.g., "Selection" instead of "Monaco.Selection").
        map.TryAdd(type.Name, typeInfo);
    }
}
