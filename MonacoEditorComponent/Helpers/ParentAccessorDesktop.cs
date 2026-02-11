using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;

using Monaco.Bridge;

using Newtonsoft.Json;

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
    private List<Assembly> _assemblies = [];

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

    public void AddAssemblyForTypeLookup(Assembly assembly)
    {
        _assemblies.Add(assembly);
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
        if (_parent.TryGetTarget(out var tobj))
        {
            var propinfo = _typeinfo.GetProperty(name);
            var obj = propinfo?.GetValue(tobj);

            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        return "{}";
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
                var typeobj = LookForTypeByName(type);

                if (typeobj is not null)
                {
                    var obj = JsonConvert.DeserializeObject(newValue, typeobj);

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
    public async Task OnSetValue(SetValueParams p)
    {
        // Extract the string value from the JsonElement.
        var value = ExtractStringValue(p.Value);
        await SetValue(p.Name, value);
    }

    [JsonRpcMethod("parentAccessor/setValueWithType")]
    public async Task OnSetValueWithType(SetValueWithTypeParams p)
    {
        var value = ExtractStringValue(p.Value);
        await SetValue(p.Name, value, p.TypeName);
    }

    [JsonRpcMethod("parentAccessor/callAction")]
    public void OnCallAction(CallActionParams p)
    {
        CallAction(p.Name);
    }

    [JsonRpcMethod("parentAccessor/callActionWithParameters")]
    public void OnCallActionWithParameters(CallActionWithParametersParams p)
    {
        var parameters = ConvertJsonElementToStringArray(p.Parameters);
        CallActionWithParameters(p.Name, parameters);
    }

    [JsonRpcMethod("parentAccessor/callEvent")]
    public async Task<string?> OnCallEvent(CallEventParams p)
    {
        var parameters = ConvertJsonElementToStringArray(p.Parameters);
        return await CallEvent(p.Name, parameters);
    }

    [JsonRpcMethod("parentAccessor/getJsonValue")]
    public string OnGetJsonValue(GetJsonValueParams p)
    {
        return GetJsonValue(p.Name);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private Type? LookForTypeByName(string name)
    {
        var result = Type.GetType(name);
        if (result is not null) return result;

        foreach (var assembly in _assemblies)
        {
            foreach (var typeInfo in assembly.ExportedTypes)
            {
                if (typeInfo.Name == name)
                {
                    return typeInfo;
                }
            }
        }

        return null;
    }

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
}
