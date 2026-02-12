namespace Monaco.Helpers.Stubs
{
    /// <summary>
    /// Stub attribute for WinRT read-only array parameter semantics. Used to satisfy
    /// generated code that references <c>[ReadOnlyArray]</c> without requiring the
    /// full WinRT projection package.
    /// </summary>
    class ReadOnlyArrayAttribute : Attribute
    {
    }
}
