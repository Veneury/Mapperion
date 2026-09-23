using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Mapperion.SourceGeneration
{
    /// <summary>
    /// Writes the bodies of the partial methods on a class marked <c>[Mapper]</c>.
    /// </summary>
    /// <remarks>
    /// The output is plain C# with no reflection and no run-time code emission, which is what makes
    /// it work under trimming and ahead-of-time compilation. It follows the same member conventions
    /// as the run-time engine — exact name, then ignoring case — so a configuration reads the same
    /// either way, but it shares no code with it: the run-time compiler works on
    /// <c>System.Type</c>, and there is no such thing here.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class MapperGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static registration =>
                registration.AddSource("MapperionAttributes.g.cs", SourceText.From(MapperAttributes.Source, Encoding.UTF8)));

            IncrementalValuesProvider<INamedTypeSymbol> mappers = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    MapperAttributes.MapperAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (target, _) => (INamedTypeSymbol)target.TargetSymbol);

            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(mappers.Collect()),
                static (production, input) => Emit(production, input.Left, input.Right));
        }

        private static void Emit(
            SourceProductionContext context,
            Compilation compilation,
            ImmutableArray<INamedTypeSymbol> mappers)
        {
            foreach (INamedTypeSymbol mapper in mappers)
            {
                if (!IsPartial(mapper))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.MapperMustBePartial,
                        mapper.Locations.FirstOrDefault(),
                        mapper.Name));

                    continue;
                }

                string? source = Write(context, compilation, mapper);

                if (source is not null)
                {
                    context.AddSource(mapper.ToDisplayString().Replace('<', '_').Replace('>', '_') + ".g.cs",
                        SourceText.From(source, Encoding.UTF8));
                }
            }
        }

        private static bool IsPartial(INamedTypeSymbol mapper)
        {
            foreach (SyntaxReference reference in mapper.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is ClassDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(m => m.ValueText == "partial"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? Write(
            SourceProductionContext context,
            Compilation compilation,
            INamedTypeSymbol mapper)
        {
            List<IMethodSymbol> definitions = mapper.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.IsPartialDefinition && m.PartialImplementationPart is null)
                .ToList();

            if (definitions.Count == 0)
            {
                return null;
            }

            var plans = new List<MethodPlan>();

            foreach (IMethodSymbol method in definitions)
            {
                if (method.Parameters.Length != 1 || method.ReturnsVoid)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.UnsupportedSignature,
                        method.Locations.FirstOrDefault(),
                        method.Name));

                    continue;
                }

                plans.Add(new MethodPlan(method));
            }

            if (plans.Count == 0)
            {
                return null;
            }

            var emitter = new MapperEmitter(context, compilation, plans);
            return emitter.Write(mapper);
        }
    }

    /// <summary>One partial method and the pair of types it maps.</summary>
    internal sealed class MethodPlan
    {
        internal MethodPlan(IMethodSymbol method)
        {
            Method = method;
            SourceType = method.Parameters[0].Type;
            DestinationType = method.ReturnType;
            ExplicitSources = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            Ignored = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (AttributeData attribute in method.GetAttributes())
            {
                string? name = attribute.AttributeClass?.ToDisplayString();

                if (name == MapperAttributes.MapPropertyAttributeName && attribute.ConstructorArguments.Length == 2)
                {
                    string? from = attribute.ConstructorArguments[0].Value as string;
                    string? to = attribute.ConstructorArguments[1].Value as string;

                    if (from is not null && to is not null)
                    {
                        ExplicitSources[to] = from;
                    }
                }
                else if (name == MapperAttributes.MapperIgnoreAttributeName && attribute.ConstructorArguments.Length == 1)
                {
                    if (attribute.ConstructorArguments[0].Value is string ignored)
                    {
                        Ignored.Add(ignored);
                    }
                }
            }
        }

        internal IMethodSymbol Method { get; }

        internal ITypeSymbol SourceType { get; }

        internal ITypeSymbol DestinationType { get; }

        internal Dictionary<string, string> ExplicitSources { get; }

        internal HashSet<string> Ignored { get; }
    }
}
