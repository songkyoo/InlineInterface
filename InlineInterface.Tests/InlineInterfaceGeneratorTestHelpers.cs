using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.InlineInterface.Tests;

[TestFixture]
public partial class InlineInterfaceGeneratorTests
{
    private static (ImmutableArray<Diagnostic> diagnostics, string[] generatedCodes) CompileAndGetResults<T>(
        string sourceCode,
        Assembly[]? additionalAssemblies = null
    ) where T : IIncrementalGenerator, new()
    {
        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Concat(additionalAssemblies ?? [])
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create(
            assemblyName: "Macaron.InlineInterface.Tests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var generator = new T();
        var driver = CSharpGeneratorDriver.Create(generator);

        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        var generatedSources = result.GeneratedSources;
        var generatedCodes = generatedSources.Select(source => source.SourceText.ToString()).ToArray();

        var allDiagnostics = compilation.GetDiagnostics()
            .Concat(result.Diagnostics)
            .ToImmutableArray();

        return (allDiagnostics, generatedCodes);
    }

    private static void AssertGeneratedCode(string sourceCode, string[] expectedCodes)
    {
        var (_, generatedCodes) = CompileAndGetResults<InlineInterfaceGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        Assert.That(generatedCodes, Has.Length.EqualTo(expectedCodes.Length));

        foreach (var (generatedCode, index) in generatedCodes.Select((code, index) => (code, index)))
        {
            Assert.That(
                actual: generatedCode.ReplaceLineEndings(),
                expression: Is.EqualTo(expectedCodes[index].ReplaceLineEndings())
            );
        }
    }

    private static void AssertGeneratedCode(string sourceCode, int sourceIndex, string expected)
    {
        var (_, generatedCodes) = CompileAndGetResults<InlineInterfaceGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        Assert.That(
            actual: generatedCodes[sourceIndex].ReplaceLineEndings(),
            expression: Is.EqualTo(expected.ReplaceLineEndings())
        );
    }

    private static void AssertGeneratedCode(string sourceCode, string expected)
    {
        AssertGeneratedCode(sourceCode, sourceIndex: 0, expected);
    }

    private static void AssertGeneratedCodeContainsAll(string sourceCode, int sourceIndex, params string[] expectedFragments)
    {
        var (_, generatedCodes) = CompileAndGetResults<InlineInterfaceGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        var generatedCode = generatedCodes[sourceIndex].ReplaceLineEndings();

        foreach (var expectedFragment in expectedFragments)
        {
            Assert.That(
                actual: generatedCode,
                expression: Does.Contain(expectedFragment.ReplaceLineEndings())
            );
        }
    }

    private static void AssertGeneratedCodeContainsAll(string sourceCode, params string[] expectedFragments)
    {
        AssertGeneratedCodeContainsAll(sourceCode, sourceIndex: 0, expectedFragments);
    }

    private static void AssertDiagnostic(string sourceCode, string expectedDiagnosticId)
    {
        var (diagnostics, _) = CompileAndGetResults<InlineInterfaceGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        var actualDiagnosticIds = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.That(actualDiagnosticIds, Has.Some.Matches(expectedDiagnosticId));
    }

}
