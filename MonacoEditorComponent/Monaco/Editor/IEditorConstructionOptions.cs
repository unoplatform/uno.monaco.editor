using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    public interface IEditorConstructionOptions : IEditorOptions
    {
        IDimension? Dimension { get; set; }
    }
}
