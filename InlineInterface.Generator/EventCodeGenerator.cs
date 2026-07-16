using System.Collections.Immutable;

namespace Macaron.InlineInterface;

public sealed class EventCodeGenerator(
    ImmutableArray<EventGenerationModel> events,
    ImmutableArray<string> interfaceTypes,
    string indent
)
{
    public IEnumerable<string> GetEventCollectionFieldDeclarations()
    {
        foreach (var model in events)
        {
            yield return $"public {model.Type} {model.UniqueName};";
        }
    }

    public IEnumerable<ImmutableArray<string>> GetEventDispatcherImplementations()
    {
        foreach (var model in events)
        {
            var implementationBuilder = ImmutableArray.CreateBuilder<string>();

            implementationBuilder.Add($"public void {model.Name}({model.DispatcherParameters})");
            implementationBuilder.Add("{");

            var expression = $"_eventCollection.{model.UniqueName}({model.DispatcherArguments})";

            if (model.ReturnsVoid)
            {
                implementationBuilder.Add($"{indent}if (_eventCollection.{model.UniqueName} != null) {expression};");
            }
            else
            {
                implementationBuilder.Add($"{indent}@return = _eventCollection.{model.UniqueName} != null ? {expression} : default;");
            }

            implementationBuilder.Add("}");

            yield return implementationBuilder.ToImmutable();
        }
    }

    public ImmutableArray<string> GetInterfaceImplementation(EventImplementationModel implementation)
    {
        var model = events[implementation.EventIndex];
        var interfaceType = interfaceTypes[implementation.InterfaceTypeIndex];
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        implementationBuilder.Add($"event {model.Type} {interfaceType}.{model.Name}");
        implementationBuilder.Add("{");
        implementationBuilder.Add($"{indent}add => _eventCollection.{model.UniqueName} += value;");
        implementationBuilder.Add($"{indent}remove => _eventCollection.{model.UniqueName} -= value;");
        implementationBuilder.Add("}");

        return implementationBuilder.ToImmutable();
    }
}
