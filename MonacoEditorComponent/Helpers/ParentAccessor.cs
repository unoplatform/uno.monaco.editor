using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;

using Newtonsoft.Json;

using System.Reflection;

using Windows.Foundation.Metadata;

namespace Monaco.Helpers
{
    /// <summary>
    /// Class to aid in accessing WinRT values from JavaScript.
    /// Not Thread Safe.
    /// </summary>
    [AllowForWeb]
    public sealed partial class ParentAccessor : IDisposable
    {
        private readonly WeakReference<ICodeEditorPresenter> parent;
        private readonly Type typeinfo;
        private readonly DispatcherQueue _queue;
        private Dictionary<string, Action>? actions;
        private readonly Dictionary<string, Action<string[]>> action_parameters;
        private Dictionary<string, Func<string[], Task<string>?>>? events;

        private List<Assembly> Assemblies { get; set; } = [];

        /// <summary>
        /// Constructs a new reflective parent Accessor for the provided object.
        /// </summary>
        /// <param name="parent">Object to provide Property Access.</param>
        public ParentAccessor(ICodeEditorPresenter parent, DispatcherQueue queue)
        {
            _queue = queue;

            this.parent = new WeakReference<ICodeEditorPresenter>(parent);
            typeinfo = parent.GetType();
            actions = [];
            action_parameters = [];
            events = [];

            PartialCtor(parent);
        }

        partial void PartialCtor(ICodeEditorPresenter parent);

        /// <summary>
        /// Registers an action from the .NET side which can be called from within the JavaScript code.
        /// </summary>
        /// <param name="name">String Key.</param>
        /// <param name="action">Action to perform.</param>
        internal void RegisterAction(string name, Action action)
        {
            actions?[name] = action;
        }

        internal void RegisterActionWithParameters(string name, Action<string[]> action)
        {
            action_parameters[name] = action;
        }

        /// <summary>
        /// Registers an event from the .NET side which can be called with the given jsonified string arguments within the JavaScript code.
        /// </summary>
        /// <param name="name">String Key.</param>
        /// <param name="function">Event to call.</param>
        internal void RegisterEvent(string name, Func<string[], Task<string>?> function)
        {
            events?[name] = function;
        }

        /// <summary>
        /// Calls an Event registered before with the <see cref="RegisterEvent(string, Func{string[], Task{string}})"/>.
        /// </summary>
        /// <param name="name">Name of event to call.</param>
        /// <param name="parameters">JSON string Parameters.</param>
        /// <returns></returns>
        public async Task<string?> CallEvent(string name, string[] parameters)
        {
            string? result = null;

            await _queue.EnqueueAsync(async () =>
            {
                if (events is not null
                    && events.TryGetValue(name, out Func<string[], Task<string>?>? value))
                {
                    var task = value?.Invoke(parameters);
                    if (task != null)
                    {
                        result = await task;
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// Adds an Assembly to use for looking up types by name for <see cref="SetValue(string, string, string)"/>.
        /// </summary>
        /// <param name="assembly">Assembly to add.</param>
        internal void AddAssemblyForTypeLookup(Assembly assembly)
        {
            Assemblies.Add(assembly);
        }

        /// <summary>
        /// Calls an Action registered before with <see cref="RegisterAction(string, Action)"/>.
        /// </summary>
        /// <param name="name">String Key.</param>
        /// <returns>True if method was found in registration.</returns>
        public bool CallAction(string name)
        {
            if (actions is not null &&
                actions.TryGetValue(name, out Action? value))
            {
                // TODO: Not sure if this a problem too?
                _queue.EnqueueAsync(() =>
                {
                    value?.Invoke();
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calls an Action registered before with <see cref="RegisterActionWithParameters(string, Action{string[]})"/>.
        /// </summary>
        /// <param name="name">String Key.</param>
        /// <param name="parameters">Parameters to be passed to Action.</param>
        /// <returns>True if method was found in registration.</returns>
        public bool CallActionWithParameters(string name, string[] parameters)
        {
            if (action_parameters.TryGetValue(name, out Action<string[]>? value))
            {
                _queue.EnqueueAsync(() =>
                {
                    value?.Invoke(parameters);
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the winrt primative object value for the specified Property.
        /// </summary>
        /// <param name="name">Property name on Parent Object.</param>
        /// <returns>Property Value or null.</returns>
        public async Task<object?> GetValue(string name)
        {
            object? result = null;

            await _queue.EnqueueAsync(() =>
            {
                if (parent.TryGetTarget(out var tobj))
                {
                    var propinfo = typeinfo.GetProperty(name);
                    result = propinfo?.GetValue(tobj);
                }
            });

            return result;
        }

        public string GetJsonValue(string name)
        {
            if (parent.TryGetTarget(out var tobj))
            {
                var propinfo = typeinfo.GetProperty(name);
                var obj = propinfo?.GetValue(tobj);

                return JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            }
            return "{}";
        }

        /// <summary>
        /// Returns the winrt primative object value for a child property off of the specified Property.
        /// 
        /// Useful for providing complex types to users of Parent but still access primatives in JavaScript.
        /// </summary>
        /// <param name="name">Parent Property name.</param>
        /// <param name="child">Property's Property name to retrieve.</param>
        /// <returns>Value of Child Property or null.</returns>
        public async Task<object?> GetChildValue(string name, string child)
        {
            object? result = null;

            await _queue.EnqueueAsync(() =>
            {
                if (parent.TryGetTarget(out var tobj))
                {
                    // TODO: Support params for multi-level digging?
                    var propinfo = typeinfo.GetProperty(name);
                    var prop = propinfo?.GetValue(tobj);
                    if (prop != null)
                    {
                        var childinfo = prop.GetType().GetProperty(child);
                        result = childinfo?.GetValue(prop);
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// Sets the value for the specified Property.
        /// </summary>
        /// <param name="name">Parent Property name.</param>
        /// <param name="value">Value to set.</param>
        public async Task SetValue(string name, object newValue)
        {
            await _queue.EnqueueAsync(() =>
            {
                if (parent.TryGetTarget(out var tobj))
                {
                    var propinfo = typeinfo.GetProperty(name); // TODO: Cache these?
                    tobj.IsSettingValue = true;

                    try
                    {
                        object? value = newValue;

                        if (value is string valueAsString)
                        {
                            value = Desanitize(valueAsString);
                        }

                        propinfo?.SetValue(tobj, newValue);
                    }
                    finally
                    {
                        tobj.IsSettingValue = false;
                    }
                }
            });
        }

        /// <summary>
        /// Sets the value for the specified Property after deserializing the value as the given type name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        public async Task SetValue(string name, string newValue, string type)
        {
            await _queue.EnqueueAsync(() =>
            {
                if (parent.TryGetTarget(out var tobj))
                {
                    var propinfo = typeinfo.GetProperty(name);
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

        private Type? LookForTypeByName(string name)
        {
            // First search locally
            var result = Type.GetType(name);

            if (result != null)
            {
                return result;
            }

            // Search in Other Assemblies
            foreach (var assembly in Assemblies)
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

        public void Dispose()
        {
            actions?.Clear();

            actions = null;

            events?.Clear();

            events = null;
        }
    }

    //// TODO: Find better approach than this. Issue #21.
    /// <summary>
    /// Interface used on objects to be accessed.
    /// </summary>
    public interface IParentAccessorAcceptor
    {
        /// <summary>
        /// Property to tell object the value is being set by ParentAccessor.
        /// </summary>
        bool IsSettingValue { get; set; }
    }
}
