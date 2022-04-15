using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerators.PolymorphicJson;

internal sealed record GeneratorData(
    string BaseTypeNamespace,
    string BaseTypeName,
    string? DiscriminatorJsonFieldName,
    ImmutableArray<DerivedType> DerivedTypes,
    SyntaxTree SyntaxTree,
    TextSpan Span,
    bool HasJsonDiscriminatorAttribute)
{
    public string JsonConverterClassName => $"{BaseTypeName}JsonConverter";

    public string DiscriminatorPropertyName => $"{BaseTypeName}Discriminator";

    public string DiscriminatorJsonFieldNameConstName => "DiscriminatorJsonFieldName";
}
