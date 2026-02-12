#nullable enable

using Xunit;

namespace MonacoTypeEmitter.Tests;

/// <summary>
/// Snapshot tests that verify the emitter produces deterministic, expected C# output
/// for known input fragments. Each test compares emitter output against a checked-in
/// .verified.cs baseline file.
/// </summary>
[Trait("Category", "Snapshot")]
public class SnapshotTests
{
    [Fact]
    public void StringEnum_CursorStyle()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("string-enum.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce exactly one file: Editor/CursorStyle.cs
        Assert.Single(files);
        Assert.True(files.ContainsKey("Editor/CursorStyle.cs"),
            $"Expected 'Editor/CursorStyle.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("StringEnum_CursorStyle", files["Editor/CursorStyle.cs"]);
    }

    [Fact]
    public void NumericEnum_MarkerSeverity()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("numeric-enum.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        Assert.Single(files);
        Assert.True(files.ContainsKey("MarkerSeverity.cs"),
            $"Expected 'MarkerSeverity.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("NumericEnum_MarkerSeverity", files["MarkerSeverity.cs"]);
    }

    [Fact]
    public void Interface_IPosition()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("interface-with-properties.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce IPosition.cs (interface) + Position.cs (concrete class)
        Assert.Equal(2, files.Count);
        Assert.True(files.ContainsKey("IPosition.cs"),
            $"Expected 'IPosition.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("Interface_IPosition", files["IPosition.cs"]);
    }

    [Fact]
    public void ConcreteClass_Position()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("interface-with-properties.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        Assert.True(files.ContainsKey("Position.cs"),
            $"Expected 'Position.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("ConcreteClass_Position", files["Position.cs"]);
    }

    [Fact]
    public void ModelClass_Uri()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("model-class.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        Assert.Single(files);
        Assert.True(files.ContainsKey("Uri.cs"),
            $"Expected 'Uri.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("ModelClass_Uri", files["Uri.cs"]);
    }

    [Fact]
    public void NamespaceHierarchy_CrossNamespaceUsing()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("namespace-hierarchy.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce files across namespaces: MarkerSeverity.cs (root) and
        // Editor/IMarkerData.cs + Editor/MarkerData.cs (sub-namespace)
        Assert.True(files.Count >= 3,
            $"Expected at least 3 files but got {files.Count}: {string.Join(", ", files.Keys)}");

        // Verify the interface in the sub-namespace has a cross-namespace using
        Assert.True(files.ContainsKey("Editor/IMarkerData.cs"),
            $"Expected 'Editor/IMarkerData.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("NamespaceHierarchy_CrossNamespaceUsing", files["Editor/IMarkerData.cs"]);
    }

    [Fact]
    public void DollarPrefix_IJSONSchema()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("dollar-prefix-properties.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce IJSONSchema.cs (interface) + JSONSchema.cs (concrete class)
        Assert.Equal(2, files.Count);
        Assert.True(files.ContainsKey("Languages/Json/IJSONSchema.cs"),
            $"Expected 'Languages/Json/IJSONSchema.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("DollarPrefix_IJSONSchema", files["Languages/Json/IJSONSchema.cs"]);
    }

    [Fact]
    public void DollarPrefix_JSONSchema()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("dollar-prefix-properties.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        Assert.True(files.ContainsKey("Languages/Json/JSONSchema.cs"),
            $"Expected 'Languages/Json/JSONSchema.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("DollarPrefix_JSONSchema", files["Languages/Json/JSONSchema.cs"]);
    }

    [Fact]
    public void DottedIdentifier_IGlobalEditorOptions()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("dotted-identifier-property.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce IGlobalEditorOptions.cs + GlobalEditorOptions.cs
        Assert.Equal(2, files.Count);
        Assert.True(files.ContainsKey("Editor/IGlobalEditorOptions.cs"),
            $"Expected 'Editor/IGlobalEditorOptions.cs' but got: {string.Join(", ", files.Keys)}");

        SnapshotAssert.MatchesVerified("DottedIdentifier_IGlobalEditorOptions", files["Editor/IGlobalEditorOptions.cs"]);
    }

    [Fact]
    public void TypePredicate_Position()
    {
        var model = EmitterTestHelper.LoadModel(
            EmitterTestHelper.GetTestInputPath("type-predicate-return.json"));
        var files = EmitterTestHelper.EmitToMemory(model);

        // Should produce Position.cs (class with type predicate method)
        Assert.Single(files);
        Assert.True(files.ContainsKey("Position.cs"),
            $"Expected 'Position.cs' but got: {string.Join(", ", files.Keys)}");

        // Verify type predicate maps to bool, not raw "obj is IPosition"
        var content = files["Position.cs"];
        Assert.Contains("bool IsIPosition", content);
        Assert.DoesNotContain("obj is IPosition IsIPosition", content);

        SnapshotAssert.MatchesVerified("TypePredicate_Position", files["Position.cs"]);
    }
}
