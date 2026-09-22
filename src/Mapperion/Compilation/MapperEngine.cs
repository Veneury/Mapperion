using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Model;

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

        private readonly Func<TypeMapKey, MapPlan> compile;

        internal MapperEngine(MapperModel model)
        {
            Model = model;
            compile = CompilePlan;
        }

        internal MapperModel Model { get; }

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
