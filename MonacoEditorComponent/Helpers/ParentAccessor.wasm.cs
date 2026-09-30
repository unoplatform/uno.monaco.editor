using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

using Uno.Extensions.Specialized;

namespace Monaco.Helpers;

partial class ParentAccessor
{
    private static readonly ConditionalWeakTable<object, ParentAccessor> _instances = [];

    partial void PartialCtor(ICodeEditorPresenter parent)
    {
        _instances.AddOrUpdate(parent, this);

        Console.WriteLine($"ParentAccessor ctor {parent.GetType()}/{parent.GetHashCode():X8}");
    }

    /// <summary>
    /// Removes the registration for the given presenter, allowing safe re-initialization.
    /// </summary>
    internal static void RemoveInstance(ICodeEditorPresenter presenter)
    {
        _instances.Remove(presenter);
    }

    /// <summary>
    /// JSExport entry point for the raw-text property writes: the document (<c>Text</c>,
    /// <c>ModifiedText</c>) reported by the content listener and the <c>SelectedText</c>
    /// reported by the selection listener in <c>asyncCallbackHelpers.ts</c>.
    /// </summary>
    /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
    /// <param name="name">The property name on the editor control.</param>
    /// <param name="value">The editor's text, percent-encoded by <c>stringifyForMarshalling</c>.</param>
    /// <remarks>
    /// <paramref name="value"/> is the model's text with only the bridge's transport encoding
    /// applied -- it is not a JSON string literal -- so the only thing to undo here is that
    /// encoding. This method used to also trim quotation marks, halve doubled backslashes and
    /// turn a literal <c>\t</c> or <c>\r\n</c> into the control characters. That went unnoticed
    /// while the write was dropped on the presenter; once it reaches the editor's properties,
    /// each of those alters the host's copy of the document, so a consumer saving <c>Text</c>
    /// would save different source from what Monaco displays. The desktop bridge
    /// (<c>ParentAccessorDesktop.OnSetValue</c>) has always passed the string through untouched;
    /// decoding only the transport keeps both bridges reporting the same text. JSON-carrying
    /// values take <see cref="ManagedSetValueWithType"/> instead.
    /// </remarks>
    [JSExport]
    internal static void ManagedSetValue([JSMarshalAs<JSType.Any>] object managedOwner, string name, string value)
    {
        if (_instances.TryGetValue(managedOwner, out var parentAccessor))
        {
            var text = BridgeEncoding.DecodeTransport(value) ?? "";
            _ = parentAccessor.SetValue(name, text);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetType()}/{managedOwner?.GetHashCode():X8}");
        }
    }

    /// <summary>
    /// JSExport entry point for the typed property writes, where <paramref name="value"/> is a
    /// percent-encoded JSON document deserialized as <paramref name="type"/> (for example the
    /// <c>Selection</c> behind <c>SelectedRange</c>). Distinct from <see cref="ManagedSetValue"/>,
    /// which carries raw text and must not be normalized.
    /// </summary>
    /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
    /// <param name="name">The property name on the editor control.</param>
    /// <param name="value">The JSON payload, percent-encoded by <c>stringifyForMarshalling</c>.</param>
    /// <param name="type">The registered type name to deserialize the JSON as.</param>
    [JSExport]
    internal static void ManagedSetValueWithType([JSMarshalAs<JSType.Any>] object managedOwner, string name, string value, string type)
    {
        if (_instances.TryGetValue(managedOwner, out var parentAccessor))
        {
            var json = Desanitize(value) ?? "";
            json = json.Replace(@"\\", @"\");
            json = json.Trim('"', ' ');
            json = json.Replace(@"\r\n", Environment.NewLine);
            json = json.Replace(@"\t", "\t");
            System.Diagnostics.Debug.WriteLine($"Trimmed: {json}");
            _ = parentAccessor.SetValue(name, json, type);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetType()}/{managedOwner?.GetHashCode():X8}");
        }
    }

    /// <summary>
    /// Encodes special characters in a JSON string for safe transport through the WASM bridge.
    /// </summary>
    /// <param name="jsonString">The JSON string to sanitize, or <see langword="null"/>.</param>
    /// <returns>The encoded string, or <see langword="null"/> if the input was <see langword="null"/>.</returns>
    public static string? Santize(string? jsonString) => BridgeEncoding.Sanitize(jsonString);

    [JSExport]
    internal static string ManagedGetJsonValue([JSMarshalAs<JSType.Any>] object managedOwner, string name)
    {
        if (_instances.TryGetValue(managedOwner, out var parentAccessor))
        {
            var json = parentAccessor.GetJsonValue(name);
            return Santize(json) ?? "";
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetType()}/{managedOwner?.GetHashCode():X8}");
        }
    }

    [JSExport]
    internal static bool ManagedCallAction([JSMarshalAs<JSType.Any>] object managedOwner, string name)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            var result = logger.CallAction(name);

            return result;
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
        }
    }

    [JSExport]
    internal static bool ManagedCallActionWithParameters([JSMarshalAs<JSType.Any>] object managedOwner, string name, string[] parameters)
    {
        if (_instances.TryGetValue(managedOwner, out var parentAccessor))
        {
            //System.Diagnostics.Debug.WriteLine($"Calling action {name}");

            var sanitizedParameters = parameters.Select(p => Desanitize(p) ?? "").ToArray();
            var result = parentAccessor.CallActionWithParameters(name, sanitizedParameters);

            return result;
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetType()}/{managedOwner?.GetHashCode():X8}");
        }
    }

    private static string? Desanitize(string? parameter) => BridgeEncoding.Desanitize(parameter);

    /// <summary>
    /// JSExport entry point: invokes a registered event callback for the specified presenter owner.
    /// </summary>
    /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
    /// <param name="name">The event name.</param>
    /// <param name="parameters">The sanitized JSON parameter strings.</param>
    /// <returns>The desanitized result string, or <see langword="null"/>.</returns>
    [JSExport]
    public static async Task<string?> ManagedCallEvent([JSMarshalAs<JSType.Any>] object managedOwner, string name, string[] parameters)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            var resultString = await logger.CallEvent(name, [.. parameters.Select(s => Desanitize(s) ?? "").Where(p => p is not null)]);
            return Desanitize(resultString);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetHashCode():X8}");
        }
    }

    /// <summary>
    /// JSExport entry point: disposes the <see cref="ParentAccessor"/> for the specified owner.
    /// </summary>
    /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
    [JSExport]
    public static void ManagedClose([JSMarshalAs<JSType.Any>] object managedOwner)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            logger.Dispose();
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
        }
    }
}
