namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    [Test]
    public void ReportsDiagnosticWhenBuilderIsStoredWithoutBuild()
    {
        AssertAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    var builder = Implementation.Of<IBuffer>();
                }
            }
            """,
            expectedDiagnosticId: "MII0008"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenBuilderChainDoesNotEndWithBuild()
    {
        AssertAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Write(value => { });
                }
            }
            """,
            expectedDiagnosticId: "MII0008"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenBuildIsMissingRequiredMethodDelegate()
    {
        AssertAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                string Read();
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Read(() => "")
                        .Build();
                }
            }
            """,
            expectedDiagnosticId: "MII0009"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenBuildIsMissingMergedInheritedPropertyDelegate()
    {
        AssertAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IReadBuffer
            {
                string Value { get; }
            }

            public interface IWriteBuffer
            {
                string Value { set; }
            }

            public interface IBuffer : IReadBuffer, IWriteBuffer
            {
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>().Build();
                }
            }
            """,
            expectedDiagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersWhenAllowMissingImplementationIsTrue()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                string Read();
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>(allowMissingImplementation: true)
                        .Read(() => "")
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportDiagnosticWhenBuilderIsCompletedInSingleExpression()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                string Value { get; set; }
                void Write(string value);
            }

            public class Test
            {
                public IBuffer M()
                {
                    return Implementation.Of<IBuffer>()
                        .Value(
                            getter: () => "",
                            setter: value => { }
                        )
                        .Write(value => { })
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0008"
        );

        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                string Value { get; set; }
                void Write(string value);
            }

            public class Test
            {
                public IBuffer M()
                {
                    return Implementation.Of<IBuffer>()
                        .Value(
                            getter: () => "",
                            setter: value => { }
                        )
                        .Write(value => { })
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void ReportsDiagnosticsForRepeatedBuilderChainsOfSameInterface()
    {
        var diagnostics = AnalyzeAndGetDiagnostics<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>().Build();
                    _ = Implementation.Of<IBuffer>().Build();
                }
            }
            """
        ).Where(diagnostic => diagnostic.Id == "MII0009").ToArray();

        Assert.That(diagnostics, Has.Length.EqualTo(2));
    }

    [Test]
    public void CachesRequiredMembersSeparatelyForConstructedInterfaces()
    {
        var diagnostics = AnalyzeAndGetDiagnostics<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer<T>
            {
                void Write(T value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer<int>>().Build();
                    _ = Implementation.Of<IBuffer<string>>().Build();
                }
            }
            """
        ).Where(diagnostic => diagnostic.Id == "MII0009").ToArray();

        Assert.That(diagnostics, Has.Length.EqualTo(2));
        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.GetMessage()),
            Has.Some.Contains("Write(int value)")
        );
        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.GetMessage()),
            Has.Some.Contains("Write(string value)")
        );
    }

    [Test]
    public void DoesNotReportMissingMembersForConfiguredMethodOverloads()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(int value);
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Write((int value) => { })
                        .Write((string value) => { })
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersWhenDelegateIncludesEventDispatcher()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using System;
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                event EventHandler Changed;
                string Read();
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Read(_ => "")
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotTreatUserDefinedEventDispatcherAsGeneratedDispatcher()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public sealed class EventDispatcher
            {
            }

            public interface IBuffer
            {
                void Write(EventDispatcher dispatcher);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Write(dispatcher => { })
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersForConfiguredConstructedInterface()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer<T>
            {
                void Write(T value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer<int>>()
                        .Write(value => { })
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenBuildIsMissingIndexerDelegates()
    {
        var diagnostic = AnalyzeAndGetDiagnostics<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IGrid
            {
                string this[int x, int y] { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IGrid>().Build();
                }
            }
            """
        ).Single(diagnostic => diagnostic.Id == "MII0009");

        Assert.That(diagnostic.GetMessage(), Does.Contain("indexer 'this[int x, int y]'"));
    }

    [Test]
    public void DoesNotReportMissingMembersForConfiguredIndexer()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IGrid
            {
                string this[int x, int y] { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IGrid>()
                        .Indexer(
                            getter: (x, y) => "",
                            setter: (x, y, value) => { }
                        )
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersForConfiguredMergedInheritedProperty()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IReadBuffer
            {
                string Value { get; }
            }

            public interface IWriteBuffer
            {
                string Value { set; }
            }

            public interface IBuffer : IReadBuffer, IWriteBuffer
            {
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Value(
                            getter: () => "",
                            setter: value => { }
                        )
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersForPropertiesWithEventDispatcher()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using System;
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                event EventHandler Changed;
                string Value { get; set; }
                string this[int index] { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Value(
                            getter: _ => "",
                            setter: (_, value) => { }
                        )
                        .Indexer(
                            getter: (_, index) => "",
                            setter: (_, index, value) => { }
                        )
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersWhenAllowMissingImplementationIsUnknown()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(string value);
            }

            public class Test
            {
                public void M(bool allowMissingImplementation)
                {
                    _ = Implementation.Of<IBuffer>(allowMissingImplementation).Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }

    [Test]
    public void DoesNotReportMissingMembersForUnresolvedBuildInvocation()
    {
        var diagnosticIds = AnalyzeAndGetDiagnostics<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IBuffer
            {
                void Write(string value);
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<IBuffer>()
                        .Write(value => { })
                        .Build(42);
                }
            }
            """
        ).Select(diagnostic => diagnostic.Id).ToArray();

        Assert.That(diagnosticIds, Has.None.Matches("MII0009"));
    }

    [Test]
    public void DoesNotReportMissingMembersForConfiguredNestedGenericInterface()
    {
        AssertNoAnalyzerDiagnostic<ImplementationBuilderAnalyzer>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public class Outer<TOuter>
            {
                public interface IInner<TInner>
                {
                    TOuter GetOuter();
                    TInner GetInner();
                }
            }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<Outer<string>.IInner<int>>()
                        .GetOuter(() => "")
                        .GetInner(() => 0)
                        .Build();
                }
            }
            """,
            diagnosticId: "MII0009"
        );
    }
}
