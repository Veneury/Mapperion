using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Employee
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public string Department { get; set; } = string.Empty;

        public Badge? Badge { get; set; }

        public List<Item> Items { get; set; } = new List<Item>();
    }

    public sealed class Badge
    {
        public string Serial { get; set; } = string.Empty;
    }

    public sealed record BadgeRecord(string Serial);

    public sealed record EmployeeRecord(string Name, int Age);

    public sealed record EmployeeWithDefaults(string Name, string Department = "none");

    public sealed record EmployeeWithBadge(string Name, BadgeRecord Badge);

    public sealed record EmployeeWithItems(string Name, IReadOnlyList<ItemDto> Items);

    public sealed class EmployeeWithMixedShape
    {
        public EmployeeWithMixedShape(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public int Age { get; set; }
    }

    public sealed class EmployeeWithTwoConstructors
    {
        public EmployeeWithTwoConstructors(string name)
        {
            Name = name;
            Department = "unset";
        }

        public EmployeeWithTwoConstructors(string name, string department)
        {
            Name = name;
            Department = department;
        }

        public string Name { get; }

        public string Department { get; }
    }

    public sealed class EmployeeUnbuildable
    {
        public EmployeeUnbuildable(string missingOnSource)
        {
            MissingOnSource = missingOnSource;
        }

        public string MissingOnSource { get; }
    }

    public sealed class ConstructorMappingTests
    {
        private static Employee SampleEmployee() => new Employee
        {
            Name = "Ada",
            Age = 36,
            Department = "Engineering",
            Badge = new Badge { Serial = "X-1" },
            Items = { new Item { Code = "A", Price = 1m } },
        };

        [Fact]
        public void A_positional_record_is_built_through_its_constructor()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeRecord>());
            config.AssertIsValid();

            EmployeeRecord dto = config.CreateMapper().Map<Employee, EmployeeRecord>(SampleEmployee());

            dto.Name.ShouldBe("Ada");
            dto.Age.ShouldBe(36);
        }

        [Fact]
        public void Constructor_parameters_match_source_members_ignoring_case()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeRecord>());

            Mapperion.Model.TypeMapDefinition map = config.Model.TypeMaps[0];
            map.Constructor.ShouldNotBeNull();
            map.ConstructorParameters.Count.ShouldBe(2);
            map.ConstructorParameters[0].Name.ShouldBe("Name");
            map.ConstructorParameters[0].Source.ShouldNotBeNull();
        }

        [Fact]
        public void Members_fed_by_the_constructor_are_not_assigned_twice()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeRecord>());

            config.Model.TypeMaps[0].Members.ShouldBeEmpty();
        }

        [Fact]
        public void A_parameter_default_is_used_when_the_source_has_no_match()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Badge, EmployeeWithDefaults>()
                .ForCtorParam("name", o => o.MapFrom(s => s.Serial)));
            config.AssertIsValid();

            EmployeeWithDefaults dto = config.CreateMapper()
                .Map<Badge, EmployeeWithDefaults>(new Badge { Serial = "X-1" });

            dto.Name.ShouldBe("X-1");
            dto.Department.ShouldBe("none");
        }

        [Fact]
        public void ForCtorParam_overrides_the_convention()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeRecord>()
                .ForCtorParam("name", o => o.MapFrom(s => s.Department)));
            config.AssertIsValid();

            config.CreateMapper().Map<Employee, EmployeeRecord>(SampleEmployee()).Name.ShouldBe("Engineering");
        }

        [Fact]
        public void ForCtorParam_can_supply_a_fixed_value()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeRecord>()
                .ForCtorParam("age", o => o.UseValue(99)));
            config.AssertIsValid();

            config.CreateMapper().Map<Employee, EmployeeRecord>(SampleEmployee()).Age.ShouldBe(99);
        }

        [Fact]
        public void A_nested_record_argument_uses_its_own_map()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Employee, EmployeeWithBadge>();
                cfg.CreateMap<Badge, BadgeRecord>();
            });
            config.AssertIsValid();

            EmployeeWithBadge dto = config.CreateMapper().Map<Employee, EmployeeWithBadge>(SampleEmployee());

            dto.Badge.Serial.ShouldBe("X-1");
        }

        [Fact]
        public void A_collection_argument_is_converted()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Employee, EmployeeWithItems>();
                cfg.CreateMap<Item, ItemDto>();
            });
            config.AssertIsValid();

            EmployeeWithItems dto = config.CreateMapper().Map<Employee, EmployeeWithItems>(SampleEmployee());

            dto.Items.Count.ShouldBe(1);
            dto.Items[0].Code.ShouldBe("A");
        }

        [Fact]
        public void Remaining_writable_members_are_still_assigned()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeWithMixedShape>());
            config.AssertIsValid();

            EmployeeWithMixedShape dto = config.CreateMapper()
                .Map<Employee, EmployeeWithMixedShape>(SampleEmployee());

            dto.Name.ShouldBe("Ada");
            dto.Age.ShouldBe(36);
        }

        [Fact]
        public void The_constructor_with_the_most_resolvable_parameters_wins()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeWithTwoConstructors>());
            config.AssertIsValid();

            EmployeeWithTwoConstructors dto = config.CreateMapper()
                .Map<Employee, EmployeeWithTwoConstructors>(SampleEmployee());

            dto.Name.ShouldBe("Ada");
            dto.Department.ShouldBe("Engineering");
        }

        [Fact]
        public void An_unresolvable_parameter_is_reported_by_validation()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeUnbuildable>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("'missingOnSource'") && e.Contains("ForCtorParam"));
        }

        [Fact]
        public void A_missing_map_for_a_parameter_is_reported_by_validation()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeWithBadge>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("CreateMap<Badge, BadgeRecord>()"));
        }

        [Fact]
        public void Mapping_into_an_existing_instance_keeps_it_even_with_a_constructor()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Employee, EmployeeWithMixedShape>());
            var destination = new EmployeeWithMixedShape("kept");

            EmployeeWithMixedShape returned = config.CreateMapper().Map(SampleEmployee(), destination);

            returned.ShouldBeSameAs(destination);
            returned.Name.ShouldBe("kept");
            returned.Age.ShouldBe(36);
        }
    }
}
