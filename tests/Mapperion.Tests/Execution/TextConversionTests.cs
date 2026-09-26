using System;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class TextRow
    {
        public string Id { get; set; } = string.Empty;

        public string? Optional { get; set; }
    }

    public sealed class IdentifiedDto
    {
        public Guid Id { get; set; }

        public Guid? Optional { get; set; }
    }

    public sealed class Identified
    {
        public Guid Id { get; set; }
    }

    public sealed class TextDto
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// An identifier or a date arriving as text is what an application meets on its first day,
    /// out of JSON or out of a column somebody typed as text.
    /// </summary>
    public sealed class TextConversionTests
    {
        private const string Sample = "8a1b0c9d-0000-4000-8000-000000000001";

        private static IMapper Mapper() => new MapperConfiguration(cfg =>
            cfg.CreateMap<TextRow, IdentifiedDto>()).CreateMapper();

        [Fact]
        public void Text_becomes_a_guid()
        {
            IdentifiedDto dto = Mapper().Map<TextRow, IdentifiedDto>(new TextRow { Id = Sample });

            dto.Id.ShouldBe(Guid.Parse(Sample));
        }

        [Fact]
        public void A_guid_goes_back_to_text()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Identified, TextDto>());

            configuration.CreateMapper()
                .Map<Identified, TextDto>(new Identified { Id = Guid.Parse(Sample) })
                .Id.ShouldBe(Sample);
        }

        /// <remarks>
        /// Absent is not the same as wrong. Empty text is how a missing value arrives out of a
        /// form or a CSV, and it gives the default rather than an exception, which is what an
        /// empty string already did for an enum.
        /// </remarks>
        [Fact]
        public void Empty_text_gives_the_default()
        {
            Mapper().Map<TextRow, IdentifiedDto>(new TextRow { Id = string.Empty })
                .Id.ShouldBe(Guid.Empty);
        }

        [Fact]
        public void Text_that_is_not_one_says_so_and_says_which()
        {
            MappingException error = Should.Throw<MappingException>(
                () => Mapper().Map<TextRow, IdentifiedDto>(new TextRow { Id = "not an identifier" }));

            error.ToString().ShouldContain("not an identifier");
            error.ToString().ShouldContain("Guid");
        }

        [Fact]
        public void A_nullable_destination_takes_it_too()
        {
            Mapper().Map<TextRow, IdentifiedDto>(new TextRow { Optional = Sample })
                .Optional.ShouldBe(Guid.Parse(Sample));
        }

        [Fact]
        public void A_null_source_leaves_the_nullable_null()
        {
            Mapper().Map<TextRow, IdentifiedDto>(new TextRow { Optional = null })
                .Optional.ShouldBeNull();
        }

        /// <remarks>
        /// The built-in reading is looked at after a declared map, so somebody who wants their own
        /// still gets it.
        /// </remarks>
        [Fact]
        public void A_declared_map_for_the_pair_wins()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TextRow, IdentifiedDto>()
                   .ForMember(d => d.Optional, o => o.Ignore())
                   .ForMember(d => d.Id, o => o.MapFrom(s => Guid.Empty));
            });

            configuration.CreateMapper()
                .Map<TextRow, IdentifiedDto>(new TextRow { Id = Sample })
                .Id.ShouldBe(Guid.Empty);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public void Text_becomes_a_date()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<DatedRow, DatedDto>());

            DatedDto dto = configuration.CreateMapper()
                .Map<DatedRow, DatedDto>(new DatedRow { Day = "2026-03-14", At = "16:05" });

            dto.Day.ShouldBe(new DateOnly(2026, 3, 14));
            dto.At.ShouldBe(new TimeOnly(16, 5));
        }

        /// <remarks>
        /// The invariant culture, whatever the machine thinks. A date read one way here and
        /// another way on a colleague's laptop would be worse than not reading it at all.
        /// </remarks>
        [Fact]
        public void A_day_that_is_not_one_says_so()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<DatedRow, DatedDto>().ForMember(d => d.At, o => o.Ignore()));

            Should.Throw<MappingException>(
                () => configuration.CreateMapper().Map<DatedRow, DatedDto>(new DatedRow { Day = "14/03/2026" }))
                .ToString().ShouldContain("DateOnly");
        }

        [Fact]
        public void Empty_text_gives_the_default_day()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<DatedRow, DatedDto>());

            DatedDto dto = configuration.CreateMapper()
                .Map<DatedRow, DatedDto>(new DatedRow { Day = string.Empty, At = string.Empty });

            dto.Day.ShouldBe(default(DateOnly));
            dto.At.ShouldBe(default(TimeOnly));
        }
#endif
    }

#if NET6_0_OR_GREATER
    public sealed class DatedRow
    {
        public string Day { get; set; } = string.Empty;

        public string At { get; set; } = string.Empty;
    }

    public sealed class DatedDto
    {
        public DateOnly Day { get; set; }

        public TimeOnly At { get; set; }
    }
#endif
}
