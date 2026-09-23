using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Compatibility.Tests
{
    public enum Level
    {
        Low = 0,
        High = 1,
    }

    public enum LevelDto
    {
        High = 5,
        Low = 6,
    }

    public sealed class Country
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class Office
    {
        public Country? Country { get; set; }
    }

    public sealed class Line
    {
        public decimal Amount { get; set; }
    }

    public sealed class LineDto
    {
        public double Amount { get; set; }
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public int? Revision { get; set; }

        public Level Level { get; set; }

        public Office? Office { get; set; }

        public List<Line> Lines { get; set; } = new List<Line>();
    }

    public sealed class OrderDto
    {
        public long Id { get; set; }

        public int Revision { get; set; }

        public LevelDto Level { get; set; }

        public string OfficeCountryCode { get; set; } = string.Empty;

        public List<LineDto> Lines { get; set; } = new List<LineDto>();

        public string Note { get; set; } = string.Empty;
    }

    public sealed record OrderRecordDto(long Id, string Note);

    public sealed class OrderProfile : Profile
    {
        public OrderProfile()
        {
            CreateMap<Order, OrderDto>()
                .ForMember(d => d.Note, o => o.MapFrom(s => "order " + s.Id))
                .AfterMap((source, destination) => destination.Note = destination.Note.ToUpperInvariant());

            CreateMap<Line, LineDto>();
        }
    }

    /// <summary>
    /// A representative slice of the library, run on every supported runtime. The point is not to
    /// cover features again but to catch anything that only breaks on a particular framework.
    /// </summary>
    public sealed class CoreBehaviourTests
    {
        private static Order SampleOrder() => new Order
        {
            Id = 7,
            Revision = 2,
            Level = Level.High,
            Office = new Office { Country = new Country { Code = "PT" } },
            Lines = { new Line { Amount = 1.5m }, new Line { Amount = 2m } },
        };

        private static MapperConfiguration Configured() => new MapperConfiguration(cfg =>
        {
            cfg.MaxFlatteningDepth = 3;
            cfg.AddProfile<OrderProfile>();
        });

        [Fact]
        public void The_configuration_builds_and_validates()
        {
            MapperConfiguration configuration = Configured();

            configuration.AssertIsValid();
            configuration.Model.Count.ShouldBe(2);
        }

        [Fact]
        public void Members_conventions_and_flattening_work()
        {
            OrderDto dto = Configured().CreateMapper().Map<Order, OrderDto>(SampleOrder());

            dto.Id.ShouldBe(7L);
            dto.OfficeCountryCode.ShouldBe("PT");
        }

        [Fact]
        public void Nullables_enums_and_collections_work()
        {
            OrderDto dto = Configured().CreateMapper().Map<Order, OrderDto>(SampleOrder());

            dto.Revision.ShouldBe(2);
            dto.Level.ShouldBe(LevelDto.High);
            dto.Lines.Count.ShouldBe(2);
            dto.Lines[0].Amount.ShouldBe(1.5d);
        }

        [Fact]
        public void Expressions_and_after_steps_work()
        {
            Configured().CreateMapper().Map<Order, OrderDto>(SampleOrder()).Note.ShouldBe("ORDER 7");
        }

        [Fact]
        public void A_record_is_built_through_its_constructor()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Order, OrderRecordDto>()
                   .ForCtorParam("note", o => o.MapFrom(s => "n" + s.Id)));

            OrderRecordDto dto = configuration.CreateMapper().Map<Order, OrderRecordDto>(SampleOrder());

            dto.Id.ShouldBe(7L);
            dto.Note.ShouldBe("n7");
        }

        [Fact]
        public void Projection_works_over_linq_to_objects()
        {
            List<OrderDto> orders = new[] { SampleOrder() }
                .AsQueryable()
                .ProjectTo<OrderDto>(new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Order, OrderDto>().ForMember(d => d.Note, o => o.Ignore());
                    cfg.CreateMap<Line, LineDto>();
                }))
                .ToList();

            orders[0].OfficeCountryCode.ShouldBe("PT");
            orders[0].Lines.Count.ShouldBe(2);
        }

        [Fact]
        public void A_missing_map_is_reported_the_same_way()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Order, OrderDto>()
                   .ForMember(d => d.Note, o => o.Ignore()));

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                configuration.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("CreateMap<Line, LineDto>()"));
        }
    }
}
