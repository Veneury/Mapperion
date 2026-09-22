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

        private readonly ConcurrentDictionary<Type, object> instances = new ConcurrentDictionary<Type, object>();
        private readonly Func<TypeMapKey, MapPlan> compile;

        internal MapperEngine(MapperModel model)
        {
            Model = model;
            compile = CompilePlan;
        }

        internal MapperModel Model { get; }

        internal IMapper Mapper { get; set; } = null!;

        internal object GetInstance(Type type)
        {
            return instances.GetOrAdd(type, Create);
        }

        private static object Create(Type type)
        {
            object? instance = Activator.CreateInstance(type);

            if (instance is null)
            {
                throw new MapperConfigurationException(
                    type.Name + " could not be created. A converter or resolver needs a public " +
                    "parameterless constructor until dependency injection support lands.");
            }

            return instance;
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
