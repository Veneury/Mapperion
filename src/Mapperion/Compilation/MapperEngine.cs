using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Mapperion.Model;
using Mapperion.Projection;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Owns the compiled plans. A plan is built the first time its type pair is mapped and reused
    /// from then on; nested maps are resolved here at run time rather than inlined, which is what
    /// lets two maps reference each other without the compiler recursing forever.
    /// </summary>
    [RequiresUnreferencedCode("Compiling a map inspects types by reflection.")]
    [RequiresDynamicCode("Compiling a map emits code at run time.")]
    internal sealed class MapperEngine
    {
        private readonly ConcurrentDictionary<TypeMapKey, MapPlan> plans =
            new ConcurrentDictionary<TypeMapKey, MapPlan>();

        private readonly ConcurrentDictionary<TypeMapKey, LambdaExpression> projections =
            new ConcurrentDictionary<TypeMapKey, LambdaExpression>();

        private readonly Func<TypeMapKey, MapPlan> compile;
        private readonly Func<TypeMapKey, LambdaExpression> project;

        internal MapperEngine(MapperModel model)
        {
            Model = model;
            RequiresState = NeedsState(model);
            compile = CompilePlan;
            project = CompileProjection;
        }

        internal MapperModel Model { get; }

        internal bool RequiresState { get; }

        private static bool NeedsState(MapperModel model)
        {
            foreach (TypeMapDefinition definition in model.TypeMaps)
            {
                if (definition.MaxDepth is not null || definition.PreserveReferences)
                {
                    return true;
                }
            }

            return false;
        }

        internal LambdaExpression GetProjection(TypeMapKey key)
        {
            return projections.GetOrAdd(key, project);
        }

        private LambdaExpression CompileProjection(TypeMapKey key)
        {
            if (!Model.TryGetTypeMap(key, out TypeMapDefinition? definition))
            {
                throw new MappingException(
                    "No map is configured for " + key + ". Declare it with CreateMap<" +
                    key.SourceType.Name + ", " + key.DestinationType.Name + ">().");
            }

            return ProjectionCompiler.Compile(definition!, this);
        }

        internal MapPlan GetPlan(TypeMapKey key)
        {
            return plans.GetOrAdd(key, compile);
        }

        private MapPlan CompilePlan(TypeMapKey key)
        {
            if (!Model.TryGetTypeMap(key, out TypeMapDefinition? definition))
            {
                throw new MappingException(
                    "No map is configured for " + key + ". Declare it with CreateMap<" +
                    key.SourceType.Name + ", " + key.DestinationType.Name + ">().");
            }

            return PlanCompiler.Compile(definition!, this);
        }
    }
}
