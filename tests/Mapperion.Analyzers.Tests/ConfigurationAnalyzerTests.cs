using Shouldly;
using Xunit;

namespace Mapperion.Analyzers.Tests
{
    /// <summary>
    /// What the analyser reports, and what it leaves alone. The second half matters as much as the
    /// first: a check that fires on correct code gets switched off, and then it protects nothing.
    /// </summary>
    public sealed class ConfigurationAnalyzerTests
    {
        private const string Types = @"
using System;
using Mapperion;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public Address Home { get; set; } = new Address();
}

public class Address
{
    public string City { get; set; } = string.Empty;
}

public class PersonDto
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public Address Home { get; set; } = new Address();
}

public record PersonRecord(string Name, int Age);
";

        private static Reported Run(string body) => AnalyzerHarness.Run(
            Types + @"
public static class Setup
{
    public static MapperConfiguration Build() => new MapperConfiguration(cfg =>
    {
" + body + @"
    });
}
");

        [Fact]
        public void The_same_pair_declared_twice_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>();
        cfg.CreateMap<Person, PersonDto>();");

            reported.Count("MPR1001").ShouldBe(1, reported.All());
        }

        [Fact]
        public void Two_different_pairs_are_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>();
        cfg.CreateMap<PersonDto, Person>();
        cfg.CreateMap<Address, Address>();");

            reported.Has("MPR1001").ShouldBeFalse(reported.All());
        }

        /// <remarks>
        /// Two configurations in one method are two configurations, and a pair may appear in each.
        /// </remarks>
        [Fact]
        public void The_same_pair_in_two_separate_configurations_is_left_alone()
        {
            Reported reported = AnalyzerHarness.Run(Types + @"
public static class Setup
{
    public static void Build()
    {
        var one = new MapperConfiguration(cfg => cfg.CreateMap<Person, PersonDto>());
        var two = new MapperConfiguration(cfg => cfg.CreateMap<Person, PersonDto>());
    }
}
");

            reported.Has("MPR1001").ShouldBeFalse(reported.All());
        }

        [Fact]
        public void A_pair_declared_twice_in_a_profile_is_reported()
        {
            Reported reported = AnalyzerHarness.Run(Types + @"
public sealed class People : Profile
{
    public People()
    {
        CreateMap<Person, PersonDto>();
        CreateMap<Person, PersonDto>();
    }
}
");

            reported.Count("MPR1001").ShouldBe(1, reported.All());
        }

        [Fact]
        public void A_member_given_two_sources_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Home.City));");

            reported.Count("MPR1002").ShouldBe(1, reported.All());
        }

        /// <remarks>
        /// Settings accumulate, so splitting a member across two calls is a style, not a mistake,
        /// as long as they do not both say where the value comes from.
        /// </remarks>
        [Fact]
        public void A_member_split_across_two_calls_without_two_sources_is_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
           .ForMember(d => d.Name, o => o.SetMappingOrder(5));");

            reported.Has("MPR1002").ShouldBeFalse(reported.All());
        }

        [Fact]
        public void Different_members_are_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
           .ForMember(d => d.Age, o => o.Ignore());");

            reported.Has("MPR1002").ShouldBeFalse(reported.All());
        }

        /// <remarks>
        /// What follows a <c>ReverseMap</c> configures the other direction, so the same member
        /// named on both sides of it is two members on two maps.
        /// </remarks>
        [Fact]
        public void The_same_member_either_side_of_a_reverse_map_is_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
           .ReverseMap()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));");

            reported.Has("MPR1002").ShouldBeFalse(reported.All());
        }

        [Fact]
        public void A_path_and_the_member_it_starts_from_are_different_members()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Home, o => o.Ignore())
           .ForPath(d => d.Home.City, o => o.MapFrom(s => s.Home.City));");

            reported.Has("MPR1002").ShouldBeFalse(reported.All());
        }

        [Fact]
        public void The_same_path_given_two_sources_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Home, o => o.Ignore())
           .ForPath(d => d.Home.City, o => o.MapFrom(s => s.Home.City))
           .ForPath(d => d.Home.City, o => o.MapFrom(s => s.Name));");

            reported.Count("MPR1002").ShouldBe(1, reported.All());
        }

        [Fact]
        public void Ignoring_a_member_and_giving_it_a_source_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => { o.Ignore(); o.MapFrom(s => s.Name); });");

            reported.Count("MPR1003").ShouldBe(1, reported.All());
        }

        /// <remarks>
        /// The contradiction reads the same whether it is written in one callback or spread over
        /// two, and it is easier to miss spread over two.
        /// </remarks>
        [Fact]
        public void Ignoring_in_one_call_and_sourcing_in_another_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.Ignore())
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name));");

            reported.Count("MPR1003").ShouldBe(1, reported.All());
        }

        [Fact]
        public void Ignoring_on_its_own_is_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.Ignore());");

            reported.Has("MPR1003").ShouldBeFalse(reported.All());
        }

        /// <remarks>
        /// A <c>MapFrom</c> whose own body mentions something called Ignore is not this.
        /// </remarks>
        [Fact]
        public void A_source_expression_that_merely_mentions_ignore_is_left_alone()
        {
            Reported reported = AnalyzerHarness.Run(Types + @"
public static class Helper
{
    public static string Ignore(string text) => text;
}

public static class Setup
{
    public static MapperConfiguration Build() => new MapperConfiguration(cfg =>
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => Helper.Ignore(s.Name))));
}
");

            reported.Has("MPR1003").ShouldBeFalse(reported.All());
        }

        [Fact]
        public void Building_the_destination_two_ways_is_reported()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonRecord>()
           .ConstructUsing(s => new PersonRecord(s.Name, s.Age))
           .ForCtorParam(""name"", o => o.MapFrom(s => s.Name));");

            reported.Count("MPR1004").ShouldBe(1, reported.All());
        }

        [Fact]
        public void Either_one_on_its_own_is_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonRecord>()
           .ForCtorParam(""name"", o => o.MapFrom(s => s.Name));
        cfg.CreateMap<PersonDto, PersonRecord>()
           .ConstructUsing(s => new PersonRecord(s.Name, s.Age));");

            reported.Has("MPR1004").ShouldBeFalse(reported.All());
        }

        /// <remarks>
        /// The two sides of a <c>ReverseMap</c> are two maps, and each may build its destination
        /// its own way.
        /// </remarks>
        [Fact]
        public void The_two_ways_either_side_of_a_reverse_map_are_left_alone()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonRecord>()
           .ForCtorParam(""name"", o => o.MapFrom(s => s.Name))
           .ReverseMap()
           .ConstructUsing(s => new Person());");

            reported.Has("MPR1004").ShouldBeFalse(reported.All());
        }

        /// <remarks>
        /// Somebody else's fluent builder with the same method names is not this one. The analyser
        /// matches symbols, and this is the test that says so.
        /// </remarks>
        [Fact]
        public void Another_librarys_fluent_builder_is_left_alone()
        {
            Reported reported = AnalyzerHarness.Run(Types + @"
public sealed class Elsewhere
{
    public Elsewhere CreateMap<TSource, TDestination>() => this;
    public Elsewhere ForMember(Func<PersonDto, object> member, Action<object> options) => this;
}

public static class Setup
{
    public static void Build()
    {
        new Elsewhere()
            .CreateMap<Person, PersonDto>()
            .ForMember(d => d.Name, o => { })
            .ForMember(d => d.Name, o => { });
    }
}
");

            reported.All().ShouldBe("(nothing reported)");
        }

        [Fact]
        public void A_configuration_with_nothing_wrong_reports_nothing()
        {
            Reported reported = Run(@"
        cfg.CreateMap<Person, PersonDto>()
           .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
           .ForMember(d => d.Age, o => o.Ignore())
           .ForPath(d => d.Home.City, o => o.MapFrom(s => s.Home.City))
           .ReverseMap()
           .ForMember(d => d.Name, o => o.Ignore());
        cfg.CreateMap<Address, Address>();");

            reported.All().ShouldBe("(nothing reported)");
        }
    }
}
