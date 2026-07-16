using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    [TestCase("Implementation.Of<IBuffer>()", true)]
    [TestCase("Of<IBuffer>()", true)]
    [TestCase("Implementation.@Of<IBuffer>()", true)]
    [TestCase("Implementation.Of<IBuffer, IOther>()", false)]
    [TestCase("Implementation.Create<IBuffer>()", false)]
    [TestCase("Implementation.Of()", false)]
    [TestCase("DoWork()", false)]
    [TestCase("Implementation.Of<IBuffer>", false)]
    public void TargetTypeExtractorIdentifiesCandidateInvocation(string sourceCode, bool expected)
    {
        var syntaxNode = SyntaxFactory.ParseExpression(sourceCode);

        Assert.That(TargetTypeExtractor.IsCandidate(syntaxNode), Is.EqualTo(expected));
    }

    [Test]
    public void TargetTypeExtractorIdentifiesImplementationType()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            namespace Macaron.InlineInterface
            {
                public static class Implementation<T> { }

                public static class Container
                {
                    public static class Implementation { }
                }
            }

            namespace Other
            {
                public static class Implementation { }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                TargetTypeExtractor.IsImplementationType(GetNamedTypeSymbol(
                    compilation,
                    "Macaron.InlineInterface.Implementation"
                )),
                Is.True
            );
            Assert.That(
                TargetTypeExtractor.IsImplementationType(GetNamedTypeSymbol(
                    compilation,
                    "Macaron.InlineInterface.Implementation`1"
                )),
                Is.False
            );
            Assert.That(
                TargetTypeExtractor.IsImplementationType(GetNamedTypeSymbol(
                    compilation,
                    "Macaron.InlineInterface.Container+Implementation"
                )),
                Is.False
            );
            Assert.That(
                TargetTypeExtractor.IsImplementationType(GetNamedTypeSymbol(
                    compilation,
                    "Other.Implementation"
                )),
                Is.False
            );
        });
    }

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
        var diagnostic = failure.Diagnostics.Single(diagnostic => diagnostic.Id == "MII0003");

        Assert.That(diagnostic.Location.SourceSpan, Is.EqualTo(typeSyntax.Span));
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
    public void InterfaceValidatorReturnsSymbolIssuesWithoutTypeSyntax()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                void Update<T>(T value);
            }
            """
        );
        var interfaceSymbol = GetNamedTypeSymbol(compilation, "Macaron.InlineInterface.Tests.IBuffer");

        var result = InterfaceValidator.ValidateTargetInterface(interfaceSymbol);

        Assert.That(result, Is.TypeOf<TargetInterfaceSymbolValidationResult.Failure>());

        var failure = (TargetInterfaceSymbolValidationResult.Failure)result;

        Assert.That(failure.Issues, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(failure.Issues[0].Kind, Is.EqualTo(InterfaceValidationIssueKind.NotAllowedGenericMethod));
            Assert.That(failure.Issues[0].MemberName, Is.EqualTo("Update"));
        });
    }

    [Test]
    public void MethodSignatureComparerUsesReturnAndParameterTypes()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            namespace Macaron.InlineInterface.Tests;

            public interface ILeft
            {
                string Read(int value);
            }

            public interface IRight
            {
                string Read(int value);
            }

            public interface IDifferentReturn
            {
                object Read(int value);
            }

            public interface IDifferentParameter
            {
                string Read(string value);
            }
            """
        );

        var left = MethodSignature.Create(GetNamedTypeSymbol(
            compilation,
            "Macaron.InlineInterface.Tests.ILeft"
        ).GetMembers("Read").OfType<IMethodSymbol>().Single());
        var right = MethodSignature.Create(GetNamedTypeSymbol(
            compilation,
            "Macaron.InlineInterface.Tests.IRight"
        ).GetMembers("Read").OfType<IMethodSymbol>().Single());
        var differentReturn = MethodSignature.Create(GetNamedTypeSymbol(
            compilation,
            "Macaron.InlineInterface.Tests.IDifferentReturn"
        ).GetMembers("Read").OfType<IMethodSymbol>().Single());
        var differentParameter = MethodSignature.Create(GetNamedTypeSymbol(
            compilation,
            "Macaron.InlineInterface.Tests.IDifferentParameter"
        ).GetMembers("Read").OfType<IMethodSymbol>().Single());
        var comparer = MethodSignatureComparer.Instance;

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(left, right), Is.True);
            Assert.That(comparer.GetHashCode(left), Is.EqualTo(comparer.GetHashCode(right)));
            Assert.That(comparer.Equals(left, differentReturn), Is.False);
            Assert.That(comparer.Equals(left, differentParameter), Is.False);
        });
    }

    [Test]
    public void PropertySignatureComparerIgnoresAccessorsAndUsesIndexerParameters()
    {
        var compilation = CreateCompilation(
            sourceCode:
            """
            namespace Macaron.InlineInterface.Tests;

            public interface IReadBuffer
            {
                string Value { get; }
            }

            public interface IWriteBuffer
            {
                string Value { set; }
            }

            public interface IIntIndexer
            {
                string this[int index] { get; }
            }

            public interface IStringIndexer
            {
                string this[string index] { get; }
            }
            """
        );

        static PropertySignature GetSignature(CSharpCompilation compilation, string metadataName)
        {
            return PropertySignature.Create(GetNamedTypeSymbol(
                compilation,
                metadataName
            ).GetMembers().OfType<IPropertySymbol>().Single());
        }

        var read = GetSignature(compilation, "Macaron.InlineInterface.Tests.IReadBuffer");
        var write = GetSignature(compilation, "Macaron.InlineInterface.Tests.IWriteBuffer");
        var intIndexer = GetSignature(compilation, "Macaron.InlineInterface.Tests.IIntIndexer");
        var stringIndexer = GetSignature(compilation, "Macaron.InlineInterface.Tests.IStringIndexer");
        var comparer = PropertySignatureComparer.Instance;

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(read, write), Is.True);
            Assert.That(comparer.GetHashCode(read), Is.EqualTo(comparer.GetHashCode(write)));
            Assert.That(comparer.Equals(intIndexer, stringIndexer), Is.False);
        });
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
                    predicate: static (node, _) => TargetTypeExtractor.IsCandidate(node),
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
