using Monaco.Helpers;
using Newtonsoft.Json;

namespace Monaco.Editor
{
    /// <summary>
    /// New model decorations.
    /// </summary>
    public sealed class IModelDeltaDecoration(IRange range, IModelDecorationOptions options)
    {
        [JsonProperty("options")]
        public IModelDecorationOptions Options { get; private set; } = options;

        [JsonProperty("range"), JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        public IRange Range { get; private set; } = range;
    }
}
