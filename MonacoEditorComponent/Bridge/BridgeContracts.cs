using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monaco.Bridge;

// ============================================================
// JSON-RPC DTO types matching bridge-protocol.md parameter schemas.
// Only types used for C#-to-JS serialization are retained here.
// JS-to-C# methods use individual named parameters (not DTOs)
// to match StreamJsonRpc named-params dispatch behavior.
// ============================================================

// --- C# to JS Notifications ---

/// <summary>Parameters for editor/lifecycleUpdate notification.</summary>
public record LifecycleUpdateParams(int Loading, int Loaded);

// ============================================================
// AOT-friendly source-generated JsonSerializerContext.
// Covers DTO types used for C#-to-JS bridge serialization
// and primitive types used by StreamJsonRpc named-params dispatch.
// ============================================================

[JsonSerializable(typeof(LifecycleUpdateParams))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class BridgeSerializerContext : JsonSerializerContext;
