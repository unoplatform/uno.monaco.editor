using System;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Represents a context key in the Monaco editor that can be used for conditional keybindings and menu items.
    /// </summary>
    public sealed class ContextKey : IContextKey
    {
        [JsonIgnore]
        private readonly WeakReference<EditorHostBase> _editor;

        /// <summary>
        /// Gets the unique key identifier for this context key.
        /// </summary>
        [JsonInclude]
        public string Key { get; internal set; }

        /// <summary>
        /// Gets the default value of this context key.
        /// </summary>
        [JsonInclude]
        public bool DefaultValue { get; internal set; }

        /// <summary>
        /// Gets or sets the current value of this context key.
        /// </summary>
        [JsonInclude]
        public bool Value { get; internal set; }

        internal ContextKey(EditorHostBase editor, string key, bool defaultValue)
        {
            _editor = new WeakReference<EditorHostBase>(editor);

            Key = key;
            DefaultValue = defaultValue;
        }

        private async void UpdateValueAsync()
        {
            if (_editor.TryGetTarget(out var editor))
            {
                await editor.InvokeScriptAsync("updateContext", [Key, Value]);
            }
        }

        /// <summary>
        /// Gets the current value of this context key.
        /// </summary>
        /// <returns>The current boolean value.</returns>
        public bool Get()
        {
            return Value;
        }

        /// <summary>
        /// Resets the context key to its default value.
        /// </summary>
        public void Reset()
        {
            Value = DefaultValue;

            UpdateValueAsync();
        }

        /// <summary>
        /// Sets a new value for this context key.
        /// </summary>
        /// <param name="value">The new boolean value to set.</param>
        public void Set(bool value)
        {
            Value = value;

            UpdateValueAsync();
        }
    }
}
