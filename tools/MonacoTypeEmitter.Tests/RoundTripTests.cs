#nullable enable

using System.Text.Json;
using System.Text.RegularExpressions;
using MonacoTypeEmitter.Model;
using Xunit;

namespace MonacoTypeEmitter.Tests;

/// <summary>
/// Round-trip tests that verify the emitter produces C# types whose emitted source code
/// preserves wire-format compatibility with SerializationContractTests golden baselines.
///
/// These tests validate source-text output because:
/// 1. The emitter is a code generation tool -- its contract is "produce correct C# source"
/// 2. Runtime serialization tests already exist in SerializationContractTests (the baselines)
/// 3. Runtime compilation of emitted types would require Roslyn scripting/reflection which
///    adds complexity without additional coverage over (source assertions + SerializationContractTests)
///
/// Key types covered: MarkerSeverity, BuiltinTheme, MarkerData, CompletionItemKind,
/// CompletionItem, and TextEditorCursorStyle.
///
/// Note: CursorStyle (string enum, kebab-case values) is a hand-tuned type on the repo's
/// ignore list. The emittable equivalent is TextEditorCursorStyle (numeric enum) which maps
/// to monaco.editor.TextEditorCursorStyle in the Monaco API. BuiltinTheme covers the string
/// enum emission path (same JsonStringEnumConverter+EnumMember pattern as CursorStyle).
/// </summary>
[Trait("Category", "RoundTrip")]
public partial class RoundTripTests
{
    /// <summary>
    /// Verifies the emitted MarkerSeverity enum has the correct numeric values
    /// that match the golden baseline (Hint=1, Info=2, Warning=4, Error=8).
    /// </summary>
    [Fact]
    public void MarkerSeverity_EmittedValues_MatchGoldenBaseline()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        var markerSeverityFile = files.FirstOrDefault(f =>
            f.Key.EndsWith("MarkerSeverity.cs", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(markerSeverityFile.Value),
            "MarkerSeverity.cs was not emitted");

        var content = markerSeverityFile.Value;

        // Verify numeric enum values match golden baseline from SerializationContractTests
        // Error = 8, Warning = 4, Info = 2, Hint = 1
        Assert.Contains("Hint = 1", content);
        Assert.Contains("Info = 2", content);
        Assert.Contains("Warning = 4", content);
        Assert.Contains("Error = 8", content);

        // Must NOT have JsonStringEnumConverter (it's a numeric enum)
        Assert.DoesNotContain("JsonStringEnumConverter", content);
    }

    /// <summary>
    /// Verifies the emitted BuiltinTheme string enum (type alias) has the correct wire-format
    /// values. BuiltinTheme is a string literal union type alias in the full model
    /// (vs, vs-dark, hc-black, hc-light) and demonstrates the string enum emission pattern
    /// that CursorStyle also uses (CursorStyle is on the ignore list for the real repo).
    /// </summary>
    [Fact]
    public void BuiltinTheme_EmittedValues_AreStringEnum()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        var builtinThemeFile = files.FirstOrDefault(f =>
            f.Key.EndsWith("BuiltinTheme.cs", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(builtinThemeFile.Value),
            $"BuiltinTheme.cs was not emitted. Available: {string.Join(", ", files.Keys.Where(k => k.Contains("Builtin")))}");

        var content = builtinThemeFile.Value;

        // Must have JsonStringEnumConverter (it's a string enum from type alias)
        Assert.Contains("JsonStringEnumConverter<BuiltinTheme>", content);

        // Verify all wire-format values
        Assert.Contains("\"vs\"", content);
        Assert.Contains("\"vs-dark\"", content);
        Assert.Contains("\"hc-black\"", content);
        Assert.Contains("\"hc-light\"", content);

        // Verify member names are PascalCase
        Assert.Contains("Vs,", content);
        Assert.Contains("VsDark", content);
        Assert.Contains("HcBlack", content);
        Assert.Contains("HcLight", content);
    }

    /// <summary>
    /// Verifies the emitted CompletionItemKind enum has the correct numeric values
    /// matching the golden baseline (Method=0, Function=1, etc.).
    /// </summary>
    [Fact]
    public void CompletionItemKind_EmittedValues_MatchGoldenBaseline()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        var completionItemKindFile = files.FirstOrDefault(f =>
            f.Key.EndsWith("CompletionItemKind.cs", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(completionItemKindFile.Value),
            "CompletionItemKind.cs was not emitted");

        var content = completionItemKindFile.Value;

        // Verify numeric enum values match golden baseline from SerializationContractTests
        // Method = 0, Function = 1
        Assert.Contains("Method = 0", content);
        Assert.Contains("Function = 1", content);

        // Must NOT have JsonStringEnumConverter (it's a numeric enum)
        Assert.DoesNotContain("JsonStringEnumConverter", content);
    }

    /// <summary>
    /// Verifies the emitted IMarkerData interface has the correct property structure
    /// matching the golden baseline wire format from SerializationContractTests.
    /// </summary>
    [Fact]
    public void MarkerData_EmittedProperties_MatchGoldenBaseline()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        // Find the MarkerData concrete class (emitted from IMarkerData interface)
        var markerDataFile = files.FirstOrDefault(f =>
            f.Key.EndsWith("/MarkerData.cs", StringComparison.OrdinalIgnoreCase)
            || f.Key == "MarkerData.cs");

        // If not a standalone file, check for it under Editor/
        if (string.IsNullOrEmpty(markerDataFile.Value))
        {
            markerDataFile = files.FirstOrDefault(f =>
                f.Key.EndsWith("Editor/MarkerData.cs", StringComparison.OrdinalIgnoreCase));
        }

        Assert.False(string.IsNullOrEmpty(markerDataFile.Value),
            $"MarkerData.cs was not emitted. Available files: {string.Join(", ", files.Keys)}");

        var content = markerDataFile.Value;

        // Must implement IMarkerData
        Assert.Contains(": IMarkerData", content);

        // Must have the key properties from the golden baseline
        Assert.Contains("Severity", content);
        Assert.Contains("Message", content);
        Assert.Contains("StartLineNumber", content);
        Assert.Contains("StartColumn", content);
        Assert.Contains("EndLineNumber", content);
        Assert.Contains("EndColumn", content);

        // Properties must use { get; set; } (mutable model class)
        Assert.Matches(PropertyGetSetPattern(), content);

        // MarkerData properties map naturally via camelCase naming policy,
        // so [JsonPropertyName] is correctly omitted (no manual overrides needed).
        // Wire format relies on JsonNamingPolicy.CamelCase in MonacoJsonContext.
    }

    /// <summary>
    /// Verifies the emitted CompletionItem interface has the correct property structure
    /// matching the golden baseline wire format from SerializationContractTests.
    /// Note: In the Monaco model, CompletionItem is an interface without the I-prefix,
    /// so the emitter outputs it as a plain interface (not a concrete class pair).
    /// Validates key properties: label, insertText, kind, detail, sortText, filterText.
    /// </summary>
    [Fact]
    public void CompletionItem_EmittedProperties_MatchGoldenBaseline()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        // CompletionItem in the Monaco model is an interface without I-prefix,
        // so it's emitted as Languages/CompletionItem.cs (not ICompletionItem.cs)
        var completionItemFile = files.FirstOrDefault(f =>
            (f.Key.EndsWith("/CompletionItem.cs", StringComparison.OrdinalIgnoreCase)
             || f.Key == "CompletionItem.cs")
            && !f.Key.Contains("CompletionItemK", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemI", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemL", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemP", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemR", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemT", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrEmpty(completionItemFile.Value),
            $"CompletionItem was not emitted. Available: {string.Join(", ", files.Keys.Where(k => k.Contains("Completion", StringComparison.OrdinalIgnoreCase)))}");

        var content = completionItemFile.Value;

        // Must have the key properties from the golden baseline (SerializationContractTests.Golden_CompletionItem)
        Assert.Contains("Label", content);
        Assert.Contains("InsertText", content);
        Assert.Contains("Kind", content);
        Assert.Contains("Detail", content);
        Assert.Contains("SortText", content);
        Assert.Contains("FilterText", content);

        // Must be emitted as interface (CompletionItem without I-prefix)
        Assert.Contains("public interface CompletionItem", content);
    }

    /// <summary>
    /// Verifies the emitted TextEditorCursorStyle enum has the correct numeric values
    /// matching the Monaco API (Line=1, Block=2, Underline=3, LineThin=4, BlockOutline=5, UnderlineThin=6).
    /// This is the underlying numeric enum for cursor styles; the string enum CursorStyle
    /// is a hand-tuned type alias in the real repo.
    /// </summary>
    [Fact]
    public void TextEditorCursorStyle_EmittedValues_MatchMonacoApi()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        var cursorStyleFile = files.FirstOrDefault(f =>
            f.Key.EndsWith("TextEditorCursorStyle.cs", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(cursorStyleFile.Value),
            $"TextEditorCursorStyle.cs was not emitted. Available: {string.Join(", ", files.Keys.Where(k => k.Contains("Cursor", StringComparison.OrdinalIgnoreCase)))}");

        var content = cursorStyleFile.Value;

        // Verify numeric enum values match the Monaco API
        Assert.Contains("Line = 1", content);
        Assert.Contains("Block = 2", content);
        Assert.Contains("Underline = 3", content);
        Assert.Contains("LineThin = 4", content);
        Assert.Contains("BlockOutline = 5", content);
        Assert.Contains("UnderlineThin = 6", content);

        // Must NOT have JsonStringEnumConverter (it's a numeric enum)
        Assert.DoesNotContain("JsonStringEnumConverter", content);
    }

    /// <summary>
    /// Comprehensive wire-format compatibility test that validates the full serialization
    /// attribute chain for key types matches what SerializationContractTests expects at runtime.
    /// This verifies:
    /// - Interface/concrete class pairing (IMarkerData -> MarkerData, ICompletionItem -> CompletionItem)
    /// - InterfaceToClassConverter attributes on interfaces
    /// - JsonStringEnumConverter + EnumMember on string enums
    /// - Numeric enum values without string converter
    /// - Property names that map correctly via camelCase policy
    /// - sealed class pattern for concrete implementations
    /// </summary>
    [Fact]
    public void WireFormatCompatibility_FullAttributeChain()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        // === IMarkerData -> MarkerData serialization chain ===
        var iMarkerData = files.First(f =>
            f.Key.EndsWith("IMarkerData.cs", StringComparison.OrdinalIgnoreCase)).Value;
        // Interface must have InterfaceToClassConverter for deserialization
        Assert.Contains("InterfaceToClassConverter<IMarkerData, MarkerData>", iMarkerData);
        Assert.Contains("public interface IMarkerData", iMarkerData);

        var markerData = files.First(f =>
            f.Key.EndsWith("/MarkerData.cs", StringComparison.OrdinalIgnoreCase)
            || f.Key == "MarkerData.cs").Value;
        // Concrete class must be sealed and implement the interface
        Assert.Contains("public sealed class MarkerData : IMarkerData", markerData);
        // Must have mutable properties (SerializationContractTests sets them via object initializer)
        Assert.Matches(PropertyGetSetPattern(), markerData);

        // === CompletionItem interface (no I-prefix, so no concrete class pairing) ===
        // In the Monaco model, CompletionItem is an interface without the I-prefix convention.
        // The emitter emits it as a plain interface. The real repo's CompletionItem is hand-tuned
        // with a constructor, so it's on the ignore list. This test validates the emitter's
        // interface output has the expected properties for wire-format compatibility.
        var completionItem = files.First(f =>
            (f.Key.EndsWith("/CompletionItem.cs", StringComparison.OrdinalIgnoreCase)
             || f.Key == "CompletionItem.cs")
            && !f.Key.Contains("CompletionItemK", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemI", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemL", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemP", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemR", StringComparison.OrdinalIgnoreCase)
            && !f.Key.Contains("CompletionItemT", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("public interface CompletionItem", completionItem);
        Assert.Contains("Label", completionItem);
        Assert.Contains("InsertText", completionItem);
        Assert.Contains("Kind", completionItem);

        // === IRange -> Range serialization chain ===
        var iRange = files.First(f =>
            f.Key.EndsWith("IRange.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("InterfaceToClassConverter<IRange, Range>", iRange);

        // === Numeric enum: MarkerSeverity (no string converter) ===
        var markerSeverity = files.First(f =>
            f.Key.EndsWith("MarkerSeverity.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.DoesNotContain("JsonStringEnumConverter", markerSeverity);
        Assert.DoesNotContain("EnumMember", markerSeverity);
        Assert.Contains("public enum MarkerSeverity", markerSeverity);

        // === String enum: BuiltinTheme (with converter + EnumMember) ===
        var builtinTheme = files.First(f =>
            f.Key.EndsWith("BuiltinTheme.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("JsonConverter(typeof(JsonStringEnumConverter<BuiltinTheme>))", builtinTheme);
        Assert.Contains("JsonStringEnumMemberName(", builtinTheme);
        Assert.Contains("EnumMember(Value =", builtinTheme);
        Assert.Contains("public enum BuiltinTheme", builtinTheme);
    }

    /// <summary>
    /// Verifies that all emitted files from the full model are syntactically valid C#
    /// (contain namespace declarations and no obviously broken constructs).
    /// </summary>
    [Fact]
    public void AllEmittedFiles_HaveValidStructure()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        Assert.NotEmpty(files);

        foreach (var (path, content) in files)
        {
            // Every file must have the auto-generated header
            Assert.True(content.Contains("// <auto-generated />"),
                $"File '{path}' is missing auto-generated header");

            // Every file must have a namespace declaration
            Assert.True(content.Contains("namespace "),
                $"File '{path}' is missing namespace declaration");

            // Every file must have balanced braces
            var openBraces = content.Count(c => c == '{');
            var closeBraces = content.Count(c => c == '}');
            Assert.True(openBraces == closeBraces,
                $"File '{path}' has unbalanced braces: {openBraces} open vs {closeBraces} close");
        }
    }

    [GeneratedRegex(@"\{ get; set; \}")]
    private static partial Regex PropertyGetSetPattern();
}
