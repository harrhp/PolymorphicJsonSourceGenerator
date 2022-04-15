using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceGenerators.PolymorphicJson;

[Generator]
public sealed class PolymorphicJsonGenerator : IIncrementalGenerator
{
    private const string PolymorphicJsonCategory = "PolymorphicJson";

    private const string JsonPolymorphicAttributeName = "JsonPolymorphicAttribute";
    private const string JsonDiscriminatorAttributeName = "JsonDiscriminatorAttribute";

    private static readonly DiagnosticDescriptor NoDerivedTypes = new(
        "PJ0001",
        "Base type must have derived types",
        "Base type {0} must have derived types",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor NoJsonDiscriminatorOnDerivedType = new(
        "PJ0002",
        "Type has to be marked with JsonDiscriminatorAttribute and specify discriminator",
        "Type {0} has to be marked with JsonDiscriminatorAttribute and specify discriminator",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnneccessaryJsonPolymorphicAttributeOnDerivedType = new(
        "PJ0003",
        "Only base type has to be marked with JsonPolymorphicAttribute",
        "Only base type {0} has to be marked with JsonPolymorphicAttribute",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor NoDiscriminatorJsonFieldNameOnBaseType = new(
        "PJ0004",
        "Discriminator json field name must be not null or empty",
        "Discriminator json field name for type {0} must be not null or empty",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MoreThanOneDefaultDerivedType = new(
        "PJ0005",
        "There can be no more than 1 default type",
        "There can be no more than 1 default type",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnneccessaryJsonDiscriminatorAttributeOnBaseType = new(
        "PJ0006",
        "Only derived types have to be marked with JsonDiscriminatorAttribute",
        "Only derived types have to be marked with JsonDiscriminatorAttribute",
        PolymorphicJsonCategory,
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        AddSupportingTypes(context);
        var compilationAssembly = context.CompilationProvider.Select((x, _) => x.Assembly);
        var derivedTypesMap = context.CompilationProvider.SelectMany((c, _) => GetTypes(c.Assembly))
            .Combine(compilationAssembly)
            .Where(x => x.Left.BaseType?.IsAbstract == true
                && SymbolEqualityComparer.Default.Equals(x.Right, x.Left.BaseType.ContainingAssembly))
            .Select((x, _) => x.Left)
            .Collect()
            .Select((x, _) => x.ToLookup(type => type.BaseType, SymbolEqualityComparer.Default));

        var types = context.SyntaxProvider.CreateSyntaxProvider(
                (x, _) => IsRecord(x),
                (x, _) => (x.Node.SyntaxTree, x.Node.Span,
                    declaredSymbol: x.SemanticModel.GetDeclaredSymbol(x.Node)!))
            .Where(x => x.declaredSymbol.GetAttributes()
                .Any(a => a.AttributeClass?.Name == JsonPolymorphicAttributeName));

        var generatorDataValueProvider = types.Combine(derivedTypesMap)
            .Select((x, _) => new GeneratorData(
                x.Left.declaredSymbol.ContainingNamespace.ToString(),
                x.Left.declaredSymbol.Name,
                x.Left.declaredSymbol.GetAttributes()
                    .First(a => a.AttributeClass?.Name == JsonPolymorphicAttributeName)
                    .ConstructorArguments.FirstOrDefault()
                    .Value?.ToString(),
                x.Right[x.Left.declaredSymbol]
                    .Select(y =>
                    {
                        var attributes = y.GetAttributes();
                        var discriminatorAttribute = attributes.FirstOrDefault(a =>
                            a.AttributeClass?.Name == JsonDiscriminatorAttributeName);
                        return new DerivedType(y.Name,
                            discriminatorAttribute?.ConstructorArguments.FirstOrDefault().Value?.ToString(),
                            y.DeclaringSyntaxReferences.First(),
                            attributes.Any(a => a.AttributeClass?.Name == JsonPolymorphicAttributeName),
                            (bool)(discriminatorAttribute?.NamedArguments.FirstOrDefault(a => a.Key == "IsDefault")
                                .Value.Value ?? false));
                    })
                    .ToImmutableArray(),
                x.Left.SyntaxTree,
                x.Left.Span,
                x.Left.declaredSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == JsonDiscriminatorAttributeName)));
        context.RegisterSourceOutput(generatorDataValueProvider,
            static (sourceProductionContext, data) =>
            {
                if (data.DerivedTypes.IsEmpty)
                {
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(NoDerivedTypes,
                        Location.Create(data.SyntaxTree, data.Span),
                        data.BaseTypeName));
                    return;
                }

                if (string.IsNullOrWhiteSpace(data.DiscriminatorJsonFieldName))
                {
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(NoDiscriminatorJsonFieldNameOnBaseType,
                        Location.Create(data.SyntaxTree, data.Span),
                        data.BaseTypeName));
                    return;
                }

                var notAttributedDerivedTypes = data.DerivedTypes
                    .Where(x => string.IsNullOrWhiteSpace(x.Discriminator) && !x.IsDefault)
                    .ToArray();
                if (notAttributedDerivedTypes.Any())
                {
                    foreach (var derivedType in notAttributedDerivedTypes)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(NoJsonDiscriminatorOnDerivedType,
                            Location.Create(derivedType.Declaration.SyntaxTree, derivedType.Declaration.Span),
                            derivedType.Name));
                    }

                    return;
                }

                var attributedWithJsonPolymorphicDerivedTypes =
                    data.DerivedTypes.Where(x => x.HasRedundantAttribute).ToArray();
                if (attributedWithJsonPolymorphicDerivedTypes.Any())
                {
                    foreach (var derivedType in attributedWithJsonPolymorphicDerivedTypes)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            UnneccessaryJsonPolymorphicAttributeOnDerivedType,
                            Location.Create(derivedType.Declaration.SyntaxTree, derivedType.Declaration.Span),
                            data.BaseTypeName));
                    }

                    return;
                }

                var defaultDerivedTypes = data.DerivedTypes.Where(x => x.IsDefault).ToArray();
                if (defaultDerivedTypes.Length > 1)
                {
                    foreach (var derivedType in defaultDerivedTypes)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                            MoreThanOneDefaultDerivedType,
                            Location.Create(derivedType.Declaration.SyntaxTree, derivedType.Declaration.Span)));
                    }

                    return;
                }

                if (data.HasJsonDiscriminatorAttribute)
                {
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                        UnneccessaryJsonDiscriminatorAttributeOnBaseType,
                        Location.Create(data.SyntaxTree, data.Span)));

                    return;
                }

                sourceProductionContext.AddSource($"{data.BaseTypeName}.Generated.cs",
                    Generate(data).GetText(Encoding.UTF8));
            });
    }

    private static CompilationUnitSyntax Generate(GeneratorData data) =>
        CompilationUnit()
            .AddUsings(UsingDirective(IdentifierName("System"))
                    .WithUsingKeyword(Token(
                        TriviaList(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true))),
                        SyntaxKind.UsingKeyword,
                        TriviaList())),
                UsingDirective(ParseName("System.Text.Json")),
                UsingDirective(ParseName("System.Text.Json.Serialization")),
                UsingDirective(ParseName("System.Runtime.CompilerServices")))
            .AddMembers(FileScopedNamespaceDeclaration(ParseName(data.BaseTypeNamespace))
                .AddMembers(GeneratePartialBaseType(data), GenerateJsonConverterClass(data)))
            .NormalizeWhitespace();

    private static RecordDeclarationSyntax GeneratePartialBaseType(GeneratorData data)
    {
        var switchExpressionArmSyntaxes = data.DerivedTypes.Where(x => !x.IsDefault)
            .Select(x => SwitchExpressionArm(ConstantPattern(IdentifierName(x.Name)),
                LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(x.Discriminator!))))
            .Append(SwitchExpressionArm(DiscardPattern(),
                data.DerivedTypes.SingleOrDefault(x => x.IsDefault) is not null
                    ? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                        IdentifierName("Empty"))
                    : ThrowExpression(ObjectCreationExpression(IdentifierName("SwitchExpressionException"))
                        .AddArgumentListArguments(Argument(ThisExpression())))));

        return RecordDeclaration(Token(SyntaxKind.RecordKeyword), data.BaseTypeName)
            .AddModifiers(Token(SyntaxKind.PartialKeyword))
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
            .AddAttributeLists(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("JsonConverter"))
                .AddArgumentListArguments(
                    AttributeArgument(TypeOfExpression(IdentifierName(data.JsonConverterClassName)))))))
            .AddMembers(
                FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)))
                        .AddVariables(VariableDeclarator(Identifier(data.DiscriminatorJsonFieldNameConstName))
                            .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                Literal(data.DiscriminatorJsonFieldName!))))))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.ConstKeyword)),
                PropertyDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)),
                        Identifier(data.DiscriminatorPropertyName))
                    .AddAttributeLists(AttributeList(SingletonSeparatedList(
                        Attribute(IdentifierName("JsonPropertyName"))
                            .AddArgumentListArguments(AttributeArgument(LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(data.DiscriminatorJsonFieldName!)))))))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddAccessorListAccessors(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))),
                ConstructorDeclaration(Identifier(data.BaseTypeName))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddBodyStatements(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(data.DiscriminatorPropertyName),
                        SwitchExpression(ThisExpression()).WithArms(SeparatedList(switchExpressionArmSyntaxes))))));
    }

    private static ClassDeclarationSyntax GenerateJsonConverterClass(GeneratorData data)
    {
        var switchArms = data.DerivedTypes.Where(x => !x.IsDefault)
            .Select(type => SwitchExpressionArm(ConstantPattern(LiteralExpression(SyntaxKind.StringLiteralExpression,
                    Literal(type.Discriminator!))),
                TypeOfExpression(IdentifierName(type.Name))))
            .Append(SwitchExpressionArm(DiscardPattern(),
                data.DerivedTypes.SingleOrDefault(x => x.IsDefault) is { } derivedType
                    ? TypeOfExpression(IdentifierName(derivedType.Name))
                    : ThrowExpression(ObjectCreationExpression(IdentifierName("SwitchExpressionException"))
                        .AddArgumentListArguments(Argument(ThisExpression())))))
            .ToArray();

        return ClassDeclaration(data.JsonConverterClassName)
            .AddModifiers(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.SealedKeyword))
            .AddBaseListTypes(SimpleBaseType(GenericName(Identifier("PolymorphicJsonConverterBase"))
                .WithTypeArgumentList(
                    TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName(data.BaseTypeName))))))
            .AddMembers(
                ConstructorDeclaration(Identifier(data.JsonConverterClassName))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .AddArgumentListArguments(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                            Literal(data.DiscriminatorJsonFieldName!)))))
                    .WithBody(Block()),
                MethodDeclaration(IdentifierName("Type"), Identifier("GetImplementationType"))
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword))
                    .AddParameterListParameters(Parameter(Identifier("discriminator"))
                        .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))
                    .AddBodyStatements(ReturnStatement(SwitchExpression(IdentifierName("discriminator"),
                        SeparatedList(switchArms)))))
            .NormalizeWhitespace();
    }

    private static bool IsRecord(SyntaxNode x) =>
        x is RecordDeclarationSyntax recordDeclaration
        && recordDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && recordDeclaration.AttributeLists.Any();

    private static IEnumerable<INamedTypeSymbol> GetTypes(IAssemblySymbol assembly)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(assembly.GlobalNamespace);
        while (stack.Count > 0)
        {
            var @namespace = stack.Pop();
            foreach (var type in @namespace.GetTypeMembers())
            {
                if (assembly.Equals(type.ContainingAssembly, SymbolEqualityComparer.Default))
                {
                    yield return type;
                }
            }

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
            {
                stack.Push(nestedNamespace);
            }
        }
    }

    private static void AddSupportingTypes(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static x => x.AddSource("SupportingTypes.Generated.cs",
            @"
#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Text.Json.JsonSerializer;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class JsonDiscriminatorAttribute : JsonAttribute
{
    public JsonDiscriminatorAttribute()
    {
    }

    public JsonDiscriminatorAttribute(string discriminator)
    {
        Discriminator = discriminator;
    }

    public string? Discriminator { get; }

    public bool IsDefault { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class JsonPolymorphicAttribute : JsonAttribute
{
    public JsonPolymorphicAttribute(string discriminatorJsonFieldName)
    {
        DiscriminatorJsonFieldName = discriminatorJsonFieldName;
    }

    public string DiscriminatorJsonFieldName { get; }
}

internal abstract class PolymorphicJsonConverterBase<T> : JsonConverter<T>
{
    private readonly string _discriminatorJsonFieldName;

    protected PolymorphicJsonConverterBase(string discriminatorJsonFieldName)
    {
        _discriminatorJsonFieldName = discriminatorJsonFieldName;
    }

    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var reader2 = reader;
        reader2.Read();
        var discriminator = ReadPropertyValue(ref reader2, _discriminatorJsonFieldName) ?? throw new JsonException();
        var implementationType = GetImplementationType(discriminator);
        return (T)Deserialize(ref reader, implementationType, options)!;
    }

    private string? ReadPropertyValue(ref Utf8JsonReader reader2, string propertyName)
    {
        var currentDepth = reader2.CurrentDepth;
        while (reader2.CurrentDepth >= currentDepth)
        {
            if (reader2.CurrentDepth == currentDepth && reader2.TokenType == JsonTokenType.PropertyName
                && reader2.ValueTextEquals(propertyName))
            {
                reader2.Read();
                return reader2.GetString();
            }

            reader2.Read();
        }

        return null;
    }

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        Serialize(writer, value, value!.GetType(), options);

    protected abstract Type GetImplementationType(string discriminator);
}"));
}
