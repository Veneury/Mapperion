using System;
using System.Globalization;
using System.Linq;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Validation
{
    public sealed class ValidationTests
    {
        private static MapperConfiguration Complete(Action<IMapperConfigurationExpression>? extra = null)
        {
            return new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Sale, SaleDto>()
                   .ForMember(d => d.Reference, o => o.MapFrom(s => s.Id.ToString(CultureInfo.InvariantCulture)));
                cfg.CreateMap<Buyer, BuyerDto>();
                cfg.CreateMap<Line, LineDto>();
                extra?.Invoke(cfg);
            });
        }

        [Fact]
        public void A_complete_configuration_passes()
        {
            Should.NotThrow(() => Complete().AssertIsValid());
        }

        [Fact]
        public void An_unmapped_destination_member_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Sale, SaleDto>();
                cfg.CreateMap<Buyer, BuyerDto>();
                cfg.CreateMap<Line, LineDto>();
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("'Reference'") && e.Contains("has no source"));
        }

        [Fact]
        public void A_missing_nested_map_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Sale, SaleDto>()
                   .ForMember(d => d.Reference, o => o.MapFrom(s => s.Id.ToString(CultureInfo.InvariantCulture))));

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("CreateMap<Buyer, BuyerDto>()"));
        }

        [Fact]
        public void A_missing_map_for_a_collection_element_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Sale, SaleDto>()
                   .ForMember(d => d.Reference, o => o.MapFrom(s => s.Id.ToString(CultureInfo.InvariantCulture)));
                cfg.CreateMap<Buyer, BuyerDto>();
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("'Lines'") && e.Contains("CreateMap<Line, LineDto>()"));
        }

        [Fact]
        public void Every_problem_is_reported_at_once()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Sale, SaleDto>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.Count.ShouldBe(3);
            error.Message.ShouldContain("3 problems were found:");
            error.Message.ShouldContain("  1. ");
            error.Message.ShouldContain("  3. ");
        }

        [Fact]
        public void A_widening_conversion_is_left_to_the_conversion_rules()
        {
            Complete().AssertIsValid();

            MemberDefinition id = Complete().Model.TypeMaps[0].FindMember(nameof(SaleDto.Id))!;
            id.Source!.ValueType.ShouldBe(typeof(int));
            id.DestinationMember.MemberType.ShouldBe(typeof(long));
        }

        [Fact]
        public void An_ignored_member_is_not_reported()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Sale, SaleDto>()
                   .ForMember(d => d.Reference, o => o.Ignore());
                cfg.CreateMap<Buyer, BuyerDto>();
                cfg.CreateMap<Line, LineDto>();
            });

            Should.NotThrow(config.AssertIsValid);
        }

        [Fact]
        public void Validation_can_be_turned_off_per_map()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Sale, SaleDto>().ValidateMemberList(MemberListValidation.None);
                cfg.CreateMap<Buyer, BuyerDto>();
                cfg.CreateMap<Line, LineDto>();
            });

            Should.NotThrow(config.AssertIsValid);
        }

        [Fact]
        public void Source_validation_reports_members_nobody_reads()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MemberListValidation = MemberListValidation.Source;
                cfg.CreateMap<Sale, SaleSummaryDto>();
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("'Quantity'") && e.Contains("is not used"));
            error.Errors.ShouldNotContain(e => e.Contains("'Id'"));
        }

        [Fact]
        public void Nothing_is_validated_at_build_time_by_default()
        {
            MapperOptions.Defaults.ValidateOnBuild.ShouldBeFalse();

            Should.NotThrow(() => new MapperConfiguration(cfg => cfg.CreateMap<Sale, SaleDto>()));
        }

        [Fact]
        public void ValidateOnBuild_moves_the_failure_to_the_constructor()
        {
            Should.Throw<MapperConfigurationException>(() => new MapperConfiguration(cfg =>
            {
                cfg.ValidateOnBuild = true;
                cfg.CreateMap<Sale, SaleDto>();
            }));
        }

        [Fact]
        public void The_AutoMapper_name_does_the_same_thing()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Sale, SaleDto>());

            MapperConfigurationException viaAlias =
                Should.Throw<MapperConfigurationException>(config.AssertConfigurationIsValid);

            viaAlias.Errors.Count.ShouldBe(3);
        }

        [Fact]
        public void Errors_are_reported_in_a_stable_order()
        {
            string Errors()
            {
                var config = new MapperConfiguration(cfg => cfg.CreateMap<Sale, SaleDto>());
                return string.Join(
                    "|",
                    Should.Throw<MapperConfigurationException>(config.AssertIsValid).Errors.ToArray());
            }

            Errors().ShouldBe(Errors());
        }
    }
}
