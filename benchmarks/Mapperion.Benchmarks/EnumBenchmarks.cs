using AgileObjects.AgileMapper;
using BenchmarkDotNet.Attributes;
using Mapperion.Model;
using Mapster;

namespace Mapperion.Benchmarks
{
    public enum Priority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    public enum Channel
    {
        Web = 0,
        Phone = 1,
        Store = 2,
    }

    public enum Currency
    {
        Eur = 0,
        Usd = 1,
        Gbp = 2,
    }

    public enum Region
    {
        North = 0,
        South = 1,
        East = 2,
    }

    public enum Tier
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
    }

    /// <summary>The same five, with the numbers in the other order.</summary>
    /// <remarks>
    /// Reversing them is what makes the two policies distinguishable: mapping by value and mapping
    /// by name give different answers here, so a benchmark cannot quietly measure one while
    /// claiming the other.
    /// </remarks>
    public enum PriorityDto
    {
        High = 0,
        Normal = 1,
        Low = 2,
    }

    public enum ChannelDto
    {
        Store = 0,
        Phone = 1,
        Web = 2,
    }

    public enum CurrencyDto
    {
        Gbp = 0,
        Usd = 1,
        Eur = 2,
    }

    public enum RegionDto
    {
        East = 0,
        South = 1,
        North = 2,
    }

    public enum TierDto
    {
        Gold = 0,
        Silver = 1,
        Bronze = 2,
    }

    public sealed class Ticket
    {
        public Priority Priority { get; set; }

        public Channel Channel { get; set; }

        public Currency Currency { get; set; }

        public Region Region { get; set; }

        public Tier Tier { get; set; }
    }

    /// <summary>The destination when the numbers line up: every member is a cast.</summary>
    public sealed class TicketByValueDto
    {
        public Priority Priority { get; set; }

        public Channel Channel { get; set; }

        public Currency Currency { get; set; }

        public Region Region { get; set; }

        public Tier Tier { get; set; }
    }

    /// <summary>The destination when only the names line up.</summary>
    public sealed class TicketByNameDto
    {
        public PriorityDto Priority { get; set; }

        public ChannelDto Channel { get; set; }

        public CurrencyDto Currency { get; set; }

        public RegionDto Region { get; set; }

        public TierDto Tier { get; set; }
    }

    /// <summary>B06, half of it: five enums whose numbers match, so every entrant casts.</summary>
    [MemoryDiagnoser]
    public class EnumByValueBenchmarks
    {
        private IMapper mapperion = null!;
        private AutoMapper.IMapper automapper = null!;
        private TypeAdapterConfig mapster = null!;
        private Ticket source = null!;

        [GlobalSetup]
        public void Setup()
        {
            source = new Ticket
            {
                Priority = Priority.High,
                Channel = Channel.Phone,
                Currency = Currency.Usd,
                Region = Region.South,
                Tier = Tier.Gold,
            };

            mapperion = new MapperConfiguration(cfg =>
            {
                cfg.EnumMapping = EnumMappingPolicy.ByValue;
                cfg.CreateMap<Ticket, TicketByValueDto>();
            }).CreateMapper();

            automapper = new AutoMapper.MapperConfiguration(cfg =>
                cfg.CreateMap<Ticket, TicketByValueDto>()).CreateMapper();

            mapster = new TypeAdapterConfig();
            mapster.Compile();

            source.Adapt<TicketByValueDto>(mapster);
            Mapper.Map(source).ToANew<TicketByValueDto>();
        }

        [Benchmark(Baseline = true)]
        public TicketByValueDto Manual() => new TicketByValueDto
        {
            Priority = source.Priority,
            Channel = source.Channel,
            Currency = source.Currency,
            Region = source.Region,
            Tier = source.Tier,
        };

        [Benchmark]
        public TicketByValueDto Mapperion_Runtime() => mapperion.Map<Ticket, TicketByValueDto>(source);

        [Benchmark]
        public TicketByValueDto Mapperion_RuntimeFast() => mapperion.MapFast<Ticket, TicketByValueDto>(source);

        [Benchmark]
        public TicketByValueDto AutoMapper_() => automapper.Map<Ticket, TicketByValueDto>(source);

        [Benchmark]
        public TicketByValueDto Mapster_() => source.Adapt<TicketByValueDto>(mapster);

        [Benchmark]
        public TicketByValueDto AgileMapper_() => Mapper.Map(source).ToANew<TicketByValueDto>();
    }

    /// <summary>B06, the other half: five enums that only agree on their names.</summary>
    /// <remarks>
    /// Only two entrants, and that is the finding rather than an omission. Mapping by name is what
    /// <see cref="EnumMappingPolicy.ByName"/> does out of the box; the other libraries either cast
    /// regardless of the name, or need a package or a line per member to be talked into it. Timing
    /// them side by side would put a cast next to a name lookup and call it a comparison, so what
    /// is measured here is what the policy costs us against writing the switch by hand.
    /// </remarks>
    [MemoryDiagnoser]
    public class EnumByNameBenchmarks
    {
        private IMapper mapperion = null!;
        private Ticket source = null!;

        [GlobalSetup]
        public void Setup()
        {
            source = new Ticket
            {
                Priority = Priority.High,
                Channel = Channel.Phone,
                Currency = Currency.Usd,
                Region = Region.South,
                Tier = Tier.Gold,
            };

            mapperion = new MapperConfiguration(cfg =>
            {
                cfg.EnumMapping = EnumMappingPolicy.ByName;
                cfg.CreateMap<Ticket, TicketByNameDto>();
            }).CreateMapper();
        }

        [Benchmark(Baseline = true)]
        public TicketByNameDto Manual() => new TicketByNameDto
        {
            Priority = source.Priority switch
            {
                Priority.Low => PriorityDto.Low,
                Priority.Normal => PriorityDto.Normal,
                _ => PriorityDto.High,
            },
            Channel = source.Channel switch
            {
                Channel.Web => ChannelDto.Web,
                Channel.Phone => ChannelDto.Phone,
                _ => ChannelDto.Store,
            },
            Currency = source.Currency switch
            {
                Currency.Eur => CurrencyDto.Eur,
                Currency.Usd => CurrencyDto.Usd,
                _ => CurrencyDto.Gbp,
            },
            Region = source.Region switch
            {
                Region.North => RegionDto.North,
                Region.South => RegionDto.South,
                _ => RegionDto.East,
            },
            Tier = source.Tier switch
            {
                Tier.Bronze => TierDto.Bronze,
                Tier.Silver => TierDto.Silver,
                _ => TierDto.Gold,
            },
        };

        [Benchmark]
        public TicketByNameDto Mapperion_Runtime() => mapperion.Map<Ticket, TicketByNameDto>(source);

        [Benchmark]
        public TicketByNameDto Mapperion_RuntimeFast() => mapperion.MapFast<Ticket, TicketByNameDto>(source);
    }
}
