using BenchmarkDotNet.Attributes;
using Mapperion.Compilation;
using Mapperion.Execution;
using Mapperion.Model;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// Splits the cost of a map into the part that does the work and the part that gets to it.
    /// </summary>
    /// <remarks>
    /// The scenario benchmarks say the run-time engine is a fixed distance above the hand-written
    /// baseline whatever the member count, which points at the dispatch rather than the mapping.
    /// This measures the two halves separately instead of reasoning about it.
    /// </remarks>
    [MemoryDiagnoser]
    public class DispatchBenchmarks
    {
        private IMapper mapper = null!;
        private Mapper concrete = null!;
        private MapperEngine engine = null!;
        private MapDelegate<Flat, FlatDto> plan = null!;
        private MappingContext context;
        private Flat source = null!;

        [GlobalSetup]
        public void Setup()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Flat, FlatDto>());

            mapper = configuration.CreateMapper();
            concrete = (Mapper)mapper;
            source = Samples.Flat();

            engine = Engine(mapper);
            plan = (MapDelegate<Flat, FlatDto>)engine
                .GetPlan(new TypeMapKey(typeof(Flat), typeof(FlatDto)))
                .Typed;

            context = Context(mapper);

            mapper.Map<Flat, FlatDto>(source);
        }

        /// <summary>Everything: finding the plan, building the context, running the map.</summary>
        [Benchmark(Baseline = true)]
        public FlatDto ThroughTheMapper() => mapper.Map<Flat, FlatDto>(source);

        /// <summary>The map alone, with the plan and the context already in hand.</summary>
        [Benchmark]
        public FlatDto PlanOnly() => plan(source, default!, context);

        /// <summary>The same call, but without going through the interface.</summary>
        [Benchmark]
        public FlatDto ThroughTheConcreteMapper() => concrete.Map<Flat, FlatDto>(source);

        /// <summary>What MapFast gives back, still holding the mapper as an IMapper.</summary>
        [Benchmark]
        public FlatDto ThroughMapFast() => mapper.MapFast<Flat, FlatDto>(source);

        /// <summary>Looking the plan up the old way, by key.</summary>
        [Benchmark]
        public object FindingThePlanByKey() =>
            engine.GetPlan(new TypeMapKey(typeof(Flat), typeof(FlatDto)));

        /// <summary>Reaching the plan through the pair's slot, which is what replaced it.</summary>
        [Benchmark]
        public object FindingThePlanBySlot() => engine.GetTyped<Flat, FlatDto>();

        private static MapperEngine Engine(IMapper mapper)
        {
            return (MapperEngine)typeof(Mapper)
                .GetField("engine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(mapper)!;
        }

        private static MappingContext Context(IMapper mapper)
        {
            return (MappingContext)typeof(Mapper)
                .GetMethod("Context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(mapper, new object?[] { null })!;
        }
    }
}
