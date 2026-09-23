using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Badge2
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class Badge2Dto
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class Visitor
    {
        public string? Name { get; set; }

        public int? Age { get; set; }

        public Badge2? Badge { get; set; }

        public List<string>? Tags { get; set; }
    }

    public sealed class VisitorDto
    {
        public string Name { get; set; } = string.Empty;

        public int? Age { get; set; }

        public Badge2Dto? Badge { get; set; }

        public IReadOnlyList<string>? Tags { get; set; }
    }

    public sealed class NullDestinationTests
    {
        private static IMapper Mapper(bool allowNulls, bool allowNullCollections = false)
        {
            return new MapperConfiguration(cfg =>
            {
                cfg.AllowNullDestinationValues = allowNulls;
                cfg.AllowNullCollections = allowNullCollections;
                cfg.CreateMap<Visitor, VisitorDto>();
                cfg.CreateMap<Badge2, Badge2Dto>();
            }).CreateMapper();
        }

        [Fact]
        public void Nulls_reach_the_destination_by_default()
        {
            VisitorDto dto = Mapper(allowNulls: true).Map<Visitor, VisitorDto>(new Visitor());

            dto.Name.ShouldBeNull();
            dto.Badge.ShouldBeNull();
        }

        [Fact]
        public void A_null_string_becomes_empty_when_nulls_are_refused()
        {
            Mapper(allowNulls: false).Map<Visitor, VisitorDto>(new Visitor()).Name.ShouldBe(string.Empty);
        }

        [Fact]
        public void A_null_complex_member_becomes_an_empty_instance()
        {
            Badge2Dto? badge = Mapper(allowNulls: false).Map<Visitor, VisitorDto>(new Visitor()).Badge;

            badge.ShouldNotBeNull();
            badge!.Code.ShouldBe(string.Empty);
        }

        [Fact]
        public void A_value_present_on_the_source_is_untouched()
        {
            var visitor = new Visitor { Name = "ada", Badge = new Badge2 { Code = "x" } };

            VisitorDto dto = Mapper(allowNulls: false).Map<Visitor, VisitorDto>(visitor);

            dto.Name.ShouldBe("ada");
            dto.Badge!.Code.ShouldBe("x");
        }

        [Fact]
        public void A_nullable_value_type_is_left_alone()
        {
            Mapper(allowNulls: false).Map<Visitor, VisitorDto>(new Visitor()).Age.ShouldBeNull();
        }

        [Fact]
        public void Collections_keep_answering_to_their_own_setting()
        {
            Mapper(allowNulls: false, allowNullCollections: true)
                .Map<Visitor, VisitorDto>(new Visitor())
                .Tags.ShouldBeNull();

            Mapper(allowNulls: false, allowNullCollections: false)
                .Map<Visitor, VisitorDto>(new Visitor())
                .Tags.ShouldBeEmpty();
        }

        [Fact]
        public void A_projection_applies_it_to_strings()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullDestinationValues = false;
                cfg.CreateMap<Visitor, VisitorDto>()
                   .ForMember(d => d.Badge, o => o.Ignore());
                cfg.CreateMap<Badge2, Badge2Dto>();
            });

            List<VisitorDto> visitors = new[] { new Visitor() }
                .AsQueryable()
                .ProjectTo<VisitorDto>(config)
                .ToList();

            visitors[0].Name.ShouldBe(string.Empty);
        }

        [Fact]
        public void The_option_reaches_the_model()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullDestinationValues = false;
                cfg.CreateMap<Badge2, Badge2Dto>();
            });

            config.Options.AllowNullDestinationValues.ShouldBeFalse();
        }

        [Fact]
        public void A_destination_without_a_parameterless_constructor_stays_null()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullDestinationValues = false;
                cfg.CreateMap<Visitor, HolderOfRecord>()
                   .ForMember(d => d.Badge, o => o.MapFrom(s => s.Badge));
                cfg.CreateMap<Badge2, BadgeRecord2>();
            });

            HolderOfRecord dto = config.CreateMapper().Map<Visitor, HolderOfRecord>(new Visitor());

            dto.Badge.ShouldBeNull();
        }
    }

    public sealed record BadgeRecord2(string Code);

    public sealed class HolderOfRecord
    {
        public BadgeRecord2? Badge { get; set; }
    }
}
