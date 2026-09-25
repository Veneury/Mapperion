using AgileObjects.AgileMapper;
using BenchmarkDotNet.Attributes;

namespace Mapperion.Benchmarks
{
    public abstract class Vehicle
    {
        public int Id { get; set; }

        public string Plate { get; set; } = string.Empty;
    }

    public class Car : Vehicle
    {
        public int Doors { get; set; }
    }

    public sealed class SportsCar : Car
    {
        public int TopSpeed { get; set; }
    }

    /// <remarks>
    /// Abstract, like the source. Nothing here ever builds one, and the shape a polymorphic map
    /// actually has in an application is the shape worth timing.
    /// </remarks>
    public abstract class VehicleDto
    {
        public int Id { get; set; }

        public string Plate { get; set; } = string.Empty;
    }

    public class CarDto : VehicleDto
    {
        public int Doors { get; set; }
    }

    public sealed class SportsCarDto : CarDto
    {
        public int TopSpeed { get; set; }
    }

    /// <summary>
    /// B08: a three-level hierarchy reached through the base type, so every entrant has to look at
    /// what the reference actually holds before it can map anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source is declared as <see cref="Vehicle"/> and holds a <see cref="SportsCar"/>, which
    /// is the only arrangement that measures the dispatch. Handed the concrete type, every one of
    /// these would go straight to a plan and the scenario would be B01 with fewer members.
    /// </para>
    /// <para>
    /// Mapster has no row. With the derived pairs declared and <c>Include</c> registered both ways,
    /// <c>Adapt&lt;VehicleDto&gt;</c> on a reference typed as the base throws "Cannot instantiate
    /// type: VehicleDto", so it is reading the static type. There may well be a spelling that works
    /// and was not found here; what is not going to happen is a row timing a call that was handed
    /// the concrete type while the others were handed the base.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    public class PolymorphicBenchmarks
    {
        private IMapper mapperion = null!;
        private AutoMapper.IMapper automapper = null!;
        private Vehicle source = null!;

        [GlobalSetup]
        public void Setup()
        {
            source = new SportsCar { Id = 7, Plate = "AB-123", Doors = 2, TopSpeed = 300 };

            mapperion = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Vehicle, VehicleDto>()
                   .Include<Car, CarDto>()
                   .Include<SportsCar, SportsCarDto>();
                cfg.CreateMap<Car, CarDto>()
                   .IncludeBase<Vehicle, VehicleDto>()
                   .Include<SportsCar, SportsCarDto>();
                cfg.CreateMap<SportsCar, SportsCarDto>()
                   .IncludeBase<Car, CarDto>();
            }).CreateMapper();

            automapper = new AutoMapper.MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Vehicle, VehicleDto>()
                   .Include<Car, CarDto>()
                   .Include<SportsCar, SportsCarDto>();
                cfg.CreateMap<Car, CarDto>()
                   .IncludeBase<Vehicle, VehicleDto>()
                   .Include<SportsCar, SportsCarDto>();
                cfg.CreateMap<SportsCar, SportsCarDto>()
                   .IncludeBase<Car, CarDto>();
            }).CreateMapper();

            Mapper.Map(source).ToANew<VehicleDto>();
        }

        [Benchmark(Baseline = true)]
        public VehicleDto Manual()
        {
            switch (source)
            {
                case SportsCar sports:
                    return new SportsCarDto
                    {
                        Id = sports.Id,
                        Plate = sports.Plate,
                        Doors = sports.Doors,
                        TopSpeed = sports.TopSpeed,
                    };

                case Car car:
                    return new CarDto { Id = car.Id, Plate = car.Plate, Doors = car.Doors };

                default:
                    throw new System.InvalidOperationException("No destination for " + source.GetType().Name + ".");
            }
        }

        [Benchmark]
        public VehicleDto Mapperion_Runtime() => mapperion.Map<Vehicle, VehicleDto>(source);

        [Benchmark]
        public VehicleDto Mapperion_RuntimeFast() => mapperion.MapFast<Vehicle, VehicleDto>(source);

        [Benchmark]
        public VehicleDto AutoMapper_() => automapper.Map<Vehicle, VehicleDto>(source);

        [Benchmark]
        public VehicleDto AgileMapper_() => Mapper.Map(source).ToANew<VehicleDto>();
    }
}
