namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    [Test]
    public void ReportsDiagnosticsWithoutBlockingSuccessfulGeneration()
    {
        var (diagnostics, generatedCodes) = CompileAndGetResults<InlineInterfaceGenerator>(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public class NotAnInterface { }

            public interface IUnsupported
            {
                void Write<T>(T value);
            }

            public interface IBuffer { }

            public class Test
            {
                public void M()
                {
                    _ = Implementation.Of<NotAnInterface>();
                    _ = Implementation.Of<IUnsupported>();
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            additionalAssemblies: [typeof(ImplementationOf<>).Assembly]
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(diagnostic => diagnostic.Id),
                Has.Some.EqualTo("MII0001")
            );
            Assert.That(
                diagnostics.Select(diagnostic => diagnostic.Id),
                Has.Some.EqualTo("MII0003")
            );
            Assert.That(generatedCodes, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void ReportsDiagnosticWhenEventDelegateContainsOutParameter()
    {
        AssertDiagnostic(
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
            expectedDiagnosticId: "MII0007"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenEventDelegateContainsRefParameter()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public delegate void BufferChangedHandler(ref int current);

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
            expectedDiagnosticId: "MII0007"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenEventDelegateContainsParamsParameter()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public delegate void BufferChangedHandler(params int[] current);

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
            expectedDiagnosticId: "MII0007"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetTypeIsNotInterface()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public class NotAnInterface { }

            public class Test { void M() => Implementation.Of<NotAnInterface>(); }
            """,
            expectedDiagnosticId: "MII0001"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetTypeIsNullable()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface ITest { }

            public class Test { void M() { Implementation.Of<ITest?>(); } }
            """,
            expectedDiagnosticId: "MII0002"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceContainsGenericMethod()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IWithGenericMethod { void M<T>(T arg); }

            public class Test { void M() => Implementation.Of<IWithGenericMethod>(); }
            """,
            expectedDiagnosticId: "MII0003"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceContainsRefParameter()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IWithRefParam { void M(ref int arg); }

            public class Test { void M() => Implementation.Of<IWithRefParam>(); }
            """,
            expectedDiagnosticId: "MII0004"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceContainsOutParameter()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IWithOutParam
            {
                void M(out int value);
            }

            public class Test
            {
                void M() => Implementation.Of<IWithOutParam>();
            }
            """,
            expectedDiagnosticId: "MII0004"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceContainsParamsParameter()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public interface IWithParamsParam
            {
                void M(params int[] values);
            }

            public class Test
            {
                void M() => Implementation.Of<IWithParamsParam>();
            }
            """,
            expectedDiagnosticId: "MII0004"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceIsPrivate()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public class Container
            {
                private interface IHidden
                {
                    void M();
                }

                public void Test()
                {
                    _ = Implementation.Of<IHidden>();
                }
            }
            """,
            expectedDiagnosticId: "MII0006"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenTargetInterfaceIsProtected()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public class Container
            {
                protected interface IHidden
                {
                    void M();
                }

                public void Test()
                {
                    _ = Implementation.Of<IHidden>();
                }
            }
            """,
            expectedDiagnosticId: "MII0006"
        );
    }

    [Test]
    public void ReportsDiagnosticWhenContainingTypeOfTargetInterfaceIsPrivate()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using Macaron.InlineInterface;

            public class Container
            {
                private class HiddenContainer
                {
                    public interface IInner
                    {
                        void M();
                    }
                }

                public void Test()
                {
                    _ = Implementation.Of<HiddenContainer.IInner>();
                }
            }
            """,
            expectedDiagnosticId: "MII0006"
        );
    }
}
