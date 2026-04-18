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
  Incremental source generator plus analyzer assembly that discovers `Implementation.Of<T>()` usages, emits builders plus extension methods, and reports usage diagnostics for incomplete builder chains.
- `InlineInterface.Tests`
  NUnit test project that validates generated source, diagnostics, and internal validation behavior.

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
6. Analyze `Implementation.Of<T>()` usage chains and report diagnostics when the builder is stored, the chain does not end with `Build()`, or required delegates are missing while `allowMissingImplementation` is `false`.

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
- `MII0004`: method parameters with `ref`, `out`, `in`, or `params` are not supported
- `MII0005`: unexpected member kinds are rejected
- `MII0006`: target interface and containing types must be accessible from generated code
- `MII0007`: event delegate parameters only allow `in`; `ref`, `out`, and `params` are rejected
- `MII0008`: inline interface builders must stay in one fluent expression and end with `Build()`
- `MII0009`: when `allowMissingImplementation` is `false`, every required method/property/indexer delegate must be configured before `Build()`

## Generation Notes

- The builder stores delegates for each property accessor and method.
- Consumers are expected to keep builder usage inline: `Implementation.Of<T>()...Build()`.
- The analyzer intentionally discourages storing the intermediate builder in locals, fields, returns, or helper parameters.
- `ImplementationBuilderAnalyzer` currently uses `SyntaxNodeAction` plus the provided `context.SemanticModel`; avoid calling `Compilation.GetSemanticModel(...)` inside the analyzer to keep Roslyn analyzer warnings and binding overhead down.
- If the target interface contains events, generated code also includes `EventCollection` and `EventDispatcher`.
- When events exist, generated method/property delegates can receive `EventDispatcher` as a leading argument so implementations can raise events.
- Event delegate `in` modifiers are preserved in generated dispatcher methods.
- `allowMissingImplementation: false` means missing delegates cause `InvalidOperationException` during `Build()`, and the message includes the target interface/member context.
- The analyzer mirrors that runtime rule for single-expression chains and reports missing required delegates at compile time when possible.
- Missing delegates that still reach the implementation throw `NotImplementedException` when invoked, and the message includes the interface/member that was not configured.
- Source is currently assembled mostly through string-building helpers rather than syntax factories.

## Important Files

- `InlineInterface.Core/Implementation.cs`
- `InlineInterface.Core/ImplementationOf_1.cs`
- `InlineInterface.Core/Tag.cs`
- `InlineInterface.Generator/InlineInterfaceGenerator.cs`
- `InlineInterface.Generator/ImplementationBuilderAnalyzer.cs`
- `InlineInterface.Generator/InlineInterfaceDiagnostics.cs`
- `InlineInterface.Generator/InterfaceValidator.cs`
- `InlineInterface.Generator/SourceGenerationHelpers.cs`
- `InlineInterface.Generator/MethodCodeGenerator.cs`
- `InlineInterface.Generator/PropertyCodeGenerator.cs`
- `InlineInterface.Generator/EventCodeGenerator.cs`
- `InlineInterface.Tests/InlineInterfaceGeneratorDiagnosticTests.cs`
- `InlineInterface.Tests/InlineInterfaceBuilderAnalyzerTests.cs`
- `InlineInterface.Tests/InlineInterfaceGeneratorGenerationTests.cs`
- `InlineInterface.Tests/InlineInterfaceInternalValidationTests.cs`

## Test Status

Verified on 2026-04-19 with:

```powershell
dotnet test InlineInterface.sln
```

Result at analysis time:

- 35 tests passed
- 0 failed

## Notes For Future Work

- If new member kinds are added, update both validation and code generation paths.
- If the fluent API shape changes, update both generated extension naming and `ImplementationBuilderAnalyzer` member-matching logic.
- If analyzer logic is expanded, prefer keeping it on the existing syntax/semantic-model path unless there is a clear benefit to more complex operation/dataflow analysis.
- Changes to generated output should usually be covered by exact-string tests or fragment tests.
- Analyzer behavior should be covered with same-expression-chain tests before adding wider dataflow analysis.
- `SourceGenerationHelpers` is the main composition point and likely the first file to inspect during feature work.
