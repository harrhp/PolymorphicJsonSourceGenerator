using Microsoft.CodeAnalysis;

namespace SourceGenerators.PolymorphicJson;

internal sealed record DerivedType(string Name,
    string? Discriminator,
    SyntaxReference Declaration,
    bool HasRedundantAttribute,
    bool IsDefault);
