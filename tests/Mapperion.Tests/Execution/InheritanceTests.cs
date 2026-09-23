using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public abstract class Vehicle
    {
        public string Plate { get; set; } = string.Empty;

        public int Wheels { get; set; }
    }

    public class Car : Vehicle
    {
        public int Doors { get; set; }
    }

    public sealed class SportsCar : Car
    {
        public int TopSpeed { get; set; }
    }

    public sealed class Lorry : Vehicle
    {
        public decimal Payload { get; set; }
    }

    public class VehicleDto
    {
        public string Registration { get; set; } = string.Empty;

        public int Wheels { get; set; }
    }

    public class CarDto : VehicleDto
    {
        public int Doors { get; set; }
    }

    public sealed class SportsCarDto : CarDto
    {
        public int TopSpeed { get; set; }
    }

    public sealed class LorryDto : VehicleDto
    {
        public decimal Payload { get; set; }
    }

    public sealed class Fleet
    {
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }

    public sealed class FleetDto
    {
        public List<VehicleDto> Vehicles { get; set; } = new List<VehicleDto>();
    }

    public sealed class InheritanceTests
    {
        private static MapperConfiguration Hierarchy() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Vehicle, VehicleDto>()
               .ForMember(d => d.Registration, o => o.MapFrom(s => s.Plate))
               .Include<Car, CarDto>()
               .Include<SportsCar, SportsCarDto>()
               .Include<Lorry, LorryDto>();

            cfg.CreateMap<Car, CarDto>().IncludeBase<Vehicle, VehicleDto>();
            cfg.CreateMap<SportsCar, SportsCarDto>().IncludeBase<Vehicle, VehicleDto>();
            cfg.CreateMap<Lorry, LorryDto>().IncludeBase<Vehicle, VehicleDto>();
        });

        [Fact]
        public void The_configuration_validates()
        {
            Should.NotThrow(Hierarchy().AssertIsValid);
        }

        [Fact]
        public void A_derived_source_produces_the_derived_destination()
        {
            var car = new Car { Plate = "AA-11", Wheels = 4, Doors = 5 };

            VehicleDto dto = Hierarchy().CreateMapper().Map<Vehicle, VehicleDto>(car);

            dto.ShouldBeOfType<CarDto>();
            dto.Registration.ShouldBe("AA-11");
            ((CarDto)dto).Doors.ShouldBe(5);
        }

        [Fact]
        public void The_closest_derived_map_wins_over_a_further_one()
        {
            var sports = new SportsCar { Plate = "SS-99", Wheels = 4, Doors = 2, TopSpeed = 300 };

            VehicleDto dto = Hierarchy().CreateMapper().Map<Vehicle, VehicleDto>(sports);

            dto.ShouldBeOfType<SportsCarDto>();
            ((SportsCarDto)dto).TopSpeed.ShouldBe(300);
            ((SportsCarDto)dto).Doors.ShouldBe(2);
        }

        [Fact]
        public void IncludeBase_carries_the_renamed_member_down()
        {
            var lorry = new Lorry { Plate = "LL-22", Wheels = 6, Payload = 12m };

            LorryDto dto = Hierarchy().CreateMapper().Map<Lorry, LorryDto>(lorry);

            dto.Registration.ShouldBe("LL-22");
            dto.Payload.ShouldBe(12m);
        }

        [Fact]
        public void A_derived_map_can_override_what_the_base_said()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Vehicle, VehicleDto>()
                   .ForMember(d => d.Registration, o => o.MapFrom(s => s.Plate));

                cfg.CreateMap<Car, CarDto>()
                   .IncludeBase<Vehicle, VehicleDto>()
                   .ForMember(d => d.Registration, o => o.MapFrom(s => "car " + s.Plate));
            });

            config.CreateMapper()
                .Map<Car, CarDto>(new Car { Plate = "AA-11" })
                .Registration.ShouldBe("car AA-11");
        }

        [Fact]
        public void A_collection_of_the_base_type_maps_each_element_to_its_own_kind()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Fleet, FleetDto>();

                cfg.CreateMap<Vehicle, VehicleDto>()
                   .ForMember(d => d.Registration, o => o.MapFrom(s => s.Plate))
                   .Include<Car, CarDto>()
                   .Include<Lorry, LorryDto>();

                cfg.CreateMap<Car, CarDto>().IncludeBase<Vehicle, VehicleDto>();
                cfg.CreateMap<Lorry, LorryDto>().IncludeBase<Vehicle, VehicleDto>();
            });

            var fleet = new Fleet
            {
                Vehicles =
                {
                    new Car { Plate = "AA-11", Doors = 3 },
                    new Lorry { Plate = "LL-22", Payload = 5m },
                },
            };

            List<VehicleDto> mapped = config.CreateMapper().Map<Fleet, FleetDto>(fleet).Vehicles;

            mapped[0].ShouldBeOfType<CarDto>();
            mapped[1].ShouldBeOfType<LorryDto>();
            ((LorryDto)mapped[1]).Payload.ShouldBe(5m);
        }

        [Fact]
        public void A_base_instance_still_uses_the_base_map()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Car, CarDto>().Include<SportsCar, SportsCarDto>();
                cfg.CreateMap<SportsCar, SportsCarDto>().IncludeBase<Car, CarDto>();
            });

            CarDto dto = config.CreateMapper().Map<Car, CarDto>(new Car { Doors = 4 });

            dto.ShouldBeOfType<CarDto>();
            dto.Doors.ShouldBe(4);
        }

        [Fact]
        public void Including_a_map_that_was_not_declared_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Vehicle, VehicleDto>()
                   .ForMember(d => d.Registration, o => o.MapFrom(s => s.Plate))
                   .Include<Car, CarDto>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("CreateMap<Car, CarDto>()"));
        }

        [Fact]
        public void Inheriting_from_a_map_that_was_not_declared_is_reported()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg =>
                    cfg.CreateMap<Car, CarDto>().IncludeBase<Vehicle, VehicleDto>()));

            error.Message.ShouldContain("CreateMap<Vehicle, VehicleDto>()");
        }

        [Fact]
        public void A_projection_reports_a_polymorphic_map_instead_of_flattening_it()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new Vehicle[] { new Car() }.AsQueryable().ProjectTo<VehicleDto>(Hierarchy()).ToList());

            error.Message.ShouldContain("dispatches to derived maps");
        }
    }
}
