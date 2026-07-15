using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    #region Constants
    private const string IncrementalSourceCode =
    """
    using Macaron.InlineInterface;
    using System;

    namespace MyNamespace;

    public interface IValue
    {
        event Action Changed;
        int Value { get; set; }
        string this[int index] { get; }
        int GetValue();
    }

    public static class Factory
    {
        public static IValue Create() => Implementation
            .Of<IValue>(allowMissingImplementation: true)
            .Build();
    }

    public static class Unrelated
    {
    }
    """;

    private const string UnrelatedChangeSourceCode =
    """
    using Macaron.InlineInterface;
    using System;

    namespace MyNamespace;

    public interface IValue
    {
        event Action Changed;
        int Value { get; set; }
        string this[int index] { get; }
        int GetValue();
    }

    public static class Factory
    {
        public static IValue Create() => Implementation
            .Of<IValue>(allowMissingImplementation: true)
            .Build();
    }

    public static class Unrelated
    {
        public static int Value => 42;
    }
    """;

    private const string InterfaceChangeSourceCode =
    """
    using Macaron.InlineInterface;
    using System;

    namespace MyNamespace;

    public interface IValue
    {
        event Action Changed;
        int Value { get; set; }
        string this[int index] { get; }
        int GetValue();
        string FormatValue();
    }

    public static class Factory
    {
        public static IValue Create() => Implementation
            .Of<IValue>(allowMissingImplementation: true)
            .Build();
    }

    public static class Unrelated
    {
    }
    """;
    #endregion

    #region Tests
    [Test]
    public void GeneratorCachesSourceOutputWhenGenerationModelIsUnchanged()
    {
        var (_, updatedResult) = RunGeneratorIncrementally(
            IncrementalSourceCode,
            UnrelatedChangeSourceCode
        );
        var modelReasons = GetModelRunReasons(updatedResult);
        var outputReasons = GetOutputRunReasons(updatedResult);

        Assert.Multiple(() =>
        {
            Assert.That(modelReasons, Is.EqualTo(new[] { IncrementalStepRunReason.Unchanged }));
            Assert.That(outputReasons, Does.Contain(IncrementalStepRunReason.Cached));
            Assert.That(outputReasons, Does.Not.Contain(IncrementalStepRunReason.Modified));
        });
    }

    [Test]
    public void GeneratorRegeneratesSourceOutputWhenGenerationModelIsModified()
    {
        var (originalResult, updatedResult) = RunGeneratorIncrementally(
            IncrementalSourceCode,
            InterfaceChangeSourceCode
        );
        var modelReasons = GetModelRunReasons(updatedResult);
        var outputReasons = GetOutputRunReasons(updatedResult);
        var originalSource = originalResult.GeneratedSources.Single().SourceText.ToString();
        var updatedSource = updatedResult.GeneratedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(modelReasons, Is.EqualTo(new[] { IncrementalStepRunReason.Modified }));
            Assert.That(outputReasons, Does.Contain(IncrementalStepRunReason.New));
            Assert.That(outputReasons, Does.Not.Contain(IncrementalStepRunReason.Cached));
            Assert.That(updatedSource, Is.Not.EqualTo(originalSource));
        });
    }
    #endregion

    #region Static Methods
    private static (GeneratorRunResult OriginalResult, GeneratorRunResult UpdatedResult) RunGeneratorIncrementally(
        string sourceCode,
        string updatedSourceCode
    )
    {
        var compilation = CreateCompilation(
            sourceCode,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );
        var originalSyntaxTree = compilation.SyntaxTrees.Single();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new InlineInterfaceGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: (CSharpParseOptions)originalSyntaxTree.Options,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

        driver = driver.RunGenerators(compilation);

        var originalResult = driver.GetRunResult().Results.Single();
        var updatedSyntaxTree = CSharpSyntaxTree.ParseText(
            updatedSourceCode,
            (CSharpParseOptions)originalSyntaxTree.Options
        );
        var updatedCompilation = compilation.ReplaceSyntaxTree(originalSyntaxTree, updatedSyntaxTree);

        driver = driver.RunGenerators(updatedCompilation);

        return (
            OriginalResult: originalResult,
            UpdatedResult: driver.GetRunResult().Results.Single()
        );
    }

    private static IncrementalStepRunReason[] GetModelRunReasons(GeneratorRunResult result)
    {
        return result
            .TrackedSteps[nameof(InterfaceGenerationModel)]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
    }

    private static IncrementalStepRunReason[] GetOutputRunReasons(GeneratorRunResult result)
    {
        return result
            .TrackedOutputSteps
            .Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
    }
    #endregion
}
