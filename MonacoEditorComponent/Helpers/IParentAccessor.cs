using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Monaco.Helpers
{
    /// <summary>
    /// Interface for parent accessor implementations.
    /// Provides property access and action/event registration for the bridge layer.
    /// WASM uses the concrete ParentAccessor with JSExport.
    /// Desktop uses a JsonRpc-based variant.
    /// </summary>
    internal interface IParentAccessor : IDisposable
    {
        /// <summary>
        /// Registers an action from the .NET side which can be called from within the JavaScript code.
        /// </summary>
        void RegisterAction(string name, Action action);

        /// <summary>
        /// Registers an action with parameters from the .NET side.
        /// </summary>
        void RegisterActionWithParameters(string name, Action<string[]> action);

        /// <summary>
        /// Registers an event from the .NET side which can be called with the given jsonified string arguments.
        /// </summary>
        void RegisterEvent(string name, Func<string[], Task<string>?> function);

        /// <summary>
        /// Registers a <see cref="JsonTypeInfo"/> for AOT-safe deserialization in SetValue.
        /// </summary>
        void RegisterTypeInfo(string name, JsonTypeInfo info);

        /// <summary>
        /// Obsolete: Assembly scanning is no longer used. Use <see cref="RegisterTypeInfo"/> instead.
        /// </summary>
        [Obsolete("Use RegisterTypeInfo instead. Assembly scanning is not AOT-compatible.")]
        void AddAssemblyForTypeLookup(Assembly assembly);

        /// <summary>
        /// Returns the value for the specified Property.
        /// </summary>
        Task<object?> GetValue(string name);

        /// <summary>
        /// Returns the JSON-serialized value for the specified Property.
        /// </summary>
        string GetJsonValue(string name);

        /// <summary>
        /// Returns the value of a child property off of the specified Property.
        /// </summary>
        Task<object?> GetChildValue(string name, string child);

        /// <summary>
        /// Sets the value for the specified Property.
        /// </summary>
        Task SetValue(string name, object newValue);

        /// <summary>
        /// Sets the value for the specified Property after deserializing as the given type.
        /// </summary>
        Task SetValue(string name, string newValue, string type);

        /// <summary>
        /// Calls an Action registered before with RegisterAction.
        /// </summary>
        bool CallAction(string name);

        /// <summary>
        /// Calls an Action registered before with RegisterActionWithParameters.
        /// </summary>
        bool CallActionWithParameters(string name, string[] parameters);

        /// <summary>
        /// Calls an Event registered before with RegisterEvent.
        /// </summary>
        Task<string?> CallEvent(string name, string[] parameters);
    }
}
