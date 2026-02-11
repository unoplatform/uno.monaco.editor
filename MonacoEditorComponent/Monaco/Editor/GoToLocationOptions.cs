using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Configuration options for go to location
    /// </summary>
    public sealed class GoToLocationOptions
    {
        public string? AlternativeDeclarationCommand { get; set; }

        public string? AlternativeDefinitionCommand { get; set; }

        public string? AlternativeImplementationCommand { get; set; }

        public string? AlternativeReferenceCommand { get; set; }

        public string? AlternativeTypeDefinitionCommand { get; set; }

        public Multiple? Multiple { get; set; }

        public Multiple? MultipleDeclarations { get; set; }

        public Multiple? MultipleDefinitions { get; set; }

        public Multiple? MultipleImplementations { get; set; }

        public Multiple? MultipleReferences { get; set; }

        public Multiple? MultipleTypeDefinitions { get; set; }
    }

}
