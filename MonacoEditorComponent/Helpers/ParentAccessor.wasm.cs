using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using System.Xml.Linq;
using Uno;
using Uno.Extensions;
using Uno.Extensions.Specialized;

namespace Monaco.Helpers;

partial class ParentAccessor
{
    private static ConditionalWeakTable<object, ParentAccessor> _instances = new();

    partial void PartialCtor()
    {
        if (this.parent.TryGetTarget(out var target))
        {
            _instances.Add(target, this);
        }
    }

    [JSExport]
    internal static void ManagedSetValue([JSMarshalAs<JSType.Any>] object managedOwner, string name, string value)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            var json = Desanitize(value) ?? "";
            json = json.Replace(@"\\",@"\");
            json = json.Trim('"');
            json = json.Replace(@"\r\n", Environment.NewLine);
            json = json.Replace(@"\t", "\t");
            System.Diagnostics.Debug.WriteLine($"Trimmed: {json}");
            _ = logger.SetValue(name, value);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
        }
    }

    [JSExport]
    internal static void ManagedSetValueWithType([JSMarshalAs<JSType.Any>] object managedOwner, string name, string value, string type)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            var json = Desanitize(value) ?? "";
            json = json.Replace(@"\\", @"\");
            json = json.Trim('"', ' ');
            json = json.Replace(@"\r\n", Environment.NewLine);
            json = json.Replace(@"\t", "\t");
            System.Diagnostics.Debug.WriteLine($"Trimmed: {json}");
            _ = logger.SetValue(name, json, type);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
        }
    }

    public static string? Santize(string? jsonString)
    {
        if (jsonString == null) return null;

        var replacements = @"%&\""'{}:,";
        for (var i = 0; i < replacements.Length; i++)
        {
            jsonString = jsonString.Replace(replacements[i]+"", "%" + (int)replacements[i]);
        }
        return jsonString;
    }

    [JSExport]
    internal static string ManagedGetJsonValue([JSMarshalAs<JSType.Any>] object managedOwner, string name)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            var json = logger.GetJsonValue(name);
            return Santize(json) ?? "";
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
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
    internal static bool ManagedCallActionWithParameters([JSMarshalAs<JSType.Any>] object managedOwner, string name, string parameter1, string parameter2)
    {
        if (_instances.TryGetValue(managedOwner, out var logger))
        {
            //System.Diagnostics.Debug.WriteLine($"Calling action {name}");

            var parameters = new[] { Desanitize(parameter1) ?? "", Desanitize(parameter2) ?? "" }.ToArray();
            var result = logger.CallActionWithParameters(name, parameters);

            return result;
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
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
            Console.WriteLine($"ManagedCallEvent({managedOwner}/{managedOwner.GetHashCode():x8}, {name}, {string.Join(", ", parameters)}");
            var resultString = await logger.CallEvent(name, parameters.Where(p => p is not null).ToArray());
            return Desanitize(resultString);
        }
        else
        {
            throw new InvalidOperationException($"ParentAccessor not found for owner {managedOwner}");
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
