using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Money
    {
        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }

    public sealed class MoneyDto
    {
        public string Formatted { get; set; } = string.Empty;
    }

    public sealed class Ticket
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public Money Cost { get; set; } = new Money();
    }

    public sealed class TicketDto
    {
        public string Reference { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Cost { get; set; } = string.Empty;
    }

    public sealed class MoneyToTextConverter : ITypeConverter<Money, MoneyDto>
    {
        public MoneyDto Convert(Money source, MoneyDto destination, ResolutionContext context)
        {
            destination ??= new MoneyDto();
            destination.Formatted = source.Amount.ToString(CultureInfo.InvariantCulture) + " " + source.Currency;
            return destination;
        }
    }

    public sealed class UpperCaseConverter : IValueConverter<string, string>
    {
        public string Convert(string sourceMember, ResolutionContext context)
        {
            return sourceMember?.ToUpperInvariant() ?? string.Empty;
        }
    }

    public sealed class IdToReferenceConverter : IValueConverter<int, string>
    {
        public string Convert(int sourceMember, ResolutionContext context)
        {
            return "TCK-" + sourceMember.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class CostResolver : IValueResolver<Ticket, TicketDto, string>
    {
        public string Resolve(Ticket source, TicketDto destination, string destinationMember, ResolutionContext context)
        {
            MoneyDto money = context.Mapper.Map<Money, MoneyDto>(source.Cost);
            return money.Formatted;
        }
    }

    public sealed class CountingResolver : IValueResolver<Ticket, TicketDto, string>
    {
        internal static int Instances;

        public CountingResolver()
        {
            Instances++;
        }

        public string Resolve(Ticket source, TicketDto destination, string destinationMember, ResolutionContext context)
        {
            return source.Title;
        }
    }

    public sealed class NotAConverter
    {
    }

    public sealed class ExtensibilityTests
    {
        private static Ticket SampleTicket() => new Ticket
        {
            Id = 7,
            Title = "broken lift",
            Cost = new Money { Amount = 12.5m, Currency = "EUR" },
        };

        [Fact]
        public void A_type_converter_replaces_the_whole_map()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>());
            config.AssertIsValid();

            MoneyDto dto = config.CreateMapper().Map<Money, MoneyDto>(SampleTicket().Cost);

            dto.Formatted.ShouldBe("12.5 EUR");
        }

        [Fact]
        public void A_type_converter_receives_an_existing_destination()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>());

            var destination = new MoneyDto();
            MoneyDto returned = config.CreateMapper().Map(SampleTicket().Cost, destination);

            returned.ShouldBeSameAs(destination);
        }

        [Fact]
        public void A_nested_map_can_use_a_type_converter()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Reference, o => o.MapFrom(s => s.Id))
                   .ForMember(d => d.Cost, o => o.MapFrom<CostResolver>());
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>();
            });
            config.AssertIsValid();

            config.CreateMapper().Map<Ticket, TicketDto>(SampleTicket()).Cost.ShouldBe("12.5 EUR");
        }

        [Fact]
        public void A_value_converter_runs_on_the_resolved_value()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Title, o => o.ConvertUsing<UpperCaseConverter, string>())
                   .ForMember(d => d.Reference, o => o.Ignore())
                   .ForMember(d => d.Cost, o => o.Ignore()));
            config.AssertIsValid();

            config.CreateMapper().Map<Ticket, TicketDto>(SampleTicket()).Title.ShouldBe("BROKEN LIFT");
        }

        [Fact]
        public void A_value_converter_can_change_the_type()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Reference, o =>
                   {
                       o.MapFrom(s => s.Id);
                       o.ConvertUsing<IdToReferenceConverter, int>();
                   })
                   .ForMember(d => d.Cost, o => o.Ignore()));
            config.AssertIsValid();

            config.CreateMapper().Map<Ticket, TicketDto>(SampleTicket()).Reference.ShouldBe("TCK-7");
        }

        [Fact]
        public void A_resolver_sees_the_source_and_can_map_through_the_context()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Cost, o => o.MapFrom<CostResolver>())
                   .ForMember(d => d.Reference, o => o.Ignore());
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>();
            });
            config.AssertIsValid();

            TicketDto dto = config.CreateMapper().Map<Ticket, TicketDto>(SampleTicket());

            dto.Cost.ShouldBe("12.5 EUR");
            dto.Title.ShouldBe("broken lift");
        }

        [Fact]
        public void A_resolver_is_created_once_and_reused()
        {
            CountingResolver.Instances = 0;

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Cost, o => o.MapFrom<CountingResolver>())
                   .ForMember(d => d.Reference, o => o.Ignore()));

            IMapper mapper = config.CreateMapper();

            for (int i = 0; i < 5; i++)
            {
                mapper.Map<Ticket, TicketDto>(SampleTicket());
            }

            CountingResolver.Instances.ShouldBe(1);
        }

        [Fact]
        public void A_map_with_a_type_converter_skips_member_validation()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>());

            Should.NotThrow(config.AssertIsValid);
        }

        [Fact]
        public void A_converter_that_does_not_implement_the_contract_is_reported()
        {
            var definition = new Mapperion.Model.TypeMapDefinition(
                new Mapperion.Model.TypeMapKey(typeof(Money), typeof(MoneyDto)))
            {
                TypeConverterType = typeof(NotAConverter),
            };

            var model = new Mapperion.Model.MapperModel(
                Mapperion.Model.MapperOptions.Defaults,
                new[] { definition });

            var mapper = new Mapperion.Execution.Mapper(model);

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => mapper.Map<Money, MoneyDto>(new Money()));

            error.Message.ShouldContain("does not implement ITypeConverter");
        }

        [Fact]
        public void A_resolver_feeding_a_constructor_parameter_gets_a_default_destination()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Ticket, TicketDto>()
                   .ForMember(d => d.Cost, o => o.MapFrom<CostResolver>())
                   .ForMember(d => d.Reference, o => o.Ignore());
                cfg.CreateMap<Money, MoneyDto>().ConvertUsing<MoneyToTextConverter>();
            });

            Should.NotThrow(() => config.CreateMapper().Map<Ticket, TicketDto>(SampleTicket()));
        }
    }
}
