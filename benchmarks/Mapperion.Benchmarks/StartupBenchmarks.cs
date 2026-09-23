using BenchmarkDotNet.Attributes;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// B10: what a configuration costs before the first map runs. Building the configuration and
    /// compiling the first plan are separate numbers because they are paid at different moments:
    /// the first at startup, the second the first time a pair is actually mapped.
    /// </summary>
    [MemoryDiagnoser]
    public class StartupBenchmarks
    {
        private static MapperConfiguration Build() => new MapperConfiguration(cfg =>
        {
            cfg.MaxFlatteningDepth = 4;
            cfg.CreateMap<Flat, FlatDto>();
            cfg.CreateMap<Line, LineDto>();
            cfg.CreateMap<Line, LineRecordDto>();
            cfg.CreateMap<Client, ClientDto>();
            cfg.CreateMap<Order, OrderDto>();
            cfg.CreateMap<Order, FlattenedDto>();
        });

        [Benchmark]
        public int BuildConfiguration() => Build().Model.Count;

        [Benchmark]
        public int BuildAndValidate()
        {
            MapperConfiguration configuration = Build();
            configuration.AssertIsValid();
            return configuration.Model.Count;
        }

        [Benchmark]
        public object CompileFirstPlan()
        {
            IMapper mapper = Build().CreateMapper();
            return mapper.Map<Flat, FlatDto>(Samples.Flat());
        }
    }
}
