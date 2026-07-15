using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Macaron.InlineInterface;

internal static class SourceGenerationHelpers
{
    const string Indent = "    ";

    public static void AddSource(
        SourceProductionContext context,
        InterfaceGenerationModel model
    )
    {
        var type = model.Type;
        var genericParameters = model.GenericParameters;
        var genericParameterConstraints = model.GenericParameterConstraints;
        var mergedTypePrefix = model.MergedTypePrefix;
        var typeBuilderNamespace = model.TypeBuilderNamespace;
        var typeBuilder = model.TypeBuilder;
        var globalTypeBuilder = model.GlobalTypeBuilder;
        var eventCodeGenerator = new EventCodeGenerator(model.Events, model.InterfaceTypes, Indent);
        var hasEventMembers = model.Events.Length > 0;
        var propertyCodeGenerator = new PropertyCodeGenerator(
            model.Properties,
            model.InterfaceTypes,
            typeBuilder,
            Indent
        );
        var methodCodeGenerator = new MethodCodeGenerator(
            model.Methods,
            model.InterfaceTypes,
            typeBuilder,
            Indent
        );

        var stringBuilder = CreateStringBuilderWithFileHeader();
        var depthSpacerText = "";

        // begin builder namespace
        stringBuilder.AppendLine($"namespace {typeBuilderNamespace}");
        stringBuilder.AppendLine($"{{");

        depthSpacerText += Indent;

        // begin builder type
        stringBuilder.AppendLine($"{depthSpacerText}internal readonly struct {typeBuilder}");

        // constraints
        foreach (var constraint in genericParameterConstraints)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
        }

        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        if (hasEventMembers)
        {
            // EventCollection
            stringBuilder.AppendLine($"{depthSpacerText}public sealed class EventCollection");
            stringBuilder.AppendLine($"{depthSpacerText}{{");

            depthSpacerText += Indent;

            foreach (var line in eventCodeGenerator.GetEventCollectionFieldDeclarations())
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }

            depthSpacerText = depthSpacerText[..^Indent.Length];

            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();

            // EventDispatcher
            stringBuilder.AppendLine($"{depthSpacerText}public sealed class EventDispatcher");
            stringBuilder.AppendLine($"{depthSpacerText}{{");

            depthSpacerText += Indent;

            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventCollection _eventCollection;");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}public EventDispatcher(EventCollection eventCollection)");
            stringBuilder.AppendLine($"{depthSpacerText}{{");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}_eventCollection = eventCollection;");
            stringBuilder.AppendLine($"{depthSpacerText}}}");

            foreach (var lines in eventCodeGenerator.GetEventDispatcherImplementations())
            {
                stringBuilder.AppendLine();

                foreach (var line in lines)
                {
                    stringBuilder.AppendLine($"{depthSpacerText}{line}");
                }
            }

            depthSpacerText = depthSpacerText[..^Indent.Length];

            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();
        }

        // begin impl type
        stringBuilder.AppendLine($"{depthSpacerText}private sealed class Impl : {type}");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        if (hasEventMembers)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventCollection _eventCollection = new();");
            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventDispatcher _eventDispatcher;");
        }

        foreach (var line in propertyCodeGenerator.GetImplFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        foreach (var line in methodCodeGenerator.GetImplFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        stringBuilder.AppendLine();

        // impl constructor parameters
        stringBuilder.Append($"{depthSpacerText}public Impl(");

        var implConstructorParams = new List<string>();

        implConstructorParams.AddRange(propertyCodeGenerator.GetImplConstructorParameterFragments());
        implConstructorParams.AddRange(methodCodeGenerator.GetImplConstructorParameterFragments());

        if (implConstructorParams.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", implConstructorParams));
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        // begin impl constructor body
        depthSpacerText += Indent;

        if (hasEventMembers)
        {
            stringBuilder.AppendLine($"{depthSpacerText}_eventDispatcher = new EventDispatcher(_eventCollection);");
        }

        foreach (var line in propertyCodeGenerator.GetImplConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        foreach (var line in methodCodeGenerator.GetImplConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        // end impl constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // impl event implementations
        foreach (var eventImplementation in model.EventImplementations)
        {
            stringBuilder.AppendLine();

            foreach (var line in eventCodeGenerator.GetInterfaceImplementation(eventImplementation))
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }
        }

        // impl property implementations
        foreach (var propertyImplementation in model.PropertyImplementations)
        {
            stringBuilder.AppendLine();

            foreach (var line in propertyCodeGenerator.GetInterfaceImplementation(propertyImplementation))
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }
        }

        // impl method implementations
        foreach (var methodImplementation in model.MethodImplementations)
        {
            var line = methodCodeGenerator.GetInterfaceImplementation(methodImplementation);

            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        // end impl type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // builder field members
        stringBuilder.AppendLine($"{depthSpacerText}private const string InterfaceDisplayName = {ToStringLiteral(type)};");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new global::System.InvalidOperationException($\"Cannot build inline implementation for '{{InterfaceDisplayName}}' because no delegate was provided for {{memberDescription}}. Pass a delegate or set allowMissingImplementation: true.\");");
        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private static global::System.NotImplementedException CreateMissingInvocationDelegateException(string memberDescription)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new global::System.NotImplementedException($\"No delegate was configured for {{memberDescription}} on '{{InterfaceDisplayName}}'. This can happen when Build was called with allowMissingImplementation: true.\");");
        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private readonly bool _allowMissingImplementation;");
        stringBuilder.AppendLine();

        foreach (var line in propertyCodeGenerator.GetBuilderFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        foreach (var line in methodCodeGenerator.GetBuilderFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        // builder constructor parameters
        stringBuilder.Append($"{depthSpacerText}public {mergedTypePrefix}Builder(");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{depthSpacerText}{Indent}bool allowMissingImplementation");

        var builderConstructorParams = new List<string>();

        builderConstructorParams.AddRange(propertyCodeGenerator.GetBuilderConstructorParameterFragments());
        builderConstructorParams.AddRange(methodCodeGenerator.GetBuilderConstructorParameterFragments());

        if (builderConstructorParams.Count > 0)
        {
            stringBuilder.AppendLine(",");
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", builderConstructorParams));
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        // begin builder constructor body
        depthSpacerText += Indent;

        stringBuilder.AppendLine($"{depthSpacerText}_allowMissingImplementation = allowMissingImplementation;");
        stringBuilder.AppendLine();

        foreach (var line in propertyCodeGenerator.GetBuilderConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        foreach (var line in methodCodeGenerator.GetBuilderConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        // end builder constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // builder methods
        foreach (var line in propertyCodeGenerator.GetBuilderMethodImplementation(typeBuilder))
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        foreach (var line in methodCodeGenerator.GetBuilderMethodImplementation(typeBuilder))
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        // begin build method
        stringBuilder.AppendLine($"{depthSpacerText}public {type} Build(global::Macaron.InlineInterface.Tag _ = default)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        stringBuilder.Append($"{depthSpacerText}return new Impl(");

        var implConstructorArgs = new List<string>();

        implConstructorArgs.AddRange(propertyCodeGenerator.GetBuildArgumentFragments());
        implConstructorArgs.AddRange(methodCodeGenerator.GetBuildArgumentFragments());

        if (implConstructorArgs.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", implConstructorArgs));
        }

        stringBuilder.Append($");");
        stringBuilder.AppendLine();

        // end build method
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end builder type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end builder namespace
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // begin extension namespace
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"namespace Macaron.InlineInterface");
        stringBuilder.AppendLine($"{{");

        depthSpacerText += Indent;

        // begin extension class
        stringBuilder.AppendLine($"{depthSpacerText}internal static partial class ImplementationOfExtensions");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        // extension methods
        foreach (var lines in propertyCodeGenerator.GetExtensionMethodImplementation(
            type,
            globalTypeBuilder,
            genericParameters,
            genericParameterConstraints
        ))
        {
            foreach (var line in lines)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }

            stringBuilder.AppendLine();
        }

        foreach (var lines in methodCodeGenerator.GetExtensionMethodImplementation(
            type,
            globalTypeBuilder,
            genericParameters,
            genericParameterConstraints
        ))
        {
            foreach (var line in lines)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }

            stringBuilder.AppendLine();
        }

        // extension build method
        stringBuilder.AppendLine($"{depthSpacerText}public static {type} Build{genericParameters}(");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}global::Macaron.InlineInterface.Tag _ = default)");

        foreach (var constraint in genericParameterConstraints)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
        }

        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);");
        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end extension class
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end extension namespace
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"}}");

        context.AddSource(
            hintName: model.HintName,
            sourceText: SourceText.From(stringBuilder.ToString(), Encoding.UTF8)
        );
    }

    private static StringBuilder CreateStringBuilderWithFileHeader()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("// <auto-generated />");
        stringBuilder.AppendLine("#nullable enable");
        stringBuilder.AppendLine();

        return stringBuilder;
    }

    private static string ToStringLiteral(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
