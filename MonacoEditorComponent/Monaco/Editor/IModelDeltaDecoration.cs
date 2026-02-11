using Monaco.Helpers;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// New model decorations.
    /// </summary>
    public sealed class IModelDeltaDecoration(IRange range, IModelDecorationOptions options)
    {
        [JsonInclude]
        public IModelDecorationOptions Options { get; internal set; } = options;

        [JsonInclude]
        [JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        public IRange Range { get; internal set; } = range;
    }
}
