using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Pass
    {
        public int Code { get; set; }

        public string Label { get; set; } = string.Empty;

        public Seat Seat { get; set; } = new Seat();
    }

    public sealed class Seat
    {
        public string Row { get; set; } = string.Empty;
    }

    public sealed class SeatDto
    {
        public string Row { get; set; } = string.Empty;
    }

    public sealed class PassDto
    {
        public PassDto(string label)
        {
            Label = label;
        }

        public string Label { get; }

        public int Code { get; set; }

        public SeatDto? Seat { get; set; }
    }

    public sealed class ConstructUsingTests
    {
        [Fact]
        public void The_factory_builds_the_destination_and_the_members_are_still_assigned()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Seat, SeatDto>();
                cfg.CreateMap<Pass, PassDto>()
                   .ConstructUsing(source => new PassDto("from " + source.Label));
            });

            PassDto dto = config.CreateMapper().Map<Pass, PassDto>(
                new Pass { Code = 7, Label = "x", Seat = new Seat { Row = "B" } });

            dto.Label.ShouldBe("from x");
            dto.Code.ShouldBe(7);
            dto.Seat!.Row.ShouldBe("B");
        }

        [Fact]
        public void The_factory_can_reach_the_running_mapper()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Seat, SeatDto>();
                cfg.CreateMap<Pass, PassDto>()
                   .ConstructUsing((source, context) => new PassDto(
                       context.Mapper.Map<Seat, SeatDto>(source.Seat).Row));
            });

            PassDto dto = config.CreateMapper().Map<Pass, PassDto>(
                new Pass { Seat = new Seat { Row = "C" } });

            dto.Label.ShouldBe("C");
        }

        [Fact]
        public void A_destination_the_caller_supplied_is_used_instead_of_the_factory()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Seat, SeatDto>();
                cfg.CreateMap<Pass, PassDto>()
                   .ConstructUsing(source => new PassDto("built"));
            });

            var existing = new PassDto("supplied");

            PassDto dto = config.CreateMapper().Map(new Pass { Code = 3 }, existing);

            dto.ShouldBeSameAs(existing);
            dto.Label.ShouldBe("supplied");
            dto.Code.ShouldBe(3);
        }

        [Fact]
        public void The_factory_replaces_picking_a_constructor()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Seat, SeatDto>();
                cfg.CreateMap<Pass, PassDto>().ConstructUsing(source => new PassDto("fixed"));
            });

            Should.NotThrow(config.AssertIsValid);

            config.CreateMapper().Map<Pass, PassDto>(new Pass()).Label.ShouldBe("fixed");
        }

        [Fact]
        public void A_factory_and_a_configured_constructor_parameter_cannot_both_stand()
        {
#pragma warning disable MPR1004
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Seat, SeatDto>();
                    cfg.CreateMap<Pass, PassDto>()
                       .ConstructUsing(source => new PassDto("fixed"))
                       .ForCtorParam("label", o => o.MapFrom(s => s.Label));
                }));
#pragma warning restore MPR1004

            error.Message.ShouldContain("ForCtorParam");
        }

        [Fact]
        public void A_projection_reports_the_factory_instead_of_ignoring_it()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Seat, SeatDto>();
                cfg.CreateMap<Pass, PassDto>().ConstructUsing(source => new PassDto("fixed"));
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new[] { new Pass() }.AsQueryable().ProjectTo<PassDto>(config));

            error.Message.ShouldContain("factory");
        }
    }
}
