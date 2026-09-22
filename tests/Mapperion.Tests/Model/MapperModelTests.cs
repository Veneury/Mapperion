using System;
using System.Collections.Generic;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Model
{
    public sealed class MapperModelTests
    {
        private static TypeMapDefinition OrderToDto()
        {
            var key = new TypeMapKey(typeof(Order), typeof(OrderDto));

            var id = new MemberDefinition(Members.Property<OrderDto>(nameof(OrderDto.Id)))
            {
                Source = new MemberPathSource(MemberPath.Of(Members.Property<Order>(nameof(Order.Id)))),
                IsExplicit = false,
            };

            var customerName = new MemberDefinition(Members.Property<OrderDto>(nameof(OrderDto.CustomerName)))
            {
                Source = new MemberPathSource(
                    MemberPath.Of(Members.Property<Order>(nameof(Order.Customer)))
                        .Append(Members.Property<Customer>(nameof(Customer.Name)))),
                IsExplicit = true,
            };

            var city = new MemberDefinition(Members.Property<OrderDto>(nameof(OrderDto.CustomerAddressCity)))
            {
                IsIgnored = true,
            };

            return new TypeMapDefinition(key)
            {
                Members = new[] { id, customerName, city },
            };
        }

        [Fact]
        public void Model_exposes_maps_in_declaration_order()
        {
            var model = new MapperModel(MapperOptions.Defaults, new[] { OrderToDto() });

            model.Count.ShouldBe(1);
            model.TypeMaps[0].SourceType.ShouldBe(typeof(Order));
            model.TypeMaps[0].DestinationType.ShouldBe(typeof(OrderDto));
        }

        [Fact]
        public void Maps_can_be_looked_up_by_type_pair()
        {
            var model = new MapperModel(MapperOptions.Defaults, new[] { OrderToDto() });

            model.TryGetTypeMap(typeof(Order), typeof(OrderDto), out TypeMapDefinition? found).ShouldBeTrue();
            found.ShouldNotBeNull();
            model.Contains(new TypeMapKey(typeof(Order), typeof(OrderDto))).ShouldBeTrue();
        }

        [Fact]
        public void Unknown_type_pair_is_not_found()
        {
            var model = new MapperModel(MapperOptions.Defaults, new[] { OrderToDto() });

            model.TryGetTypeMap(typeof(OrderDto), typeof(Order), out TypeMapDefinition? found).ShouldBeFalse();
            found.ShouldBeNull();
        }

        [Fact]
        public void Duplicate_maps_for_the_same_pair_are_rejected()
        {
            var maps = new[] { OrderToDto(), OrderToDto() };

            Should.Throw<ArgumentException>(() => new MapperModel(MapperOptions.Defaults, maps));
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new MapperModel(null!, new List<TypeMapDefinition>()));
            Should.Throw<ArgumentNullException>(() => new MapperModel(MapperOptions.Defaults, null!));
        }

        [Fact]
        public void Members_can_be_found_by_name()
        {
            TypeMapDefinition map = OrderToDto();

            map.FindMember(nameof(OrderDto.CustomerName)).ShouldNotBeNull();
            map.FindMember("customername").ShouldBeNull();
            map.FindMember("Missing").ShouldBeNull();
        }

        [Fact]
        public void Ignored_member_reports_itself_as_ignored()
        {
            MemberDefinition? ignored = OrderToDto().FindMember(nameof(OrderDto.CustomerAddressCity));

            ignored.ShouldNotBeNull();
            ignored!.IsIgnored.ShouldBeTrue();
            ignored.Source.ShouldBeNull();
            ignored.ToString().ShouldBe("CustomerAddressCity <- (ignored)");
        }

        [Fact]
        public void Flattened_member_describes_its_source_path()
        {
            MemberDefinition? member = OrderToDto().FindMember(nameof(OrderDto.CustomerName));

            member.ShouldNotBeNull();
            member!.Source.ShouldBeOfType<MemberPathSource>();
            member.Source!.Kind.ShouldBe(MemberSourceKind.MemberPath);
            member.Source.ValueType.ShouldBe(typeof(string));
            member.ToString().ShouldBe("CustomerName <- Customer.Name");
        }

        [Fact]
        public void Defaults_are_the_documented_ones()
        {
            MapperOptions defaults = MapperOptions.Defaults;

            defaults.NameComparison.ShouldBe(MemberNameComparison.OrdinalIgnoreCase);
            defaults.NameStringComparison.ShouldBe(StringComparison.OrdinalIgnoreCase);
            defaults.MaxFlatteningDepth.ShouldBe(3);
            defaults.AllowNullCollections.ShouldBeFalse();
            defaults.EnumMapping.ShouldBe(EnumMappingPolicy.ByNameThenValue);
            defaults.MemberListValidation.ShouldBe(MemberListValidation.Destination);
            defaults.ValidateOnBuild.ShouldBeTrue();
            defaults.IncludeFields.ShouldBeFalse();
        }

        [Fact]
        public void A_map_without_a_converter_reports_no_converter()
        {
            OrderToDto().HasTypeConverter.ShouldBeFalse();

            var withConverter = new TypeMapDefinition(new TypeMapKey(typeof(Order), typeof(OrderDto)))
            {
                TypeConverterType = typeof(string),
            };

            withConverter.HasTypeConverter.ShouldBeTrue();
        }
    }
}
