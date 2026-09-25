using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class SnakeRow
    {
        public int order_id { get; set; }

        public string customer_name { get; set; } = string.Empty;

        public SnakeAddress ship_to { get; set; } = new SnakeAddress();
    }

    public sealed class SnakeAddress
    {
        public string city_name { get; set; } = string.Empty;
    }

    public sealed class OrderRow
    {
        public int OrderId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string ShipToCityName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A source that spells its members one way and a destination that spells them another is the
    /// whole reason these exist, so what is checked is that the pair matches with no
    /// <c>ForMember</c> per property.
    /// </summary>
    public sealed class NamingConventionTests
    {
        [Fact]
        public void An_underscore_source_matches_a_pascal_destination()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                cfg.CreateMap<SnakeRow, OrderRow>();
            });

            configuration.AssertIsValid();

            OrderRow row = configuration.CreateMapper().Map<OrderRow>(new SnakeRow
            {
                order_id = 7,
                customer_name = "Ada",
            });

            row.OrderId.ShouldBe(7);
            row.CustomerName.ShouldBe("Ada");
        }

        /// <remarks>
        /// Ignoring case is not enough on its own: <c>first_name</c> and <c>FirstName</c> differ by
        /// a character, not by capitalisation, so without the convention they do not meet.
        /// </remarks>
        [Fact]
        public void Without_the_convention_the_two_spellings_do_not_meet()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<SnakeRow, OrderRow>());

            Should.Throw<MapperConfigurationException>(() => configuration.AssertIsValid())
                  .Message.ShouldContain("CustomerName");
        }

        /// <remarks>
        /// Flattening has to keep working across the spelling, since a destination
        /// <c>ShipToCityName</c> reaching <c>ship_to.city_name</c> is the case a per-property
        /// configuration would be most tedious for.
        /// </remarks>
        [Fact]
        public void Flattening_crosses_the_two_spellings()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                cfg.CreateMap<SnakeRow, OrderRow>();
            });

            OrderRow row = configuration.CreateMapper().Map<OrderRow>(new SnakeRow
            {
                ship_to = new SnakeAddress { city_name = "Lovelace" },
            });

            row.ShipToCityName.ShouldBe("Lovelace");
        }

        [Fact]
        public void The_destination_side_can_be_the_one_with_underscores()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                cfg.CreateMap<OrderRow, SnakeRow>()
                   .ForMember(d => d.ship_to, o => o.Ignore());
            });

            SnakeRow row = configuration.CreateMapper().Map<SnakeRow>(new OrderRow
            {
                OrderId = 7,
                CustomerName = "Ada",
            });

            row.order_id.ShouldBe(7);
            row.customer_name.ShouldBe("Ada");
        }

        /// <remarks>
        /// What is being checked is that the library asks the interface rather than its own three
        /// types, so a convention it does not ship works the same.
        /// </remarks>
        [Fact]
        public void A_convention_the_library_does_not_ship_is_asked_all_the_same()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = new DoubleUnderscoreNamingConvention();
                cfg.CreateMap<GeneratedRow, PersonName>();
            });

            configuration.CreateMapper()
                .Map<PersonName>(new GeneratedRow { full__name = "Ada" })
                .FullName.ShouldBe("Ada");
        }

        [Fact]
        public void The_exact_convention_stops_the_flattening()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = ExactMatchNamingConvention.Instance;
                cfg.CreateMap<Shipment, ShipmentDto>()
                   .ForMember(d => d.Label, o => o.Ignore())
                   .ForMember(d => d.Orphan, o => o.Ignore());
            });

            Should.Throw<MapperConfigurationException>(() => configuration.AssertIsValid())
                  .Message.ShouldContain("ConsigneeName");
        }

        /// <remarks>
        /// The control for the one above: the same map without the exact convention resolves
        /// <c>ConsigneeName</c> by flattening, so the failure there is the convention doing
        /// something and not the map being wrong.
        /// </remarks>
        [Fact]
        public void The_same_map_validates_without_the_exact_convention()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Shipment, ShipmentDto>()
                   .ForMember(d => d.Label, o => o.Ignore())
                   .ForMember(d => d.Orphan, o => o.Ignore()));

            configuration.AssertIsValid();
        }

        /// <remarks>
        /// The defaults are what every existing configuration runs on, so they have to leave a name
        /// exactly as it stood before any of this existed.
        /// </remarks>
        [Fact]
        public void The_default_spelling_leaves_names_untouched()
        {
            MapperOptions.Defaults.SourceMemberNamingConvention.SeparatorCharacter.ShouldBe(string.Empty);
            MapperOptions.Defaults.DestinationMemberNamingConvention.SeparatorCharacter.ShouldBe(string.Empty);
        }
    }

    public sealed class GeneratedRow
    {
        public string full__name { get; set; } = string.Empty;
    }

    public sealed class PersonName
    {
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class DoubleUnderscoreNamingConvention : INamingConvention
    {
        public string? SeparatorCharacter => "__";
    }
}
