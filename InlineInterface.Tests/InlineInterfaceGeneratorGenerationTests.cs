namespace Macaron.InlineInterface.Tests;

public partial class InlineInterfaceGeneratorTests
{
    [Test]
    public void GeneratesBuilderAndExtensionsForNestedGenericInterface()
    {
        AssertGeneratedCodeContainsAll(
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
            expectedFragments:
            [
                "internal readonly struct Parent_1_IPerson_1Builder<T0, T1>",
                "where T0 : class",
                "where T1 : struct",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>\";",
                "private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)",
                "private static global::System.NotImplementedException CreateMissingInvocationDelegateException(string memberDescription)",
                "get => (_property_get_FirstName_0 ?? throw Parent_1_IPerson_1Builder<T0, T1>.CreateMissingInvocationDelegateException(\"property 'global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.FirstName' (getter)\"))();",
                "T1 global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.GetName() => (_method_GetName_0 ?? throw Parent_1_IPerson_1Builder<T0, T1>.CreateMissingInvocationDelegateException(\"method 'global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.GetName()'\"))();",
                "method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"method 'SetName(T1 name)'\"))",
                "property_set_FirstName_0: Property_Set_FirstName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'FirstName' (setter)\"))",
                "public static global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1> Build<T0, T1>(",
            ]
        );
    }

    [Test]
    public void GeneratesEventCollectionAndDispatcherForInterfaceWithEvents()
    {
        AssertGeneratedCodeContainsAll(
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
            expectedFragments:
            [
                "public sealed class EventCollection",
                "public sealed class EventDispatcher",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>\";",
                "get => (_property_get_LastName_0 ?? throw Parent_1_IPerson_1Builder<T0, T1>.CreateMissingInvocationDelegateException(\"property 'global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.LastName' (getter)\"))(_eventDispatcher);",
                "void global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.SetName(string name) => (_method_SetName_0 ?? throw Parent_1_IPerson_1Builder<T0, T1>.CreateMissingInvocationDelegateException(\"method 'global::Macaron.InlineInterface.Tests.Parent<T0>.IPerson<T1>.SetName(string name)'\"))(_eventDispatcher, name);",
                "property_get_LastName_0: Property_Get_LastName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'LastName' (getter)\"))",
                "method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"method 'SetName(string name)'\"))",
            ]
        );
    }

    [Test]
    public void GeneratesEventDispatcherThatPreservesInModifiers()
    {
        AssertGeneratedCodeContainsAll(
            sourceCode:
            """
            using Macaron.InlineInterface;

            namespace Macaron.InlineInterface.Tests;

            public delegate void BufferChangedHandler(in int previous);

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
            expectedFragments:
            [
                "public global::Macaron.InlineInterface.Tests.BufferChangedHandler? Changed_0;",
                "public void Changed(in int previous)",
                "if (_eventCollection.Changed_0 != null) _eventCollection.Changed_0(in previous);",
                "event global::Macaron.InlineInterface.Tests.BufferChangedHandler? global::Macaron.InlineInterface.Tests.IBuffer.Changed",
            ]
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
        AssertGeneratedCodeContainsAll(
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

                    string GetLastName(int? index);
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
            expectedFragments:
            [
                "internal readonly struct Foo_1_IFooBar_2Builder<T0, T1, T2>",
                "public sealed class EventCollection",
                "public sealed class EventDispatcher",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.Foo<T0>.IFooBar<T1, T2>\";",
                "string global::Macaron.InlineInterface.Tests.Inner.IFoo<T1>.GetLastName() => (_method_GetLastName_0 ?? throw Foo_1_IFooBar_2Builder<T0, T1, T2>.CreateMissingInvocationDelegateException(\"method 'global::Macaron.InlineInterface.Tests.Inner.IFoo<T1>.GetLastName()'\"))(_eventDispatcher);",
                "string global::Macaron.InlineInterface.Tests.IBar<int>.GetFirstName() => (_method_GetFirstName_0 ?? throw Foo_1_IFooBar_2Builder<T0, T1, T2>.CreateMissingInvocationDelegateException(\"method 'global::Macaron.InlineInterface.Tests.IBar<int>.GetFirstName()'\"))(_eventDispatcher);",
                "string global::Macaron.InlineInterface.Tests.IBaz<T2>.GetLastName(int? index) => (_method_GetLastName_1 ?? throw Foo_1_IFooBar_2Builder<T0, T1, T2>.CreateMissingInvocationDelegateException(\"method 'global::Macaron.InlineInterface.Tests.IBaz<T2>.GetLastName(int? index)'\"))(_eventDispatcher, index);",
                "method_GetFirstName_0: Method_GetFirstName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"method 'GetFirstName()'\"))",
                "method_GetLastName_1: Method_GetLastName_1 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"method 'GetLastName(int? index)'\"))",
                "method_SetName_0: Method_SetName_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"method 'SetName(string name)'\"))",
            ]
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
                "private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)",
                "property_get_Value_0: Property_Get_Value_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'Value' (getter)\"))",
                "property_set_Value_0: Property_Set_Value_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'Value' (setter)\"))",
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
                "private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)",
                "property_get_Value_0: Property_Get_Value_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'Value' (getter)\"))",
                "property_get_Value_1: Property_Get_Value_1 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'Value' (getter)\"))",
                "property_set_Value_1: Property_Set_Value_1 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"property 'Value' (setter)\"))",
            ]
        );
    }

    [Test]
    public void GeneratesIndexerBuilderAndExtensions()
    {
        AssertGeneratedCodeContainsAll(
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
            expectedFragments:
            [
                "internal readonly struct IBufferBuilder",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.IBuffer\";",
                "get => (_property_get_Indexer_0 ?? throw IBufferBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IBuffer.this[int index]' (getter)\"))(index);",
                "set => (_property_set_Indexer_0 ?? throw IBufferBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IBuffer.this[int index]' (setter)\"))(index, value);",
                "property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int index]' (getter)\"))",
                "property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int index]' (setter)\"))",
            ]
        );
    }

    [Test]
    public void GeneratesIndexerUsingEventDispatcherWhenInterfaceHasEvents()
    {
        AssertGeneratedCodeContainsAll(
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
            expectedFragments:
            [
                "public sealed class EventCollection",
                "public sealed class EventDispatcher",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.IBuffer\";",
                "get => (_property_get_Indexer_0 ?? throw IBufferBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IBuffer.this[int index]' (getter)\"))(_eventDispatcher, index);",
                "set => (_property_set_Indexer_0 ?? throw IBufferBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IBuffer.this[int index]' (setter)\"))(_eventDispatcher, index, value);",
                "property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int index]' (getter)\"))",
                "property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int index]' (setter)\"))",
            ]
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
                "private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)",
                "property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int index]' (getter)\"))",
                "property_get_Indexer_1: Property_Get_Indexer_1 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[string key]' (getter)\"))",
                "property_set_Indexer_1: Property_Set_Indexer_1 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[string key]' (setter)\"))",
            ]
        );
    }

    [Test]
    public void GeneratesIndexerBuilderAndExtensionsForMultiParameterIndexer()
    {
        AssertGeneratedCodeContainsAll(
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
            expectedFragments:
            [
                "internal readonly struct IGridBuilder",
                "private const string InterfaceDisplayName = \"global::Macaron.InlineInterface.Tests.IGrid\";",
                "get => (_property_get_Indexer_0 ?? throw IGridBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IGrid.this[int x, int y]' (getter)\"))(x, y);",
                "set => (_property_set_Indexer_0 ?? throw IGridBuilder.CreateMissingInvocationDelegateException(\"indexer 'global::Macaron.InlineInterface.Tests.IGrid.this[int x, int y]' (setter)\"))(x, y, value);",
                "property_get_Indexer_0: Property_Get_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int x, int y]' (getter)\"))",
                "property_set_Indexer_0: Property_Set_Indexer_0 ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException(\"indexer 'this[int x, int y]' (setter)\"))",
            ]
        );
    }
}
