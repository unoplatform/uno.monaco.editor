using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;

using Monaco.Serialization;

using StreamJsonRpc;

namespace Monaco.Helpers;

/// <summary>
/// Desktop implementation of <see cref="IParentAccessor"/> that receives
/// JSON-RPC notifications/requests from JavaScript and dispatches them
/// to the same action/event/property infrastructure as the WASM variant.
/// Registered as a local RPC target on the shared <see cref="JsonRpc"/> instance.
/// </summary>
internal sealed class ParentAccessorDesktop : IParentAccessor
{
    private readonly WeakReference<ICodeEditorPresenter> _parent;
    private readonly Type _typeinfo;
    private readonly DispatcherQueue _queue;
    private Dictionary<string, Action>? _actions;
    private readonly Dictionary<string, Action<string[]>> _actionParameters;
    private Dictionary<string, Func<string[], Task<string>?>>? _events;

    /// <summary>
    /// AOT-safe type info lookup for <see cref="SetValue(string, string, string)"/>.
    /// Keyed by both fully-qualified name and short name for backward compatibility.
    /// Thread-safe: may be read during SetValue while written via RegisterTypeInfo.
    /// </summary>
    private readonly ConcurrentDictionary<string, JsonTypeInfo> _typeInfoMap;

    public ParentAccessorDesktop(ICodeEditorPresenter parent, DispatcherQueue queue)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(queue);

        _parent = new WeakReference<ICodeEditorPresenter>(parent);
        _typeinfo = parent.GetType();
        _queue = queue;
        _actions = [];
        _actionParameters = [];
        _events = [];
        _typeInfoMap = MonacoJsonContext.BuildTypeInfoMap();
    }

    // ============================================================
    // IParentAccessor implementation
    // ============================================================

    public void RegisterAction(string name, Action action)
    {
        _actions?[name] = action;
    }

    public void RegisterActionWithParameters(string name, Action<string[]> action)
    {
        _actionParameters[name] = action;
    }

    public void RegisterEvent(string name, Func<string[], Task<string>?> function)
    {
        _events?[name] = function;
    }

    /// <summary>
    /// Registers a <see cref="JsonTypeInfo"/> for AOT-safe deserialization in
    /// <see cref="SetValue(string, string, string)"/>.
    /// </summary>
    /// <param name="name">Type name key (fully-qualified or short name).</param>
    /// <param name="info">The <see cref="JsonTypeInfo"/> to register.</param>
    public void RegisterTypeInfo(string name, JsonTypeInfo info)
    {
        _typeInfoMap[name] = info;
    }

    /// <summary>
    /// Obsolete: Assembly scanning is no longer used. Use <see cref="RegisterTypeInfo"/> instead.
    /// </summary>
    [Obsolete("Use RegisterTypeInfo instead. Assembly scanning is not AOT-compatible.")]
    public void AddAssemblyForTypeLookup(Assembly assembly)
    {
        // No-op: retained for API compatibility during migration.
    }

    public async Task<object?> GetValue(string name)
    {
        object? result = null;

        await _queue.EnqueueAsync(() =>
        {
            if (_parent.TryGetTarget(out var tobj))
            {
                var propinfo = _typeinfo.GetProperty(name);
                result = propinfo?.GetValue(tobj);
            }
        });

        return result;
    }

    public string GetJsonValue(string name)
    {
        // Synchronous interface implementation. On desktop, callers should use
        // GetJsonValueAsync to ensure UI thread dispatch. This exists for
        // interface compliance only; direct callers on desktop should not use it.
        if (_parent.TryGetTarget(out var tobj))
        {
            var propinfo = _typeinfo.GetProperty(name);
            var obj = propinfo?.GetValue(tobj);

            if (obj is null)
            {
                return "null";
            }

            return SerializePropertyValue(obj);
        }

        return "null";
    }

    /// <summary>
    /// Async version of <see cref="GetJsonValue"/> that dispatches through the
    /// <see cref="DispatcherQueue"/> to ensure DependencyProperty access occurs
    /// on the UI thread. Called by the JSON-RPC handler <see cref="OnGetJsonValue"/>.
    /// </summary>
    public async Task<string> GetJsonValueAsync(string name)
    {
        string result = "null";

        await _queue.EnqueueAsync(() =>
        {
            if (_parent.TryGetTarget(out var tobj))
            {
                var propinfo = _typeinfo.GetProperty(name);
                var obj = propinfo?.GetValue(tobj);

                if (obj is null)
                {
                    result = "null";
                    return;
                }

                result = SerializePropertyValue(obj);
            }
        });

        return result;
    }

    public async Task<object?> GetChildValue(string name, string child)
    {
        object? result = null;

        await _queue.EnqueueAsync(() =>
        {
            if (_parent.TryGetTarget(out var tobj))
            {
                var propinfo = _typeinfo.GetProperty(name);
                var prop = propinfo?.GetValue(tobj);
                if (prop is not null)
                {
                    var childinfo = prop.GetType().GetProperty(child);
                    result = childinfo?.GetValue(prop);
                }
            }
        });

        return result;
    }

    public async Task SetValue(string name, object newValue)
    {
        await _queue.EnqueueAsync(() =>
        {
            if (_parent.TryGetTarget(out var tobj))
            {
                var propinfo = _typeinfo.GetProperty(name);
                tobj.IsSettingValue = true;

                try
                {
                    // Desktop values arrive as clean JSON via JSON-RPC -- no Desanitize needed.
                    propinfo?.SetValue(tobj, newValue);
                }
                finally
                {
                    tobj.IsSettingValue = false;
                }
            }
        });
    }

    public async Task SetValue(string name, string newValue, string type)
    {
        await _queue.EnqueueAsync(() =>
        {
            if (_parent.TryGetTarget(out var tobj))
            {
                var propinfo = _typeinfo.GetProperty(name);

                if (!_typeInfoMap.TryGetValue(type, out var jsonTypeInfo))
                {
                    throw new InvalidOperationException(
                        $"Type '{type}' is not registered for deserialization. " +
                        "Register it in MonacoJsonContext or call RegisterTypeInfo.");
                }

                var obj = JsonSerializer.Deserialize(newValue, jsonTypeInfo);

                tobj.IsSettingValue = true;
                try
                {
                    propinfo?.SetValue(tobj, obj);
                }
                finally
                {
                    tobj.IsSettingValue = false;
                }
            }
        });
    }

    public bool CallAction(string name)
    {
        if (_actions is not null &&
            _actions.TryGetValue(name, out Action? value))
        {
            _queue.EnqueueAsync(() =>
            {
                value?.Invoke();
            });
            return true;
        }

        return false;
    }

    public bool CallActionWithParameters(string name, string[] parameters)
    {
        if (_actionParameters.TryGetValue(name, out Action<string[]>? value))
        {
            _queue.EnqueueAsync(() =>
            {
                value?.Invoke(parameters);
            });
            return true;
        }

        return false;
    }

    public async Task<string?> CallEvent(string name, string[] parameters)
    {
        string? result = null;

        await _queue.EnqueueAsync(async () =>
        {
            if (_events is not null
                && _events.TryGetValue(name, out Func<string[], Task<string>?>? value))
            {
                var task = value?.Invoke(parameters);
                if (task is not null)
                {
                    result = await task;
                }
            }
        });

        return result;
    }

    public void Dispose()
    {
        _actions?.Clear();
        _actions = null;
        _events?.Clear();
        _events = null;
    }

    // ============================================================
    // JSON-RPC target methods (registered via JsonRpc.AddLocalRpcTarget)
    // ============================================================

    [JsonRpcMethod("parentAccessor/setValue")]
    public async Task OnSetValue(string name, JsonElement value)
    {
        // Extract the string value from the JsonElement.
        var stringValue = ExtractStringValue(value);
        await SetValue(name, stringValue);
    }

    [JsonRpcMethod("parentAccessor/setValueWithType")]
    public async Task OnSetValueWithType(string name, JsonElement value, string typeName)
    {
        var stringValue = ExtractStringValue(value);
        await SetValue(name, stringValue, typeName);
    }

    [JsonRpcMethod("parentAccessor/callAction")]
    public void OnCallAction(string name)
    {
        DesktopCodeEditorPresenter.DiagnosticLog($"OnCallAction: name={name}");
        CallAction(name);
    }

    [JsonRpcMethod("parentAccessor/callActionWithParameters")]
    public void OnCallActionWithParameters(string name, JsonElement parameters)
    {
        var paramArray = ConvertJsonElementToStringArray(parameters);
        CallActionWithParameters(name, paramArray);
    }

    [JsonRpcMethod("parentAccessor/callEvent")]
    public async Task<string?> OnCallEvent(string name, JsonElement parameters)
    {
        var paramArray = ConvertJsonElementToStringArray(parameters);
        return await CallEvent(name, paramArray);
    }

    [JsonRpcMethod("parentAccessor/getJsonValue")]
    public async Task<string> OnGetJsonValue(string name)
    {
        DesktopCodeEditorPresenter.DiagnosticLog($"OnGetJsonValue: name={name}");
        try
        {
            var result = await GetJsonValueAsync(name);
            DesktopCodeEditorPresenter.DiagnosticLog($"OnGetJsonValue: name={name}, result={result}");
            return result;
        }
        catch (Exception ex)
        {
            DesktopCodeEditorPresenter.DiagnosticLog($"OnGetJsonValue: name={name}, error={ex}");
            throw;
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// Extracts a string value from a <see cref="JsonElement"/>.
    /// Desktop JSON-RPC delivers values as clean JSON -- no sanitization needed.
    /// </summary>
    internal static string ExtractStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText(),
        };
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a string array using deterministic mapping:
    /// Array: element-wise GetRawText(), String: single-element, Null/Undefined: empty,
    /// other: single-element GetRawText().
    /// </summary>
    internal static string[] ConvertJsonElementToStringArray(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => [.. element.EnumerateArray().Select(e => e.GetRawText())],
            JsonValueKind.String => [element.GetString() ?? string.Empty],
            JsonValueKind.Null or JsonValueKind.Undefined => [],
            _ => [element.GetRawText()],
        };
    }

    /// <summary>
    /// Serializes a property value to JSON. Tries the AOT-safe <see cref="MonacoJsonContext"/>
    /// first, then falls back to reflection-based serialization for framework types
    /// that are not registered in the source-generated context.
    /// </summary>
    /// <remarks>
    /// <c>ElementTheme</c> is now registered in <see cref="MonacoJsonContext"/> and will
    /// serialize via the source-generated path. The reflection fallback is retained as a
    /// safety net for any other framework types that may appear. Desktop runs as native
    /// code (not AOT-WASM), so the reflection fallback is safe.
    /// </remarks>
    private static string SerializePropertyValue(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, obj.GetType(), MonacoJsonContext.Relaxed.Options);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // Type not in source-generated context -- fall back to reflection serializer
            // with the same naming/escaping conventions.
            Debug.WriteLine($"ParentAccessorDesktop: Falling back to reflection serializer for type '{obj.GetType().FullName}'");
            return JsonSerializer.Serialize(obj, obj.GetType(), MonacoJsonContext.FallbackOptions);
        }
    }
}
