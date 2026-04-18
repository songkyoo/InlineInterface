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
}
