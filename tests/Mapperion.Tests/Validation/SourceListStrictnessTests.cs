using Shouldly;
using Xunit;

namespace Mapperion.Tests.Validation
{
    public sealed class Depot
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class Shipment
    {
        public Depot Depot { get; set; } = new Depot();
    }

    /// <summary>A name the convention flattens to Depot.Code by itself.</summary>
    public sealed class FlattenedShipmentDto
    {
        public string DepotCode { get; set; } = string.Empty;
    }

    /// <summary>A name the convention cannot reach, so it has to be configured by hand.</summary>
    public sealed class NamedShipmentDto
    {
        public string Anything { get; set; } = string.Empty;
    }

    public sealed class WholeShipmentDto
    {
        public Depot Depot { get; set; } = new Depot();
    }

    /// <summary>
    /// What counts as using a source member, when validating against the source list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three cases below were run through AutoMapper 14.0.0 side by side. It accepts the
    /// flattened one and the whole one and rejects the hand-written path, which reads more like an
    /// artefact of how it records what the convention matched than a rule anybody chose: reading
    /// <c>Depot.Code</c> reads <c>Depot</c> either way.
    /// </para>
    /// <para>
    /// So the lenient reading is the default and the strict one is available. The default is the
    /// lenient one because being more lenient cannot break a migration — a configuration that
    /// library accepted is accepted here — while being stricter would reject configurations that
    /// already work.
    /// </para>
    /// </remarks>
    public sealed class SourceListStrictnessTests
    {
        private static MapperConfiguration Flattened(bool readingThroughUses) =>
            new MapperConfiguration(cfg =>
            {
                cfg.ReadingThroughAMemberUsesIt = readingThroughUses;
                cfg.CreateMap<Shipment, FlattenedShipmentDto>()
                    .ValidateMemberList(Mapperion.Model.MemberListValidation.Source);
            });

        private static MapperConfiguration ByHand(bool readingThroughUses) =>
            new MapperConfiguration(cfg =>
            {
                cfg.ReadingThroughAMemberUsesIt = readingThroughUses;
                cfg.CreateMap<Shipment, NamedShipmentDto>()
                    .ValidateMemberList(Mapperion.Model.MemberListValidation.Source)
                    .ForMember(d => d.Anything, o => o.MapFrom(s => s.Depot.Code));
            });

        private static MapperConfiguration Whole(bool readingThroughUses) =>
            new MapperConfiguration(cfg =>
            {
                cfg.ReadingThroughAMemberUsesIt = readingThroughUses;
                cfg.CreateMap<Depot, Depot>();
                cfg.CreateMap<Shipment, WholeShipmentDto>()
                    .ValidateMemberList(Mapperion.Model.MemberListValidation.Source);
            });

        [Fact]
        public void By_default_a_hand_written_path_uses_the_member_it_reads_through()
        {
            Should.NotThrow(() => ByHand(readingThroughUses: true).AssertConfigurationIsValid());
        }

        /// <remarks>The one case the two libraries answer differently, and the reason for the option.</remarks>
        [Fact]
        public void Turned_off_a_hand_written_path_does_not()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => ByHand(readingThroughUses: false).AssertConfigurationIsValid());

            error.Message.ShouldContain("Depot");
        }

        [Fact]
        public void A_flattened_match_uses_the_member_either_way()
        {
            Should.NotThrow(() => Flattened(readingThroughUses: true).AssertConfigurationIsValid());
            Should.NotThrow(() => Flattened(readingThroughUses: false).AssertConfigurationIsValid());
        }

        [Fact]
        public void A_member_read_whole_is_used_either_way()
        {
            Should.NotThrow(() => Whole(readingThroughUses: true).AssertConfigurationIsValid());
            Should.NotThrow(() => Whole(readingThroughUses: false).AssertConfigurationIsValid());
        }

        /// <remarks>
        /// Validation against the destination list is the default and is untouched by any of this.
        /// </remarks>
        [Fact]
        public void The_option_does_nothing_when_the_destination_list_is_validated()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.ReadingThroughAMemberUsesIt = false;
                cfg.CreateMap<Shipment, NamedShipmentDto>()
                    .ForMember(d => d.Anything, o => o.MapFrom(s => s.Depot.Code));
            });

            Should.NotThrow(() => config.AssertConfigurationIsValid());
        }
    }
}
