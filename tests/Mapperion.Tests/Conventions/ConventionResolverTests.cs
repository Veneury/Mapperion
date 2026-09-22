using System;
using System.Linq;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Conventions
{
    public sealed class ConventionResolverTests
    {
        private static TypeMapDefinition Map(Action<IMapperConfigurationExpression> configure)
        {
            return new MapperConfiguration(configure).Model.TypeMaps.Single();
        }

        private static string? PathOf(TypeMapDefinition map, string destinationMember)
        {
            MemberDefinition? member = map.FindMember(destinationMember);
            return (member?.Source as MemberPathSource)?.Path.ToString();
        }

        [Fact]
        public void Members_with_the_same_name_are_matched()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, PersonDto>());

            PathOf(map, nameof(PersonDto.Id)).ShouldBe("Id");
            PathOf(map, nameof(PersonDto.Name)).ShouldBe("Name");
        }

        [Fact]
        public void Matching_ignores_case_by_default()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, LowercaseDto>());

            PathOf(map, "name").ShouldBe("Name");
        }

        [Fact]
        public void Matching_respects_an_ordinal_comparison_when_configured()
        {
            TypeMapDefinition map = Map(cfg =>
            {
                cfg.NameComparison = MemberNameComparison.Ordinal;
                cfg.CreateMap<Person, LowercaseDto>();
            });

            map.FindMember("name")!.Source.ShouldBeNull();
        }

        [Fact]
        public void Two_level_flattening_is_resolved()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, PersonDto>());

            PathOf(map, nameof(PersonDto.ContactEmail)).ShouldBe("Contact.Email");
        }

        [Fact]
        public void Three_level_flattening_is_resolved()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, PersonDto>());

            PathOf(map, nameof(PersonDto.CompanyContactEmail)).ShouldBe("Company.Contact.Email");
        }

        [Fact]
        public void Flattening_stops_at_the_configured_depth()
        {
            TypeMapDefinition map = Map(cfg =>
            {
                cfg.MaxFlatteningDepth = 2;
                cfg.CreateMap<Person, PersonDto>();
            });

            PathOf(map, nameof(PersonDto.ContactEmail)).ShouldBe("Contact.Email");
            map.FindMember(nameof(PersonDto.CompanyContactEmail))!.Source.ShouldBeNull();
        }

        [Fact]
        public void A_get_prefixed_method_matches_once_the_prefix_is_recognised()
        {
            TypeMapDefinition withoutPrefix = Map(cfg => cfg.CreateMap<Person, BadgeDto>());
            withoutPrefix.FindMember(nameof(BadgeDto.Badge))!.Source.ShouldBeNull();

            TypeMapDefinition withPrefix = Map(cfg =>
            {
                cfg.RecognizeSourcePrefixes("Get");
                cfg.CreateMap<Person, BadgeDto>();
            });

            PathOf(withPrefix, nameof(BadgeDto.Badge)).ShouldBe("GetBadge");
        }

        [Fact]
        public void A_destination_suffix_is_stripped_before_matching()
        {
            TypeMapDefinition map = Map(cfg =>
            {
                cfg.RecognizeDestinationPostfixes("Dto");
                cfg.CreateMap<Person, PersonDtoSuffixed>();
            });

            PathOf(map, nameof(PersonDtoSuffixed.NameDto)).ShouldBe("Name");
        }

        [Fact]
        public void An_unmatched_member_is_reported_with_no_source()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, PersonDto>());

            MemberDefinition unmatched = map.FindMember(nameof(PersonDto.Unmatched))!;
            unmatched.Source.ShouldBeNull();
            unmatched.IsIgnored.ShouldBeFalse();
        }

        [Fact]
        public void Read_only_destination_members_are_skipped()
        {
            TypeMapDefinition map = Map(cfg => cfg.CreateMap<Person, PersonDto>());

            map.FindMember(nameof(PersonDto.Computed)).ShouldBeNull();
        }

        [Fact]
        public void Explicit_configuration_wins_over_the_convention()
        {
            TypeMapDefinition map = Map(cfg => cfg
                .CreateMap<Person, PersonDto>()
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Contact.Email)));

            PathOf(map, nameof(PersonDto.Name)).ShouldBe("Contact.Email");
            map.FindMember(nameof(PersonDto.Name))!.IsExplicit.ShouldBeTrue();
        }

        [Fact]
        public void An_ignored_member_is_not_resolved_by_the_convention()
        {
            TypeMapDefinition map = Map(cfg => cfg
                .CreateMap<Person, PersonDto>()
                .ForMember(d => d.Name, o => o.Ignore()));

            MemberDefinition name = map.FindMember(nameof(PersonDto.Name))!;
            name.IsIgnored.ShouldBeTrue();
            name.Source.ShouldBeNull();
        }

        [Fact]
        public void A_member_configured_without_a_source_still_gets_one_from_the_convention()
        {
            TypeMapDefinition map = Map(cfg => cfg
                .CreateMap<Person, PersonDto>()
                .ForMember(d => d.Name, o => o.SetMappingOrder(3)));

            MemberDefinition name = map.FindMember(nameof(PersonDto.Name))!;
            name.IsExplicit.ShouldBeTrue();
            name.MappingOrder.ShouldBe(3);
            PathOf(map, nameof(PersonDto.Name)).ShouldBe("Name");
        }

        [Fact]
        public void Every_writable_destination_member_appears_exactly_once()
        {
            TypeMapDefinition map = Map(cfg => cfg
                .CreateMap<Person, PersonDto>()
                .ForMember(d => d.Unmatched, o => o.Ignore()));

            map.Members.Count.ShouldBe(5);
            map.Members.Select(m => m.DestinationMember.Name).Distinct().Count().ShouldBe(5);
        }

        [Fact]
        public void Resolution_is_deterministic()
        {
            string First() => string.Join(
                "|",
                Map(cfg => cfg.CreateMap<Person, PersonDto>()).Members.Select(m => m.ToString()));

            First().ShouldBe(First());
        }
    }
}
