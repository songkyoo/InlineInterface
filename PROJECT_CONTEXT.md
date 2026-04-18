# InlineInterface Project Context

## Purpose

This project provides a Roslyn source generator that lets consumers build interface implementations inline with delegate-based builder APIs.

Example direction:

```csharp
var value = Implementation
    .Of<IMyInterface>()
    .SomeMethod(() => 123)
    .Build();
```

The packaged library is published as `Macaron.InlineInterface`.

## Solution Structure

- `InlineInterface`
  Packaging project. Packs the runtime core assembly and the generator assembly together.
- `InlineInterface.Core`
  Minimal runtime API surface used by consumers.
- `InlineInterface.Generator`
  Incremental source generator that discovers `Implementation.Of<T>()` usages and emits builders plus extension methods.
- `InlineInterface.Tests`
  NUnit test project that validates generated source and diagnostics.

## Key Runtime Types

- `Implementation`
  Entry point for consumers. `Of<T>(bool allowMissingImplementation = false)` returns an `ImplementationOf<T>`.
- `ImplementationOf<T>`
  Lightweight value that carries the `allowMissingImplementation` option into generated extension methods.
- `Tag`
  Marker type used to disambiguate the generated `Build` API.

## Generator Flow

1. Find invocation expressions that resolve to `Macaron.InlineInterface.Implementation.Of<T>()`.
2. Extract the target type argument and verify that it is a non-nullable interface.
3. Validate supported members on the interface and its inherited interfaces.
4. Build `InterfaceContext` models for events, properties, and methods.
5. Generate:
   - a builder struct under `Macaron.InlineInterface.Generated...`
   - an internal `Impl` class implementing the interface
   - `ImplementationOfExtensions` methods for fluent configuration
   - an extension `Build()` method

## Supported Interface Members

- Instance methods
- Instance properties
- Indexers
- Events
- Inherited interface members
- Nested and generic interfaces

## Current Validation Rules

- `MII0001`: target type must be an interface
- `MII0002`: nullable interface targets are not supported
- `MII0003`: generic methods are not supported
- `MII0004`: `ref`, `out`, `in`, and `params` parameters are not supported
- `MII0005`: unexpected member kinds are rejected
- `MII0006`: target interface and containing types must be accessible from generated code

## Generation Notes

- The builder stores delegates for each property accessor and method.
- If the target interface contains events, generated code also includes `EventCollection` and `EventDispatcher`.
- `allowMissingImplementation: false` means missing delegates cause `InvalidOperationException` during `Build()`.
- Missing delegates that still reach the implementation throw `NotImplementedException` when invoked.
- Source is currently assembled mostly through string-building helpers rather than syntax factories.

## Important Files

- `InlineInterface.Core/Implementation.cs`
- `InlineInterface.Core/ImplementationOf_1.cs`
- `InlineInterface.Core/Tag.cs`
- `InlineInterface.Generator/InlineInterfaceGenerator.cs`
- `InlineInterface.Generator/SourceGenerationHelpers.cs`
- `InlineInterface.Generator/MethodCodeGenerator.cs`
- `InlineInterface.Generator/PropertyCodeGenerator.cs`
- `InlineInterface.Generator/EventCodeGenerator.cs`
- `InlineInterface.Tests/InlineInterfaceGeneratorTests.cs`

## Test Status

Verified on 2026-04-18 with:

```powershell
dotnet test InlineInterface.sln
```

Result at analysis time:

- 20 tests passed
- 0 failed

## Notes For Future Work

- If new member kinds are added, update both validation and code generation paths.
- Changes to generated output should usually be covered by exact-string tests or fragment tests.
- `SourceGenerationHelpers` is the main composition point and likely the first file to inspect during feature work.
