using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monaco.Bridge;

// ============================================================
// JSON-RPC DTO types matching bridge-protocol.md parameter schemas.
// Used by desktop bridge classes as typed parameter objects for
// StreamJsonRpc method dispatch. Source-generated serialization
// via BridgeSerializerContext ensures AOT compatibility.
// ============================================================

// --- JS to C# Notifications ---

/// <summary>Parameters for the bridge/ready notification sent at bundle load.</summary>
public record BridgeReadyParams(int ProtocolVersion);

/// <summary>Parameters for the editor/ready notification sent after Monaco creation.</summary>
public record EditorReadyParams(int ProtocolVersion);

/// <summary>Parameters for parentAccessor/setValue notification.</summary>
public record SetValueParams(string Name, JsonElement Value);

/// <summary>Parameters for parentAccessor/setValueWithType notification.</summary>
public record SetValueWithTypeParams(string Name, JsonElement Value, string TypeName);

/// <summary>Parameters for parentAccessor/callAction notification.</summary>
public record CallActionParams(string Name);

/// <summary>Parameters for parentAccessor/callActionWithParameters notification.</summary>
public record CallActionWithParametersParams(string Name, JsonElement Parameters);

/// <summary>Parameters for debug/log notification.</summary>
public record LogParams(string Level, string Message);

/// <summary>Parameters for keyboard/keyDown notification.</summary>
public record KeyDownParams(int KeyCode, bool CtrlKey, bool ShiftKey, bool AltKey, bool MetaKey);

// --- JS to C# Requests ---

/// <summary>Parameters for parentAccessor/callEvent request.</summary>
public record CallEventParams(string Name, JsonElement Parameters);

/// <summary>Parameters for parentAccessor/getJsonValue request.</summary>
public record GetJsonValueParams(string Name);

/// <summary>Parameters for theme/getProperty request.</summary>
public record GetThemePropertyParams(string Name);

// --- C# to JS Notifications ---

/// <summary>Parameters for editor/lifecycleUpdate notification.</summary>
public record LifecycleUpdateParams(int Loading, int Loaded);

// ============================================================
// AOT-friendly source-generated JsonSerializerContext.
// Covers all DTO types used by the JSON-RPC bridge layer.
// ============================================================

[JsonSerializable(typeof(BridgeReadyParams))]
[JsonSerializable(typeof(EditorReadyParams))]
[JsonSerializable(typeof(SetValueParams))]
[JsonSerializable(typeof(SetValueWithTypeParams))]
[JsonSerializable(typeof(CallActionParams))]
[JsonSerializable(typeof(CallActionWithParametersParams))]
[JsonSerializable(typeof(LogParams))]
[JsonSerializable(typeof(KeyDownParams))]
[JsonSerializable(typeof(CallEventParams))]
[JsonSerializable(typeof(GetJsonValueParams))]
[JsonSerializable(typeof(GetThemePropertyParams))]
[JsonSerializable(typeof(LifecycleUpdateParams))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class BridgeSerializerContext : JsonSerializerContext;
