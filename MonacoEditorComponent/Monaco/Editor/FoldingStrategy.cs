using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Selects the folding strategy. 'auto' uses the strategies contributed for the current
    /// document, 'indentation' uses the indentation based folding strategy.
    /// Defaults to 'auto'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<FoldingStrategy>))]
    public enum FoldingStrategy
    {
        [JsonStringEnumMemberName("auto")]
        [EnumMember(Value = "auto")]
        Auto,
        [JsonStringEnumMemberName("indentation")]
        [EnumMember(Value = "indentation")]
        Indentation,
    };
}
