using System;
using System.Threading;
using System.Threading.Tasks;
using AgileObjects.AgileMapper;
using BenchmarkDotNet.Attributes;
using Mapster;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// B11: the same flat map run from sixteen workers at once, to find out whether a mapper holds
    /// anything the callers have to queue behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What this looks for is not speed but shape. A mapper that keeps per-operation state on the
    /// instance, or takes a lock to reach a plan, reads no worse than the others one call at a time
    /// and falls apart here. Comparing against the same work done by hand in the same loop is what
    /// separates the mapper's contention from the harness's.
    /// </para>
    /// <para>
    /// Sixteen is the number of work items, not a promise of sixteen cores: on a smaller machine
    /// the runtime runs them with fewer threads and the contention simply shows up less. The ratio
    /// against the hand-written run is still read against that same machine.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    public class ConcurrentBenchmarks
    {
        private const int Workers = 16;
        private const int PerWorker = 256;

        private readonly ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Workers };

        private IMapper mapperion = null!;
        private AutoMapper.IMapper automapper = null!;
        private TypeAdapterConfig mapster = null!;
        private Flat source = null!;

        [GlobalSetup]
        public void Setup()
        {
            source = Samples.Flat();

            mapperion = new MapperConfiguration(cfg => cfg.CreateMap<Flat, FlatDto>()).CreateMapper();

            automapper = new AutoMapper.MapperConfiguration(cfg => cfg.CreateMap<Flat, FlatDto>()).CreateMapper();

            mapster = new TypeAdapterConfig();
            mapster.Compile();

            source.Adapt<FlatDto>(mapster);
            Mapper.Map(source).ToANew<FlatDto>();
        }

        [Benchmark(Baseline = true)]
        public int Manual() => Run(ByHand.Map);

        [Benchmark]
        public int Mapperion_Runtime() => Run(s => mapperion.Map<Flat, FlatDto>(s));

        [Benchmark]
        public int Mapperion_RuntimeFast() => Run(s => mapperion.MapFast<Flat, FlatDto>(s));

        [Benchmark]
        public int AutoMapper_() => Run(s => automapper.Map<Flat, FlatDto>(s));

        [Benchmark]
        public int Mapster_() => Run(s => s.Adapt<FlatDto>(mapster));

        [Benchmark]
        public int AgileMapper_() => Run(s => Mapper.Map(s).ToANew<FlatDto>());

        /// <summary>
        /// Every entrant goes through the same delegate and the same loop, so what is left over
        /// between them is the mapper.
        /// </summary>
        private int Run(Func<Flat, FlatDto> map)
        {
            int total = 0;

            Parallel.For(0, Workers, options, _ =>
            {
                int local = 0;

                for (int i = 0; i < PerWorker; i++)
                {
                    local += map(source).Id;
                }

                Interlocked.Add(ref total, local);
            });

            return total;
        }
    }
}
