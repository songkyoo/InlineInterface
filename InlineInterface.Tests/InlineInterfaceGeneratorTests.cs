using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.InlineInterface.Tests;

[TestFixture]
public class InlineInterfaceGeneratorTests
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

    [Test]
    public void GeneratesBuilderAndExtensionsForNestedGenericInterface()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using System;

            namespace Macaron.InlineInterface.Tests;

            public class Foo { }

            public class Parent<T> where T : class
            {
                public interface IPerson<T> where T : struct
                {
                    T GetName();

                    void SetName(T name);

                    string? FirstName { get; internal set; }

                    string LastName { get; }
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var implementationOf = Implementation.Of<Parent<string>.IPerson<int>>();
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct Parent_1_IPerson_1Builder<T0, T1>
                    where T0 : class
                    where T1 : struct
                {
                    private sealed class Impl : global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>
                    {
                        private readonly global::System.Func<string?>? _property_get_FirstName_0;
                        private readonly global::System.Action<string?>? _property_set_FirstName_0;
                        private readonly global::System.Func<string>? _property_get_LastName_0;
                        private readonly global::System.Func<T1>? _method_GetName_0;
                        private readonly global::System.Action<T1>? _method_SetName_0;

                        public Impl(
                            global::System.Func<string?>? property_get_FirstName_0,
                            global::System.Action<string?>? property_set_FirstName_0,
                            global::System.Func<string>? property_get_LastName_0,
                            global::System.Func<T1>? method_GetName_0,
                            global::System.Action<T1>? method_SetName_0)
                        {
                            _property_get_FirstName_0 = property_get_FirstName_0;
                            _property_set_FirstName_0 = property_set_FirstName_0;
                            _property_get_LastName_0 = property_get_LastName_0;
                            _method_GetName_0 = method_GetName_0;
                            _method_SetName_0 = method_SetName_0;
                        }

                        string? global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.FirstName
                        {
                            get => (_property_get_FirstName_0 ?? throw new global::System.NotImplementedException())();
                            set => (_property_set_FirstName_0 ?? throw new global::System.NotImplementedException())(value);
                        }

                        string global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.LastName
                        {
                            get => (_property_get_LastName_0 ?? throw new global::System.NotImplementedException())();
                        }

                        T1 global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.GetName() => (_method_GetName_0 ?? throw new global::System.NotImplementedException())();

                        void global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.SetName(T1 name) => (_method_SetName_0 ?? throw new global::System.NotImplementedException())(name);
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<string?>? Property_Get_FirstName_0 { get; init; } = null;

                    private readonly global::System.Action<string?>? Property_Set_FirstName_0 { get; init; } = null;

                    private readonly global::System.Func<string>? Property_Get_LastName_0 { get; init; } = null;

                    private readonly global::System.Func<T1>? Method_GetName_0 { get; init; } = null;

                    private readonly global::System.Action<T1>? Method_SetName_0 { get; init; } = null;

                    public Parent_1_IPerson_1Builder(
                        bool allowMissingImplementation,
                        global::System.Func<string?>? property_get_FirstName_0 = null,
                        global::System.Action<string?>? property_set_FirstName_0 = null,
                        global::System.Func<string>? property_get_LastName_0 = null,
                        global::System.Func<T1>? method_GetName_0 = null,
                        global::System.Action<T1>? method_SetName_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Property_Get_FirstName_0 = property_get_FirstName_0;
                        Property_Set_FirstName_0 = property_set_FirstName_0;
                        Property_Get_LastName_0 = property_get_LastName_0;
                        Method_GetName_0 = method_GetName_0;
                        Method_SetName_0 = method_SetName_0;
                    }

                    public Parent_1_IPerson_1Builder<T0, T1> FirstName(global::System.Func<string?> getter, global::System.Action<string?> setter) => this with { Property_Get_FirstName_0 = getter, Property_Set_FirstName_0 = setter };

                    public Parent_1_IPerson_1Builder<T0, T1> LastName(global::System.Func<string> getter) => this with { Property_Get_LastName_0 = getter };

                    public Parent_1_IPerson_1Builder<T0, T1> GetName(global::System.Func<T1> impl) => this with { Method_GetName_0 = impl };

                    public Parent_1_IPerson_1Builder<T0, T1> SetName(global::System.Action<T1> impl) => this with { Method_SetName_0 = impl };

                    public global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1> Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            property_get_FirstName_0: Property_Get_FirstName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            property_set_FirstName_0: Property_Set_FirstName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            property_get_LastName_0: Property_Get_LastName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_GetName_0: Method_GetName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> FirstName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Func<string?> getter,
                        global::System.Action<string?> setter)
                        where T0 : class
                        where T1 : struct
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_FirstName_0: getter, property_set_FirstName_0: setter);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> LastName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Func<string> getter)
                        where T0 : class
                        where T1 : struct
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_LastName_0: getter);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> GetName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Func<T1> impl)
                        where T0 : class
                        where T1 : struct
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_GetName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> SetName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Action<T1> impl)
                        where T0 : class
                        where T1 : struct
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_SetName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1> Build<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                        where T0 : class
                        where T1 : struct
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
        );
    }

    [Test]
    public void GeneratesEventCollectionAndDispatcherForInterfaceWithEvents()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using System;

            namespace Macaron.InlineInterface.Tests;

            public class Foo { }

            public class Parent<T>
            {
                public interface IPerson<T>
                {
                    event Func<Foo?, string> NameChanged;

                    void SetName(string name);

                    string LastName { get; }
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var implementationOf = Implementation.Of<Parent<string>.IPerson<int>>();
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct Parent_1_IPerson_1Builder<T0, T1>
                {
                    public sealed class EventCollection
                    {
                        public global::System.Func<global::Macaron.InlineInterface.Tests.Foo?, string>? NameChanged_0;
                    }

                    public sealed class EventDispatcher
                    {
                        private readonly EventCollection _eventCollection;

                        public EventDispatcher(EventCollection eventCollection)
                        {
                            _eventCollection = eventCollection;
                        }

                        public string? InvokeNameChanged(global::Macaron.InlineInterface.Tests.Foo? arg)
                        {
                            if (_eventCollection.NameChanged_0 == null) return default;
                            return _eventCollection.NameChanged_0(arg);
                        }
                    }

                    private sealed class Impl : global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>
                    {
                        private readonly EventCollection _eventCollection = new();
                        private readonly EventDispatcher _eventDispatcher;
                        private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? _property_get_LastName_0;
                        private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? _method_SetName_0;

                        public Impl(
                            global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? property_get_LastName_0,
                            global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? method_SetName_0)
                        {
                            _eventDispatcher = new EventDispatcher(_eventCollection);
                            _property_get_LastName_0 = property_get_LastName_0;
                            _method_SetName_0 = method_SetName_0;
                        }

                        event global::System.Func<global::Macaron.InlineInterface.Tests.Foo?, string>? global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.NameChanged
                        {
                            add => _eventCollection.NameChanged_0 += value;
                            remove => _eventCollection.NameChanged_0 -= value;
                        }

                        string global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.LastName
                        {
                            get => (_property_get_LastName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher);
                        }

                        void global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.SetName(string name) => (_method_SetName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher, name);
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? Property_Get_LastName_0 { get; init; } = null;

                    private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? Method_SetName_0 { get; init; } = null;

                    public Parent_1_IPerson_1Builder(
                        bool allowMissingImplementation,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? property_get_LastName_0 = null,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string>? method_SetName_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Property_Get_LastName_0 = property_get_LastName_0;
                        Method_SetName_0 = method_SetName_0;
                    }

                    public Parent_1_IPerson_1Builder<T0, T1> LastName(global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string> getter) => this with { Property_Get_LastName_0 = getter };

                    public Parent_1_IPerson_1Builder<T0, T1> SetName(global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string> impl) => this with { Method_SetName_0 = impl };

                    public global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1> Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            property_get_LastName_0: Property_Get_LastName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> LastName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string> getter)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_LastName_0: getter);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1> SetName<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>.EventDispatcher, string> impl)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_SetName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1> Build<T0, T1>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Parent_1_IPerson_1Builder<T0, T1>(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
        );
    }

    [Test]
    public void GeneratesDistinctTypeParameterNamesForNestedTypesWithDuplicateGenericParameterNames()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public class Outer<T>
            {
                public interface IInner<T>
                {
                    T GetValue();
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var implementationOf = Implementation.Of<Outer<string>.IInner<int>>();
                }
            }
            """,
            expectedFragments:
            [
                "internal readonly struct Outer_1_IInner_1Builder<T0, T1>",
                "private sealed class Impl : global::Macaron.InlineInterface.Tests.Outer<T0>.IInner<T1>",
                "private readonly global::System.Func<T1>? _method_GetValue_0;",
                "public Outer_1_IInner_1Builder<T0, T1> GetValue(global::System.Func<T1> impl) => this with { Method_GetValue_0 = impl };",
                "public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Outer_1_IInner_1Builder<T0, T1> GetValue<T0, T1>(",
                "public static global::Macaron.InlineInterface.Tests.Outer<T0>.IInner<T1> Build<T0, T1>(",
            ]
        );
    }

    [Test]
    public void GeneratesTypeParameterConstraintsInCanonicalOrder()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using System;
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public class DisposableFactoryProduct : IDisposable
            {
                public void Dispose() { }
            }

            public interface IFactory<T>
                where T : class, IDisposable, new()
            {
                T Create();
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var implementationOf = Implementation.Of<IFactory<DisposableFactoryProduct>>();
                }
            }
            """,
            expectedFragments:
            [
                "internal readonly struct IFactory_1Builder<T>",
                "where T : class, global::System.IDisposable, new()",
                "private sealed class Impl : global::Macaron.InlineInterface.Tests.IFactory<T>",
                "public IFactory_1Builder<T> Create(global::System.Func<T> impl) => this with { Method_Create_0 = impl };",
                "public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IFactory_1Builder<T> Create<T>(",
                "public static global::Macaron.InlineInterface.Tests.IFactory<T> Build<T>(",
            ]
        );
    }

    [Test]
    public void GeneratesMembersFromInheritedInterfaces()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using System;

            namespace Macaron.InlineInterface.Tests
            {
                namespace Inner
                {
                    public interface IFoo<T>
                    {
                        event Func<T?> NameChanged;

                        string GetLastName();
                    }
                }

                public interface IBar<T>
                {
                    event Action NameChanged;

                    string GetFirstName();

                    string GetLastName();
                }

                public interface IBaz<T> : IBar<int>
                {
                    void SetName(string name);

                    string GetLastName(int index);
                }

                public class Foo<T>
                {
                    public interface IFooBar<T, U> : Inner.IFoo<T>, IBaz<U>
                    {
                        event Action<string, int> ValueChanged;
                    }
                }

                public class TestClass
                {
                    public void TestMethod()
                    {
                        var implementationOf = Implementation.Of<Foo<float>.IFooBar<string, int>>();
                    }
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct Foo_1_IFooBar_2Builder<T0, T1, T2>
                {
                    public sealed class EventCollection
                    {
                        public global::System.Func<T1?>? NameChanged_0;
                        public global::System.Action? NameChanged_1;
                        public global::System.Action<string, int>? ValueChanged_0;
                    }

                    public sealed class EventDispatcher
                    {
                        private readonly EventCollection _eventCollection;

                        public EventDispatcher(EventCollection eventCollection)
                        {
                            _eventCollection = eventCollection;
                        }

                        public T1? InvokeNameChanged()
                        {
                            if (_eventCollection.NameChanged_0 == null) return default;
                            return _eventCollection.NameChanged_0();
                        }

                        public void RaiseNameChanged()
                        {
                            if (_eventCollection.NameChanged_1 == null) return;
                            _eventCollection.NameChanged_1();
                        }

                        public void RaiseValueChanged(string arg1, int arg2)
                        {
                            if (_eventCollection.ValueChanged_0 == null) return;
                            _eventCollection.ValueChanged_0(arg1, arg2);
                        }
                    }

                    private sealed class Impl : global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>
                    {
                        private readonly EventCollection _eventCollection = new();
                        private readonly EventDispatcher _eventDispatcher;
                        private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? _method_GetFirstName_0;
                        private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? _method_GetLastName_0;
                        private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string>? _method_GetLastName_1;
                        private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? _method_SetName_0;

                        public Impl(
                            global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_GetFirstName_0,
                            global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_GetLastName_0,
                            global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string>? method_GetLastName_1,
                            global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_SetName_0)
                        {
                            _eventDispatcher = new EventDispatcher(_eventCollection);
                            _method_GetFirstName_0 = method_GetFirstName_0;
                            _method_GetLastName_0 = method_GetLastName_0;
                            _method_GetLastName_1 = method_GetLastName_1;
                            _method_SetName_0 = method_SetName_0;
                        }

                        event global::System.Action<string, int>? global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>.ValueChanged
                        {
                            add => _eventCollection.ValueChanged_0 += value;
                            remove => _eventCollection.ValueChanged_0 -= value;
                        }

                        event global::System.Func<T1?>? global::Macaron.InlineInterface.Tests.Inner.IFoo<T1>.NameChanged
                        {
                            add => _eventCollection.NameChanged_0 += value;
                            remove => _eventCollection.NameChanged_0 -= value;
                        }

                        event global::System.Action? global::Macaron.InlineInterface.Tests.IBar<int>.NameChanged
                        {
                            add => _eventCollection.NameChanged_1 += value;
                            remove => _eventCollection.NameChanged_1 -= value;
                        }

                        string global::Macaron.InlineInterface.Tests.Inner.IFoo<T1>.GetLastName() => (_method_GetLastName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher);

                        void global::Macaron.InlineInterface.Tests.IBaz<T2>.SetName(string name) => (_method_SetName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher, name);

                        string global::Macaron.InlineInterface.Tests.IBaz<T2>.GetLastName(int index) => (_method_GetLastName_1 ?? throw new global::System.NotImplementedException())(_eventDispatcher, index);

                        string global::Macaron.InlineInterface.Tests.IBar<int>.GetFirstName() => (_method_GetFirstName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher);

                        string global::Macaron.InlineInterface.Tests.IBar<int>.GetLastName() => (_method_GetLastName_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher);
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? Method_GetFirstName_0 { get; init; } = null;

                    private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? Method_GetLastName_0 { get; init; } = null;

                    private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string>? Method_GetLastName_1 { get; init; } = null;

                    private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? Method_SetName_0 { get; init; } = null;

                    public Foo_1_IFooBar_2Builder(
                        bool allowMissingImplementation,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_GetFirstName_0 = null,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_GetLastName_0 = null,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string>? method_GetLastName_1 = null,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string>? method_SetName_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Method_GetFirstName_0 = method_GetFirstName_0;
                        Method_GetLastName_0 = method_GetLastName_0;
                        Method_GetLastName_1 = method_GetLastName_1;
                        Method_SetName_0 = method_SetName_0;
                    }

                    public Foo_1_IFooBar_2Builder<T0, T1, T2> GetFirstName(global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl) => this with { Method_GetFirstName_0 = impl };

                    public Foo_1_IFooBar_2Builder<T0, T1, T2> GetLastName(global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl) => this with { Method_GetLastName_0 = impl };

                    public Foo_1_IFooBar_2Builder<T0, T1, T2> GetLastName(global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string> impl) => this with { Method_GetLastName_1 = impl };

                    public Foo_1_IFooBar_2Builder<T0, T1, T2> SetName(global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl) => this with { Method_SetName_0 = impl };

                    public global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2> Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            method_GetFirstName_0: Method_GetFirstName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_GetLastName_0: Method_GetLastName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_GetLastName_1: Method_GetLastName_1 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2> GetFirstName<T0, T1, T2>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>> implementationOf,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_GetFirstName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2> GetLastName<T0, T1, T2>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>> implementationOf,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_GetLastName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2> GetLastName<T0, T1, T2>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>> implementationOf,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, int, string> impl)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_GetLastName_1: impl);
                    }

                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2> SetName<T0, T1, T2>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>> implementationOf,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>.EventDispatcher, string> impl)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>(allowMissingImplementation: implementationOf.AllowMissingImplementation, method_SetName_0: impl);
                    }

                    public static global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2> Build<T0, T1, T2>(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.Foo_1_IFooBar_2Builder<T0, T1, T2>(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
        );
    }

    [Test]
    public void GeneratesSharedPropertyAccessorsForInheritedPropertiesWithDifferentAccessorCombinations()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IFoo
            {
                int Value { get; }
            }

            public interface IBar
            {
                int Value { get; set; }
            }

            public interface IBaz : IFoo, IBar
            {
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBaz>();
                }
            }
            """,
            expectedFragments:
            [
                "private readonly global::System.Func<int>? _property_get_Value_0;",
                "private readonly global::System.Action<int>? _property_set_Value_0;",
                "int global::Macaron.InlineInterface.Tests.IFoo.Value",
                "int global::Macaron.InlineInterface.Tests.IBar.Value",
                "public IBazBuilder Value(global::System.Func<int> getter, global::System.Action<int> setter) => this with { Property_Get_Value_0 = getter, Property_Set_Value_0 = setter };",
                "public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBazBuilder Value(",
                "property_get_Value_0: Property_Get_Value_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
                "property_set_Value_0: Property_Set_Value_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
            ]
        );
    }

    [Test]
    public void GeneratesDistinctPropertyAccessorNamesForInheritedPropertiesWithSameNameButDifferentTypes()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IFoo
            {
                int Value { get; }
            }

            public interface IBar
            {
                string Value { get; set; }
            }

            public interface IBaz : IFoo, IBar
            {
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBaz>();
                }
            }
            """,
            expectedFragments:
            [
                "private readonly global::System.Func<int>? _property_get_Value_0;",
                "private readonly global::System.Func<string>? _property_get_Value_1;",
                "private readonly global::System.Action<string>? _property_set_Value_1;",
                "int global::Macaron.InlineInterface.Tests.IFoo.Value",
                "string global::Macaron.InlineInterface.Tests.IBar.Value",
                "public IBazBuilder Value(global::System.Func<int> getter) => this with { Property_Get_Value_0 = getter };",
                "public IBazBuilder Value(global::System.Func<string> getter, global::System.Action<string> setter) => this with { Property_Get_Value_1 = getter, Property_Set_Value_1 = setter };",
                "public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBazBuilder Value(",
                "property_get_Value_0: Property_Get_Value_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
                "property_get_Value_1: Property_Get_Value_1 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
                "property_set_Value_1: Property_Set_Value_1 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
            ]
        );
    }

    [Test]
    public void GeneratesIndexerBuilderAndExtensions()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                string this[int index] { get; set; }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct IBufferBuilder
                {
                    private sealed class Impl : global::Macaron.InlineInterface.Tests.IBuffer
                    {
                        private readonly global::System.Func<int, string>? _property_get_Indexer_0;
                        private readonly global::System.Action<int, string>? _property_set_Indexer_0;

                        public Impl(
                            global::System.Func<int, string>? property_get_Indexer_0,
                            global::System.Action<int, string>? property_set_Indexer_0)
                        {
                            _property_get_Indexer_0 = property_get_Indexer_0;
                            _property_set_Indexer_0 = property_set_Indexer_0;
                        }

                        string global::Macaron.InlineInterface.Tests.IBuffer.this[int index]
                        {
                            get => (_property_get_Indexer_0 ?? throw new global::System.NotImplementedException())(index);
                            set => (_property_set_Indexer_0 ?? throw new global::System.NotImplementedException())(index, value);
                        }
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<int, string>? Property_Get_Indexer_0 { get; init; } = null;

                    private readonly global::System.Action<int, string>? Property_Set_Indexer_0 { get; init; } = null;

                    public IBufferBuilder(
                        bool allowMissingImplementation,
                        global::System.Func<int, string>? property_get_Indexer_0 = null,
                        global::System.Action<int, string>? property_set_Indexer_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Property_Get_Indexer_0 = property_get_Indexer_0;
                        Property_Set_Indexer_0 = property_set_Indexer_0;
                    }

                    public IBufferBuilder Indexer(global::System.Func<int, string> getter, global::System.Action<int, string> setter) => this with { Property_Get_Indexer_0 = getter, Property_Set_Indexer_0 = setter };

                    public global::Macaron.InlineInterface.Tests.IBuffer Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder Indexer(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IBuffer> implementationOf,
                        global::System.Func<int, string> getter,
                        global::System.Action<int, string> setter)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_Indexer_0: getter, property_set_Indexer_0: setter);
                    }

                    public static global::Macaron.InlineInterface.Tests.IBuffer Build(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IBuffer> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
        );
    }

    [Test]
    public void GeneratesIndexerUsingEventDispatcherWhenInterfaceHasEvents()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using System;
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                event Action Changed;

                string this[int index] { get; set; }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct IBufferBuilder
                {
                    public sealed class EventCollection
                    {
                        public global::System.Action? Changed_0;
                    }

                    public sealed class EventDispatcher
                    {
                        private readonly EventCollection _eventCollection;

                        public EventDispatcher(EventCollection eventCollection)
                        {
                            _eventCollection = eventCollection;
                        }

                        public void RaiseChanged()
                        {
                            if (_eventCollection.Changed_0 == null) return;
                            _eventCollection.Changed_0();
                        }
                    }

                    private sealed class Impl : global::Macaron.InlineInterface.Tests.IBuffer
                    {
                        private readonly EventCollection _eventCollection = new();
                        private readonly EventDispatcher _eventDispatcher;
                        private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? _property_get_Indexer_0;
                        private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? _property_set_Indexer_0;

                        public Impl(
                            global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? property_get_Indexer_0,
                            global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? property_set_Indexer_0)
                        {
                            _eventDispatcher = new EventDispatcher(_eventCollection);
                            _property_get_Indexer_0 = property_get_Indexer_0;
                            _property_set_Indexer_0 = property_set_Indexer_0;
                        }

                        event global::System.Action? global::Macaron.InlineInterface.Tests.IBuffer.Changed
                        {
                            add => _eventCollection.Changed_0 += value;
                            remove => _eventCollection.Changed_0 -= value;
                        }

                        string global::Macaron.InlineInterface.Tests.IBuffer.this[int index]
                        {
                            get => (_property_get_Indexer_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher, index);
                            set => (_property_set_Indexer_0 ?? throw new global::System.NotImplementedException())(_eventDispatcher, index, value);
                        }
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? Property_Get_Indexer_0 { get; init; } = null;

                    private readonly global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? Property_Set_Indexer_0 { get; init; } = null;

                    public IBufferBuilder(
                        bool allowMissingImplementation,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? property_get_Indexer_0 = null,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string>? property_set_Indexer_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Property_Get_Indexer_0 = property_get_Indexer_0;
                        Property_Set_Indexer_0 = property_set_Indexer_0;
                    }

                    public IBufferBuilder Indexer(global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string> getter, global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string> setter) => this with { Property_Get_Indexer_0 = getter, Property_Set_Indexer_0 = setter };

                    public global::Macaron.InlineInterface.Tests.IBuffer Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder Indexer(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IBuffer> implementationOf,
                        global::System.Func<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string> getter,
                        global::System.Action<global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder.EventDispatcher, int, string> setter)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_Indexer_0: getter, property_set_Indexer_0: setter);
                    }

                    public static global::Macaron.InlineInterface.Tests.IBuffer Build(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IBuffer> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IBufferBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
        );
    }

    [Test]
    public void GeneratesOverloadedIndexerMethodsForDifferentIndexerSignatures()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IBuffer
            {
                string this[int index] { get; }
                string this[string key] { get; set; }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IBuffer>();
                }
            }
            """,
            expectedFragments:
            [
                "private readonly global::System.Func<int, string>? _property_get_Indexer_0;",
                "private readonly global::System.Func<string, string>? _property_get_Indexer_1;",
                "private readonly global::System.Action<string, string>? _property_set_Indexer_1;",
                "string global::Macaron.InlineInterface.Tests.IBuffer.this[int index]",
                "string global::Macaron.InlineInterface.Tests.IBuffer.this[string key]",
                "public IBufferBuilder Indexer(global::System.Func<int, string> getter) => this with { Property_Get_Indexer_0 = getter };",
                "public IBufferBuilder Indexer(global::System.Func<string, string> getter, global::System.Action<string, string> setter) => this with { Property_Get_Indexer_1 = getter, Property_Set_Indexer_1 = setter };",
                "property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
                "property_get_Indexer_1: Property_Get_Indexer_1 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
                "property_set_Indexer_1: Property_Set_Indexer_1 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())",
            ]
        );
    }

    [Test]
    public void GeneratesIndexerBuilderAndExtensionsForMultiParameterIndexer()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public interface IGrid
            {
                string this[int x, int y] { get; set; }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    _ = Implementation.Of<IGrid>();
                }
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests
            {
                internal readonly struct IGridBuilder
                {
                    private sealed class Impl : global::Macaron.InlineInterface.Tests.IGrid
                    {
                        private readonly global::System.Func<int, int, string>? _property_get_Indexer_0;
                        private readonly global::System.Action<int, int, string>? _property_set_Indexer_0;

                        public Impl(
                            global::System.Func<int, int, string>? property_get_Indexer_0,
                            global::System.Action<int, int, string>? property_set_Indexer_0)
                        {
                            _property_get_Indexer_0 = property_get_Indexer_0;
                            _property_set_Indexer_0 = property_set_Indexer_0;
                        }

                        string global::Macaron.InlineInterface.Tests.IGrid.this[int x, int y]
                        {
                            get => (_property_get_Indexer_0 ?? throw new global::System.NotImplementedException())(x, y);
                            set => (_property_set_Indexer_0 ?? throw new global::System.NotImplementedException())(x, y, value);
                        }
                    }

                    private readonly bool _allowMissingImplementation;

                    private readonly global::System.Func<int, int, string>? Property_Get_Indexer_0 { get; init; } = null;

                    private readonly global::System.Action<int, int, string>? Property_Set_Indexer_0 { get; init; } = null;

                    public IGridBuilder(
                        bool allowMissingImplementation,
                        global::System.Func<int, int, string>? property_get_Indexer_0 = null,
                        global::System.Action<int, int, string>? property_set_Indexer_0 = null)
                    {
                        _allowMissingImplementation = allowMissingImplementation;

                        Property_Get_Indexer_0 = property_get_Indexer_0;
                        Property_Set_Indexer_0 = property_set_Indexer_0;
                    }

                    public IGridBuilder Indexer(global::System.Func<int, int, string> getter, global::System.Action<int, int, string> setter) => this with { Property_Get_Indexer_0 = getter, Property_Set_Indexer_0 = setter };

                    public global::Macaron.InlineInterface.Tests.IGrid Build(global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new Impl(
                            property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()),
                            property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException()));
                    }
                }
            }

            namespace Macaron.InlineInterface
            {
                internal static partial class ImplementationOfExtensions
                {
                    public static global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IGridBuilder Indexer(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IGrid> implementationOf,
                        global::System.Func<int, int, string> getter,
                        global::System.Action<int, int, string> setter)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IGridBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation, property_get_Indexer_0: getter, property_set_Indexer_0: setter);
                    }

                    public static global::Macaron.InlineInterface.Tests.IGrid Build(
                        this global::Macaron.InlineInterface.ImplementationOf<global::Macaron.InlineInterface.Tests.IGrid> implementationOf,
                        global::Macaron.InlineInterface.Tag _ = default)
                    {
                        return new global::Macaron.InlineInterface.Generated.Macaron.InlineInterface.Tests.IGridBuilder(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);
                    }
                }
            }

            """
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

            public class Test { void M() => Implementation.Of<ITest?>(); }
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
