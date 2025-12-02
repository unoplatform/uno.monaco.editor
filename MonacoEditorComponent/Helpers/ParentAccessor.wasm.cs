using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

using Uno.Extensions.Specialized;

namespace Monaco.Helpers;

partial class ParentAccessor
{
    private static readonly ConditionalWeakTable<object, ParentAccessor> _instances = [];

    partial void PartialCtor(ICodeEditorPresenter parent)
    {
        _instances.Add(parent, this);

        Console.WriteLine($"ParentAccessor ctor {parent.GetType()}/{parent.GetHashCode():X8}");
    }

    [JSExport]
    internal static void ManagedSetValue([JSMarshalAs<JSType.Any>] object managedOwner, string name, string value)
    {
        if (_instances.TryGetValue(managedOwner, out var parentAccessor))
        {
            var json = Desanitize(value) ?? "";
            json = json.Replace(@"\\", @"\");
            json = json.Trim('"');
            json = json.Replace(@"\r\n", Environment.NewLine);
            json = json.Replace(@"\t", "\t");
            System.Diagnostics.Debug.WriteLine($"Trimmed: {json}");
            _ = parentAccessor.SetValue(name, value);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner?.GetType()}/{managedOwner?.GetHashCode():X8}");
        }
    }

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

    public static string? Santize(string? jsonString)
    {
        if (jsonString == null) return null;

        var replacements = @"%&\""'{}:,";
        for (var i = 0; i < replacements.Length; i++)
        {
            jsonString = jsonString.Replace(replacements[i] + "", "%" + (int)replacements[i]);
        }
        return jsonString;
    }

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

    private static string? Desanitize(string? parameter)
    {
        // System.Diagnostics.Debug.WriteLine($"Encoded String: {parameter}");
        if (parameter == null) return parameter;
        var replacements = @"&\""'{}:,%";
        // System.Diagnostics.Debug.WriteLine($"Replacements: >{replacements}<");
        for (int i = 0; i < replacements.Length; i++)
        {
            //   System.Diagnostics.Debug.WriteLine($"Replacing: >%{(int)replacements[i]}< with >{(char)replacements[i] + "" }< ");
            parameter = parameter.Replace($"%{(int)replacements[i]}", (char)replacements[i] + "");
        }

        parameter = parameter.Replace(@"\\""", @"""");

        // System.Diagnostics.Debug.WriteLine($"Decoded String: {parameter}");
        return parameter;
    }

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
