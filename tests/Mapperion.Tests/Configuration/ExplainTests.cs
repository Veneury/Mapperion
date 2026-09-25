using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class Shipment
    {
        public int Id { get; set; }

        public string GetReference() => "R" + Id;

        public Consignee Consignee { get; set; } = new Consignee();

        public string Internal { get; set; } = string.Empty;
    }

    public sealed class Consignee
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ShipmentDto
    {
        public int Id { get; set; }

        public string ConsigneeName { get; set; } = string.Empty;

        public string Internal { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Orphan { get; set; } = string.Empty;
    }

    /// <summary>
    /// <c>Explain</c> answers "where did this member get that, and why is that one empty". These
    /// check the distinctions it exists to make, and deliberately not the exact wording, which is
    /// meant to change whenever a clearer one turns up.
    /// </summary>
    public sealed class ExplainTests
    {
        private static MapperConfiguration Shipments() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Shipment, ShipmentDto>()
               .ForMember(d => d.Label, o => o.MapFrom(s => s.GetReference()))
               .ForMember(d => d.Internal, o => o.Ignore()));

        [Fact]
        public void A_member_resolved_by_convention_says_so()
        {
            string text = Shipments().Explain<Shipment, ShipmentDto>();

            text.ShouldContain("Id");
            text.ShouldContain("by convention");
        }

        [Fact]
        public void A_member_configured_by_hand_is_told_apart_from_one_that_was_not()
        {
            string text = Shipments().Explain<Shipment, ShipmentDto>();

            foreach (string line in text.Split('\n'))
            {
                if (line.Contains("Label"))
                {
                    line.ShouldContain("configured");
                }
            }
        }

        [Fact]
        public void A_flattened_path_shows_where_it_reaches()
        {
            Shipments().Explain<Shipment, ShipmentDto>().ShouldContain("Consignee.Name");
        }

        [Fact]
        public void An_ignored_member_is_named_as_ignored()
        {
            string text = Shipments().Explain<Shipment, ShipmentDto>();

            foreach (string line in text.Split('\n'))
            {
                if (line.Contains("Internal"))
                {
                    line.ShouldContain("ignored");
                }
            }
        }

        /// <remarks>
        /// The member with nothing behind it is the reason someone reaches for this in the first
        /// place, so it has to be obvious rather than merely absent.
        /// </remarks>
        [Fact]
        public void A_member_with_no_source_is_the_one_that_stands_out()
        {
            string text = Shipments().Explain<Shipment, ShipmentDto>();

            foreach (string line in text.Split('\n'))
            {
                if (line.Contains("Orphan"))
                {
                    line.ShouldContain("nothing");
                    return;
                }
            }

            throw new Xunit.Sdk.XunitException("Orphan was not mentioned at all:\n" + text);
        }

        [Fact]
        public void A_pair_with_no_map_says_that_rather_than_nothing()
        {
            string text = Shipments().Explain<ShipmentDto, Shipment>();

            text.ShouldContain("no map is declared");
            text.ShouldContain("CreateMap");
        }

        [Fact]
        public void Explaining_everything_covers_every_declared_map()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Consignee, ShipmentDto>().ValidateMemberList(Mapperion.Model.MemberListValidation.None);
                cfg.CreateMap<Shipment, ShipmentDto>()
                   .ForMember(d => d.Label, o => o.Ignore())
                   .ForMember(d => d.Orphan, o => o.Ignore());
            });

            string text = configuration.Explain();

            text.ShouldContain("Consignee -> ShipmentDto");
            text.ShouldContain("Shipment -> ShipmentDto");
        }

        [Fact]
        public void A_converter_replacing_the_whole_map_is_said_plainly()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Shipment, ShipmentDto>().ConvertUsing<ShipmentConverter>());

            configuration.Explain<Shipment, ShipmentDto>().ShouldContain("ShipmentConverter");
        }
    }

    public sealed class ShipmentConverter : ITypeConverter<Shipment, ShipmentDto>
    {
        public ShipmentDto Convert(Shipment source, ShipmentDto destination, ResolutionContext context) =>
            new ShipmentDto();
    }
}
