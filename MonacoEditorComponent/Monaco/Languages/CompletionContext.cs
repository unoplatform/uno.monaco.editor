using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    /// <summary>
    /// Contains additional information about the context in which
    /// [completion provider](#CompletionItemProvider.provideCompletionItems) is triggered.
    /// </summary>
    public sealed class CompletionContext
    {
        /// <summary>
        /// Character that triggered the completion item provider.
        ///
        /// `undefined` if provider was not triggered by a character.
        /// </summary>
        public string? TriggerCharacter { get; set; }

        /// <summary>
        /// How the completion was triggered.
        /// </summary>
        public CompletionTriggerKind TriggerKind { get; set; }
    }
}
