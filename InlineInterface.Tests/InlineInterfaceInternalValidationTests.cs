using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    [Test]
    public void InterfaceValidatorReturnsContextsForSupportedInterface()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            using System;
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                event Action Changed;
                string Name { get; set; }
                void Update(string value);
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        var interfaceSymbol = GetNamedTypeSymbol(compilation, "Macaron.InlineInterface.Tests.IBuffer");
        var typeSyntax = GetTypeArgumentSyntax(compilation);

        var result = InterfaceValidator.ValidateTargetInterface(interfaceSymbol, typeSyntax);

        Assert.That(result, Is.TypeOf<TargetInterfaceValidationResult.Success>());

        var success = (TargetInterfaceValidationResult.Success)result;
        Assert.That(success.InterfaceSymbol, Is.SameAs(interfaceSymbol));
        Assert.That(success.Contexts, Has.Length.EqualTo(1));
        Assert.That(success.Contexts[0].EventSymbols, Has.Length.EqualTo(1));
        Assert.That(success.Contexts[0].PropertySymbols, Has.Length.EqualTo(1));
        Assert.That(success.Contexts[0].MethodSymbols, Has.Length.EqualTo(1));
    }

    [Test]
    public void InterfaceValidatorReturnsGenericMethodDiagnosticForUnsupportedMethod()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                void Update<T>(T value);
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        var interfaceSymbol = GetNamedTypeSymbol(compilation, "Macaron.InlineInterface.Tests.IBuffer");
        var typeSyntax = GetTypeArgumentSyntax(compilation);

        var result = InterfaceValidator.ValidateTargetInterface(interfaceSymbol, typeSyntax);

        Assert.That(result, Is.TypeOf<TargetInterfaceValidationResult.Failure>());

        var failure = (TargetInterfaceValidationResult.Failure)result;
        Assert.That(failure.Diagnostics.Select(diagnostic => diagnostic.Id), Has.Some.EqualTo("MII0003"));
    }

    [Test]
    public void InterfaceValidatorReturnsEventModifierDiagnosticForUnsupportedEventDelegate()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public delegate void BufferChangedHandler(out int current);

            public interface IBuffer
            {
                event BufferChangedHandler Changed;
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        var interfaceSymbol = GetNamedTypeSymbol(compilation, "Macaron.InlineInterface.Tests.IBuffer");
        var typeSyntax = GetTypeArgumentSyntax(compilation);

        var result = InterfaceValidator.ValidateTargetInterface(interfaceSymbol, typeSyntax);

        Assert.That(result, Is.TypeOf<TargetInterfaceValidationResult.Failure>());

        var failure = (TargetInterfaceValidationResult.Failure)result;
        Assert.That(failure.Diagnostics.Select(diagnostic => diagnostic.Id), Has.Some.EqualTo("MII0007"));
    }

    [Test]
    public void TargetTypeExtractorReportsDiagnosticForNonInterfaceType()
    {
        var (diagnostics, _) = CompileAndGetResults<TargetTypeExtractorTestGenerator>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public class NotAnInterface { }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<NotAnInterface>();
                }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        Assert.That(
            diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.Id),
            Has.Some.EqualTo("MII0001")
        );
    }

    [Test]
    public void TargetTypeExtractorIgnoresUnrelatedGenericInvocations()
    {
        var (_, generatedCodes) = CompileAndGetResults<TargetTypeExtractorTestGenerator>(
            sourceCode:
            """
            namespace Macaron.InlineInterface.Tests;

            public static class SomethingElse
            {
                public static T Of<T>() => default!;
            }

            public interface IBuffer { }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = SomethingElse.Of<IBuffer>();
                }
            }
            """
        );

        Assert.That(generatedCodes, Is.Empty);
    }

    private sealed class TargetTypeExtractorTestGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax,
                    transform: static (generatorSyntaxContext, _) => TargetTypeExtractor.Discover(generatorSyntaxContext)
                )
                .Where(static result => result is not TargetTypeDiscoveryResult.NotApplicable);

            context.RegisterSourceOutput(
                provider,
                static (sourceProductionContext, result) =>
                {
                    switch (result)
                    {
                        case TargetTypeDiscoveryResult.Failure failure:
                        {
                            sourceProductionContext.ReportDiagnostic(failure.Diagnostic);
                            break;
                        }
                        case TargetTypeDiscoveryResult.Success success:
                        {
                            sourceProductionContext.AddSource(
                                hintName: $"{success.Symbol.Name}.extractor.g.cs",
                                source: "// extractor-success"
                            );
                            break;
                        }
                    }
                }
            );
        }
    }
}
