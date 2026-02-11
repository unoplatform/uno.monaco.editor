using Monaco.Helpers;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// New model decorations.
    /// </summary>
    public sealed class IModelDeltaDecoration(IRange range, IModelDecorationOptions options)
    {
        [JsonProperty("options")]
        public IModelDecorationOptions Options { get; private set; } = options;

        [JsonProperty("range")]
        [System.Text.Json.Serialization.JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftInterfaceToClassConverter<IRange, Range>))]
        public IRange Range { get; private set; } = range;
    }
}
