using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class Delivery
    {
        public string Street { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string Recipient { get; set; } = string.Empty;
    }

    public sealed class AddressDto
    {
        public string Street { get; set; } = string.Empty;

        public CityDto City { get; set; } = new CityDto();
    }

    public sealed class CityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DeliveryDto
    {
        public AddressDto? Address { get; set; }

        public string Recipient { get; set; } = string.Empty;
    }

    public sealed class FixedAddress
    {
        public AddressDto Address { get; } = new AddressDto();
    }

    public sealed class Unbuildable
    {
        public Unbuildable(int anything)
        {
            Anything = anything;
        }

        public int Anything { get; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class UnbuildableHolder
    {
        public Unbuildable? Inner { get; set; }
    }

    public struct Coordinates
    {
        public int X { get; set; }
    }

    public sealed class CoordinateHolder
    {
        public Coordinates Where { get; set; }
    }

    public sealed class ForPathTests
    {
        [Fact]
        public void A_member_inside_the_destination_is_assigned()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Address!.Street, o => o.MapFrom(s => s.Street))
                .ForPath(d => d.Address!.City.Name, o => o.MapFrom(s => s.City)));

            DeliveryDto dto = config.CreateMapper().Map<Delivery, DeliveryDto>(
                new Delivery { Street = "Mayor 1", City = "Madrid", Recipient = "ana" });

            dto.Address!.Street.ShouldBe("Mayor 1");
            dto.Address.City.Name.ShouldBe("Madrid");
            dto.Recipient.ShouldBe("ana");
        }

        [Fact]
        public void The_objects_along_the_way_are_created_when_missing()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Address!.Street, o => o.MapFrom(s => s.Street)));

            DeliveryDto dto = config.CreateMapper().Map<Delivery, DeliveryDto>(
                new Delivery { Street = "Mayor 1" });

            dto.Address.ShouldNotBeNull();
            dto.Address!.Street.ShouldBe("Mayor 1");
        }

        [Fact]
        public void An_instance_the_destination_already_has_is_kept()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Address!.Street, o => o.MapFrom(s => s.Street)));

            var existing = new DeliveryDto { Address = new AddressDto { Street = "old" } };
            AddressDto address = existing.Address!;

            config.CreateMapper().Map(new Delivery { Street = "new" }, existing);

            existing.Address.ShouldBeSameAs(address);
            address.Street.ShouldBe("new");
        }

        [Fact]
        public void A_single_step_path_is_just_a_member()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Recipient, o => o.MapFrom(s => s.City))
                .ForPath(d => d.Address, o => o.Ignore()));

            DeliveryDto dto = config.CreateMapper().Map<Delivery, DeliveryDto>(
                new Delivery { City = "Madrid" });

            dto.Recipient.ShouldBe("Madrid");
            dto.Address.ShouldBeNull();
        }

        [Fact]
        public void A_path_wins_over_the_whole_member_whatever_the_declaration_order()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Delivery, AddressDto>()
                   .ForMember(d => d.City, o => o.Ignore())
                   .ForMember(d => d.Street, o => o.MapFrom(s => "whole"));

                cfg.CreateMap<Delivery, DeliveryDto>()
                   .ForPath(d => d.Address!.Street, o => o.MapFrom(s => s.Street))
                   .ForMember(d => d.Address, o => o.MapFrom(s => s));
            });

            DeliveryDto dto = config.CreateMapper().Map<Delivery, DeliveryDto>(
                new Delivery { Street = "path" });

            dto.Address!.Street.ShouldBe("path");
        }

        [Fact]
        public void A_step_that_cannot_be_created_says_which_one_it_was()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, UnbuildableHolder>()
                .ForPath(d => d.Inner!.Name, o => o.MapFrom(s => s.Recipient)));

            MappingException error = Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Delivery, UnbuildableHolder>(new Delivery()));

            error.MemberPath.ShouldBe("Inner.Name");
            error.InnerException!.Message.ShouldContain("parameterless constructor");
        }

        [Fact]
        public void A_step_that_cannot_be_written_has_to_be_there_already()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, FixedAddress>()
                .ForPath(d => d.Address.Street, o => o.MapFrom(s => s.Street)));

            FixedAddress result = config.CreateMapper().Map<Delivery, FixedAddress>(
                new Delivery { Street = "Mayor 1" });

            result.Address.Street.ShouldBe("Mayor 1");
        }

        [Fact]
        public void A_path_through_a_value_type_is_refused()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg => cfg
                    .CreateMap<Delivery, CoordinateHolder>()
                    .ForPath(d => d.Where.X, o => o.MapFrom(s => s.Street.Length))));

            error.Message.ShouldContain("value type");
        }

        [Fact]
        public void A_path_that_is_not_a_chain_of_members_is_refused()
        {
            Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg => cfg
                    .CreateMap<Delivery, DeliveryDto>()
                    .ForPath(d => d.Recipient.Length, o => o.Ignore())));
        }

        [Fact]
        public void A_projection_reports_a_path_instead_of_dropping_it()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Address!.Street, o => o.MapFrom(s => s.Street)));

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new[] { new Delivery() }.AsQueryable().ProjectTo<DeliveryDto>(config));

            error.Message.ShouldContain("ForPath");
        }

        [Fact]
        public void A_failure_inside_a_path_is_reported_against_the_whole_path()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Delivery, DeliveryDto>()
                .ForPath(d => d.Address!.City.Name, o => o.MapFrom(s => s.City.Substring(99))));

            MappingException error = Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Delivery, DeliveryDto>(new Delivery { City = "x" }));

            error.MemberPath.ShouldBe("Address.City.Name");
        }
    }
}
