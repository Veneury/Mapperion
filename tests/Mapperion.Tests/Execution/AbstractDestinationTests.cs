using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public class Instrument
    {
        public string Label { get; set; } = string.Empty;
    }

    public sealed class Gauge : Instrument
    {
        public int Reading { get; set; }
    }

    public sealed class Counter : Instrument
    {
        public long Ticks { get; set; }
    }

    public abstract class InstrumentDto
    {
        public string Label { get; set; } = string.Empty;
    }

    public sealed class GaugeDto : InstrumentDto
    {
        public int Reading { get; set; }
    }

    public sealed class CounterDto : InstrumentDto
    {
        public long Ticks { get; set; }
    }

    public sealed class Thermometer : Instrument
    {
        public double Celsius { get; set; }
    }

    public sealed class Panel
    {
        public List<Instrument> Instruments { get; set; } = new List<Instrument>();
    }

    public sealed class PanelDto
    {
        public List<InstrumentDto> Instruments { get; set; } = new List<InstrumentDto>();
    }

    /// <summary>
    /// A base destination that cannot be built on its own is the usual shape of a polymorphic map:
    /// nothing is ever meant to be a bare <c>InstrumentDto</c>. These check that the shape works,
    /// and that the one arrangement with no answer still says so.
    /// </summary>
    public sealed class AbstractDestinationTests
    {
        private static MapperConfiguration Panels() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Instrument, InstrumentDto>()
               .Include<Gauge, GaugeDto>()
               .Include<Counter, CounterDto>();
            cfg.CreateMap<Gauge, GaugeDto>().IncludeBase<Instrument, InstrumentDto>();
            cfg.CreateMap<Counter, CounterDto>().IncludeBase<Instrument, InstrumentDto>();
            cfg.CreateMap<Panel, PanelDto>();
        });

        [Fact]
        public void A_derived_instance_maps_through_the_abstract_base()
        {
            Instrument source = new Gauge { Label = "pressure", Reading = 42 };

            InstrumentDto mapped = Panels().CreateMapper().Map<Instrument, InstrumentDto>(source);

            GaugeDto gauge = mapped.ShouldBeOfType<GaugeDto>();
            gauge.Label.ShouldBe("pressure");
            gauge.Reading.ShouldBe(42);
        }

        [Fact]
        public void The_configuration_validates()
        {
            Should.NotThrow(() => Panels().AssertIsValid());
        }

        /// <remarks>
        /// The case this shape exists for: a collection held as the base type, where each element
        /// is a different derived one.
        /// </remarks>
        [Fact]
        public void A_collection_of_the_base_type_maps_each_element_to_its_own_destination()
        {
            var panel = new Panel();
            panel.Instruments.Add(new Gauge { Label = "pressure", Reading = 42 });
            panel.Instruments.Add(new Counter { Label = "cycles", Ticks = 900L });

            PanelDto mapped = Panels().CreateMapper().Map<Panel, PanelDto>(panel);

            mapped.Instruments[0].ShouldBeOfType<GaugeDto>().Reading.ShouldBe(42);
            mapped.Instruments[1].ShouldBeOfType<CounterDto>().Ticks.ShouldBe(900L);
        }

        /// <remarks>
        /// Nothing can be built for an instrument that is only an <c>Instrument</c>, and the
        /// complaint has to name it, because the configuration looks right and the data is what is
        /// wrong.
        /// </remarks>
        [Fact]
        public void A_source_no_derived_map_covers_says_which_one_it_was()
        {
            var source = new Instrument { Label = "unknown" };

            MappingException error = Should.Throw<MappingException>(
                () => Panels().CreateMapper().Map<Instrument, InstrumentDto>(source));

            error.ToString().ShouldContain("Instrument");
            error.ToString().ShouldContain("InstrumentDto");
        }

        /// <remarks>
        /// The caller who brings their own destination still gets it written into, since there is
        /// then nothing to construct and nothing to complain about.
        /// </remarks>
        [Fact]
        public void An_instance_the_caller_supplies_is_written_into()
        {
            var source = new Instrument { Label = "supplied" };
            InstrumentDto destination = new GaugeDto();

            Panels().CreateMapper().Map(source, destination);

            destination.Label.ShouldBe("supplied");
        }

        /// <remarks>
        /// The likelier mistake of the two: a derived type that nobody declared. Here the advice
        /// can be exact, so it is.
        /// </remarks>
        [Fact]
        public void A_derived_type_nobody_declared_is_told_what_to_declare()
        {
            Instrument source = new Thermometer { Label = "outside", Celsius = 19.5 };

            MappingException error = Should.Throw<MappingException>(
                () => Panels().CreateMapper().Map<Instrument, InstrumentDto>(source));

            error.ToString().ShouldContain("CreateMap<Thermometer");
            error.ToString().ShouldContain("Include<Thermometer");
        }

        /// <remarks>
        /// Without derived maps there is no shape to protect, so an unbuildable destination is
        /// still a configuration mistake and still fails before anything runs.
        /// </remarks>
        [Fact]
        public void An_unbuildable_destination_with_no_derived_maps_is_still_refused()
        {
            Should.Throw<MapperConfigurationException>(() =>
            {
                var configuration = new MapperConfiguration(cfg =>
                    cfg.CreateMap<Instrument, InstrumentDto>());

                configuration.CreateMapper().Map<Instrument, InstrumentDto>(new Instrument());
            });
        }
    }
}
