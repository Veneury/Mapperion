using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// Everything the scenarios share: a Mapperion mapper and an AutoMapper one configured the
    /// same way, so a comparison measures the engines rather than two different configurations.
    /// </summary>
    [MemoryDiagnoser]
    public abstract class ScenarioBase
    {
        protected IMapper Mapperion { get; private set; } = null!;

        protected AutoMapper.IMapper AutoMapped { get; private set; } = null!;

        [GlobalSetup]
        public void Setup()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Flat, FlatDto>();
                cfg.CreateMap<Line, LineDto>();
                cfg.CreateMap<Line, LineRecordDto>();
                cfg.CreateMap<Client, ClientDto>();
                cfg.CreateMap<Order, OrderDto>();
                cfg.CreateMap<Order, FlattenedDto>();
            });

            configuration.AssertIsValid();
            Mapperion = configuration.CreateMapper();

            var automapper = new AutoMapper.MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Flat, FlatDto>();
                cfg.CreateMap<Line, LineDto>();
                cfg.CreateMap<Line, LineRecordDto>();
                cfg.CreateMap<Client, ClientDto>();
                cfg.CreateMap<Order, OrderDto>();
                cfg.CreateMap<Order, FlattenedDto>();
            });

            AutoMapped = automapper.CreateMapper();

            Prepare();
        }

        protected virtual void Prepare()
        {
        }
    }

    /// <summary>B01: ten primitive members.</summary>
    public class FlatBenchmarks : ScenarioBase
    {
        private Flat source = null!;

        protected override void Prepare() => source = Samples.Flat();

        [Benchmark(Baseline = true)]
        public FlatDto Manual() => ByHand.Map(source);

        [Benchmark]
        public FlatDto Mapperion_() => Mapperion.Map<Flat, FlatDto>(source);

        [Benchmark]
        public FlatDto AutoMapper_() => AutoMapped.Map<Flat, FlatDto>(source);
    }

    /// <summary>B02: three levels with a collection at the bottom.</summary>
    public class NestedBenchmarks : ScenarioBase
    {
        private Order source = null!;

        protected override void Prepare() => source = Samples.Order(5);

        [Benchmark(Baseline = true)]
        public OrderDto Manual() => ByHand.Map(source);

        [Benchmark]
        public OrderDto Mapperion_() => Mapperion.Map<Order, OrderDto>(source);

        [Benchmark]
        public OrderDto AutoMapper_() => AutoMapped.Map<Order, OrderDto>(source);
    }

    /// <summary>B03: a thousand elements.</summary>
    public class CollectionBenchmarks : ScenarioBase
    {
        private List<Line> source = null!;

        protected override void Prepare() => source = Samples.Order(1000).Lines;

        [Benchmark(Baseline = true)]
        public List<LineDto> Manual()
        {
            var result = new List<LineDto>(source.Count);

            foreach (Line line in source)
            {
                result.Add(ByHand.Map(line));
            }

            return result;
        }

        [Benchmark]
        public List<LineDto> Mapperion_() => Mapperion.Map<List<Line>, List<LineDto>>(source);

        [Benchmark]
        public List<LineDto> AutoMapper_() => AutoMapped.Map<List<Line>, List<LineDto>>(source);
    }

    /// <summary>B04: four hops of flattening.</summary>
    public class FlatteningBenchmarks : ScenarioBase
    {
        private Order source = null!;

        protected override void Prepare() => source = Samples.Order(0);

        [Benchmark(Baseline = true)]
        public FlattenedDto Manual() => ByHand.MapFlattened(source);

        [Benchmark]
        public FlattenedDto Mapperion_() => Mapperion.Map<Order, FlattenedDto>(source);

        [Benchmark]
        public FlattenedDto AutoMapper_() => AutoMapped.Map<Order, FlattenedDto>(source);
    }

    /// <summary>B05: a record built through its constructor.</summary>
    public class ConstructorBenchmarks : ScenarioBase
    {
        private Line source = null!;

        protected override void Prepare() => source = new Line { Code = "L", Price = 3m };

        [Benchmark(Baseline = true)]
        public LineRecordDto Manual() => ByHand.MapToRecord(source);

        [Benchmark]
        public LineRecordDto Mapperion_() => Mapperion.Map<Line, LineRecordDto>(source);

        [Benchmark]
        public LineRecordDto AutoMapper_() => AutoMapped.Map<Line, LineRecordDto>(source);
    }

    /// <summary>B07: mapping onto an instance the caller already has.</summary>
    public class ExistingDestinationBenchmarks : ScenarioBase
    {
        private Flat source = null!;
        private FlatDto destination = null!;

        protected override void Prepare()
        {
            source = Samples.Flat();
            destination = new FlatDto();
        }

        [Benchmark(Baseline = true)]
        public FlatDto Manual()
        {
            ByHand.Into(source, destination);
            return destination;
        }

        [Benchmark]
        public FlatDto Mapperion_() => Mapperion.Map(source, destination);

        [Benchmark]
        public FlatDto AutoMapper_() => AutoMapped.Map(source, destination);
    }
}
