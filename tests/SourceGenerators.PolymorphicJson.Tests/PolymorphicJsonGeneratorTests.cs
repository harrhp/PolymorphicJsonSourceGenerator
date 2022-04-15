using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceGenerators.PolymorphicJson.Tests;

[TestClass]
public class PolymorphicJsonGeneratorTests
{
    public static IEnumerable<object[]> EmptyDiscriminatorSource =>
        new[] { "\"\"", "\" \"", "null!" }
            .Select(x => new object[]
            {
                $@"using System.Text.Json.Serialization;

    namespace ConsoleApp1
    {{
        [JsonPolymorphic(""type"")]
        public abstract partial record Abstract1;


        [JsonDiscriminator({x})]
        public record Abstract1Impl : Abstract1;
    }}
    ",
                "PJ0002"
            });

    public static IEnumerable<object[]> EmptyDiscriminatorJsonFieldNameSource =>
        new[] { "\"\"", "\" \"", "null!" }
            .Select(x => new object[]
            {
                $@"using System.Text.Json.Serialization;

    namespace ConsoleApp1
    {{
        [JsonPolymorphic({x})]
        public abstract partial record Abstract1;

        [JsonDiscriminator(""impl1"")]
        public record Abstract1Impl : Abstract1;
    }}
    ",
                "PJ0004"
            });

    [TestMethod,
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1;
}
",
         "PJ0001",
         DisplayName = "no derived types"),
     DynamicData(nameof(EmptyDiscriminatorSource)),
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1;

    [JsonDiscriminator(""impl1"")]
    [JsonPolymorphic(""type"")]
    public record Abstract1Impl : Abstract1;
}
",
         "PJ0003",
         DisplayName = "derived type has JsonPolymorphicAttribute"),
     DynamicData(nameof(EmptyDiscriminatorJsonFieldNameSource)),
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1;

    [JsonDiscriminator(IsDefault = true)]
    public record Abstract1Impl : Abstract1;

    [JsonDiscriminator(IsDefault = true)]
    public record Abstract2Impl : Abstract1;
}
",
         "PJ0005",
         DisplayName = "more than 1 default type"),
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1
{
    [JsonPolymorphic(""type""), JsonDiscriminator(""abstract"")]
    public abstract partial record Abstract1;

    [JsonDiscriminator(""impl1"")]
    public record Abstract1Impl : Abstract1;
}
",
         "PJ0006",
         DisplayName = "base type marked with JsonDiscriminator")]
    public void Fail_if_error_diagnostic(string source, string expectedDiagnosticId)
    {
        var compilation = CreateCompilation(source);

        var (_, diagnostics) = RunGenerators(compilation, new PolymorphicJsonGenerator());

        AssertDiagnostic(diagnostics, expectedDiagnosticId);
    }

    [TestMethod,
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1.Nested
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1
    {
    }

    [JsonDiscriminator(""impl1"")]
    public record Abstract1Impl1([property:JsonPropertyName(""a"")]string A) : Abstract1;

    [JsonDiscriminator(""impl2"")]
    public record Abstract1Impl2([property:JsonPropertyName(""b"")]string B) : Abstract1;
}
",
         @"#nullable enable
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Runtime.CompilerServices;

    namespace ConsoleApp1.Nested;
    [JsonConverter(typeof(Abstract1JsonConverter))]
    partial record Abstract1
    {
        public const string DiscriminatorJsonFieldName = ""type"";
        [JsonPropertyName(""type"")]
        public string Abstract1Discriminator
        {
            get;
        }

        public Abstract1()
        {
            Abstract1Discriminator = this switch
            {
                Abstract1Impl1 => ""impl1"",
                Abstract1Impl2 => ""impl2"",
                _ => throw new SwitchExpressionException(this)};
        }
    }

    internal sealed class Abstract1JsonConverter : PolymorphicJsonConverterBase<Abstract1>
    {
        public Abstract1JsonConverter(): base(""type"")
        {
        }

        protected override Type GetImplementationType(string discriminator)
        {
            return discriminator switch
            {
                ""impl1"" => typeof(Abstract1Impl1),
                ""impl2"" => typeof(Abstract1Impl2),
                _ => throw new SwitchExpressionException(this)};
        }
    }",
         DisplayName = "derived types without default"),
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1.Nested
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1
    {
    }

    [JsonDiscriminator(IsDefault = true)]
    public record Abstract1Impl1([property:JsonPropertyName(""a"")]string A) : Abstract1;
}
",
         @"#nullable enable
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Runtime.CompilerServices;

    namespace ConsoleApp1.Nested;
    [JsonConverter(typeof(Abstract1JsonConverter))]
    partial record Abstract1
    {
        public const string DiscriminatorJsonFieldName = ""type"";
        [JsonPropertyName(""type"")]
        public string Abstract1Discriminator
        {
            get;
        }

        public Abstract1()
        {
            Abstract1Discriminator = this switch
            {
                _ => string.Empty};
        }
    }

    internal sealed class Abstract1JsonConverter : PolymorphicJsonConverterBase<Abstract1>
    {
        public Abstract1JsonConverter(): base(""type"")
        {
        }

        protected override Type GetImplementationType(string discriminator)
        {
            return discriminator switch
            {
                _ => typeof(Abstract1Impl1)};
        }
    }",
         DisplayName = "1 derived type and it's default"),
     DataRow(@"using System.Text.Json.Serialization;

namespace ConsoleApp1.Nested
{
    [JsonPolymorphic(""type"")]
    public abstract partial record Abstract1
    {
    }

    [JsonDiscriminator(""impl1"")]
    public record Abstract1Impl1([property:JsonPropertyName(""a"")]string A) : Abstract1;

    [JsonDiscriminator(IsDefault = true)]
    public record Abstract1Impl2([property:JsonPropertyName(""b"")]string B) : Abstract1;
}
",
         @"#nullable enable
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Runtime.CompilerServices;

    namespace ConsoleApp1.Nested;
    [JsonConverter(typeof(Abstract1JsonConverter))]
    partial record Abstract1
    {
        public const string DiscriminatorJsonFieldName = ""type"";
        [JsonPropertyName(""type"")]
        public string Abstract1Discriminator
        {
            get;
        }

        public Abstract1()
        {
            Abstract1Discriminator = this switch
            {
                Abstract1Impl1 => ""impl1"",
                _ => string.Empty};
        }
    }

    internal sealed class Abstract1JsonConverter : PolymorphicJsonConverterBase<Abstract1>
    {
        public Abstract1JsonConverter(): base(""type"")
        {
        }

        protected override Type GetImplementationType(string discriminator)
        {
            return discriminator switch
            {
                ""impl1"" => typeof(Abstract1Impl1),
                _ => typeof(Abstract1Impl2)};
        }
    }",
         DisplayName = "derived types with default")]
    public void Success(string source, string expectedGenerated)
    {
        var compilation = CreateCompilation(source);

        var (newCompilation, _) = RunGenerators(compilation, new PolymorphicJsonGenerator());

        var expectedSource = ParseSyntaxTree(expectedGenerated);
        var actualSyntaxTree = newCompilation.SyntaxTrees.Last();
        Assert.IsTrue(expectedSource.IsEquivalentTo(actualSyntaxTree), actualSyntaxTree.ToString());
    }

    private static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId) =>
        Assert.IsTrue(diagnostics.Any(x => x.DefaultSeverity == DiagnosticSeverity.Error && x.Id == diagnosticId),
            string.Join(Environment.NewLine, diagnostics));

    private static Compilation CreateCompilation(string source) =>
        CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                Assembly.Load("System.Runtime, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                typeof(object).Assembly,
                typeof(JsonSerializer).Assembly
            }.Select(x => MetadataReference.CreateFromFile(x.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static (Compilation newCompilation, ImmutableArray<Diagnostic> diagnostics) RunGenerators(
        Compilation compilation,
        params IIncrementalGenerator[] generators)
    {
        CSharpGeneratorDriver.Create(generators)
            .RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out var diagnostics);
        return (newCompilation, diagnostics);
    }
}
