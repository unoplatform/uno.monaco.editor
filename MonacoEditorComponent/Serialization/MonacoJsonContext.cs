using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// semantics to match existing Newtonsoft behavior. <c>UnsafeRelaxedJsonEscaping</c>
/// is used because Monaco content includes code with characters (&lt;, &gt;, &amp;)
/// that STJ escapes by default but Newtonsoft does not.
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
}
