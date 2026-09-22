using System;
using System.Linq;
using Mapperion.Model;
using Mapperion.Tests.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class MapperConfigurationTests
    {
        private static TypeMapDefinition SingleMap(Action<IMapperConfigurationExpression> configure)
        {
            return new MapperConfiguration(configure).Model.TypeMaps.Single();
        }

        [Fact]
        public void CreateMap_registers_the_type_pair()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Order, OrderDto>());

            config.Model.Count.ShouldBe(1);
            config.Model.Contains(new TypeMapKey(typeof(Order), typeof(OrderDto))).ShouldBeTrue();
        }

        [Fact]
        public void Declaring_the_same_pair_twice_is_rejected()
        {
            Should.Throw<MapperConfigurationException>(() => new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Order, OrderDto>();
                cfg.CreateMap<Order, OrderDto>();
            }));
        }

        [Fact]
        public void MapFrom_on_a_direct_member_records_a_single_step_path()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Id)));

            MemberDefinition member = map.FindMember(nameof(OrderDto.Id))!;
            member.IsExplicit.ShouldBeTrue();

            var source = member.Source.ShouldBeOfType<MemberPathSource>();
            source.Path.IsFlattened.ShouldBeFalse();
            source.Path.ToString().ShouldBe("Id");
            source.ValueType.ShouldBe(typeof(int));
        }

        [Fact]
        public void MapFrom_on_a_member_chain_records_a_flattening_path()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.CustomerAddressCity, o => o.MapFrom(s => s.Customer.Address.City)));

            var source = map.FindMember(nameof(OrderDto.CustomerAddressCity))!.Source.ShouldBeOfType<MemberPathSource>();
            source.Path.Length.ShouldBe(3);
            source.Path.ToString().ShouldBe("Customer.Address.City");
        }

        [Fact]
        public void MapFrom_on_a_parameterless_method_is_part_of_the_path()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.GetDisplayName())));

            var source = map.FindMember(nameof(OrderDto.CustomerName))!.Source.ShouldBeOfType<MemberPathSource>();
            source.Path.ToString().ShouldBe("GetDisplayName");
            source.Path.Leaf.Kind.ShouldBe(MemberKind.Method);
        }

        [Fact]
        public void MapFrom_on_an_arbitrary_expression_is_kept_opaque()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Id * 2)));

            var source = map.FindMember(nameof(OrderDto.Id))!.Source.ShouldBeOfType<CustomSource>();
            source.Kind.ShouldBe(MemberSourceKind.Custom);
            source.ValueType.ShouldBe(typeof(int));
            source.Description.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void Ignore_marks_the_member_and_drops_any_source()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.CustomerName, o =>
                {
                    o.MapFrom(s => s.Customer.Name);
                    o.Ignore();
                }));

            MemberDefinition member = map.FindMember(nameof(OrderDto.CustomerName))!;
            member.IsIgnored.ShouldBeTrue();
            member.Source.ShouldBeNull();
        }

        [Fact]
        public void Configuring_the_same_member_twice_accumulates_instead_of_duplicating()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.Id, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.Id, o => o.SetMappingOrder(5)));

            map.Members.Count(m => m.DestinationMember.Name == nameof(OrderDto.Id)).ShouldBe(1);

            MemberDefinition id = map.FindMember(nameof(OrderDto.Id))!;
            id.MappingOrder.ShouldBe(5);
            id.Source.ShouldBeOfType<MemberPathSource>();
        }

        [Fact]
        public void Member_options_reach_the_model()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ForMember(d => d.CustomerName, o =>
                {
                    o.MapFrom(s => s.Customer.Name);
                    o.Condition(s => s.Id > 0);
                    o.NullSubstitute("unknown");
                    o.UseDestinationValue();
                }));

            MemberDefinition member = map.FindMember(nameof(OrderDto.CustomerName))!;
            member.Condition.ShouldNotBeNull();
            member.HasNullSubstitute.ShouldBeTrue();
            member.NullSubstitute.ShouldBe("unknown");
            member.UseDestinationValue.ShouldBeTrue();
        }

        [Fact]
        public void Map_level_options_reach_the_model()
        {
            TypeMapDefinition map = SingleMap(cfg => cfg
                .CreateMap<Order, OrderDto>()
                .ValidateMemberList(MemberListValidation.None)
                .MaxDepth(2)
                .PreserveReferences());

            map.MemberListValidation.ShouldBe(MemberListValidation.None);
            map.MaxDepth.ShouldBe(2);
            map.PreserveReferences.ShouldBeTrue();
        }

        [Fact]
        public void Non_positive_max_depth_is_rejected()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Order, OrderDto>().MaxDepth(0)));
        }

        [Fact]
        public void Global_options_reach_the_model()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.NameComparison = MemberNameComparison.Ordinal;
                cfg.MaxFlatteningDepth = 5;
                cfg.AllowNullCollections = true;
                cfg.IncludeFields = true;
                cfg.EnumMapping = EnumMappingPolicy.ByValue;
                cfg.ValidateOnBuild = true;
                cfg.RecognizeSourcePrefixes("Get", string.Empty);
                cfg.RecognizeDestinationPostfixes("Dto");
            });

            MapperOptions options = config.Options;
            options.NameComparison.ShouldBe(MemberNameComparison.Ordinal);
            options.NameStringComparison.ShouldBe(StringComparison.Ordinal);
            options.MaxFlatteningDepth.ShouldBe(5);
            options.AllowNullCollections.ShouldBeTrue();
            options.IncludeFields.ShouldBeTrue();
            options.EnumMapping.ShouldBe(EnumMappingPolicy.ByValue);
            options.ValidateOnBuild.ShouldBeTrue();
            options.SourcePrefixes.ShouldHaveSingleItem().ShouldBe("Get");
            options.DestinationPostfixes.ShouldHaveSingleItem().ShouldBe("Dto");
        }

        [Fact]
        public void An_empty_configuration_produces_an_empty_model()
        {
            var config = new MapperConfiguration(cfg => { });

            config.Model.Count.ShouldBe(0);
            config.Options.MaxFlatteningDepth.ShouldBe(MapperOptions.Defaults.MaxFlatteningDepth);
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(null!));

            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Order, OrderDto>().ForMember<int>(null!, o => o.Ignore())));

            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Order, OrderDto>().ForMember(d => d.Id, null!)));
        }
    }
}
