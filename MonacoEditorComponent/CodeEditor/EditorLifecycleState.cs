namespace Monaco
{
    /// <summary>
    /// Tracks the lifecycle state of the editor to ensure EditorLoading/EditorLoaded
    /// fire exactly once per initialization cycle.
    /// </summary>
    internal enum EditorLifecycleState
    {
        /// <summary>Editor is not loaded or has been unloaded.</summary>
        Unloaded,

        /// <summary>Editor is in the process of loading (DOM loaded, Monaco initializing).</summary>
        Loading,

        /// <summary>Editor is fully loaded and interactive.</summary>
        Loaded
    }
}
