using Shouldly;
using Xunit;

namespace Mapperion.SourceGenerator.Tests
{
    public sealed class DiagnosticsTests
    {
        private const string Types = @"
public class Source { public int Id { get; set; } public string Name { get; set; } = """"; }
public class Destination { public int Id { get; set; } public string Name { get; set; } = """"; }
";

        [Fact]
        public void A_complete_mapper_generates_without_complaint()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Types + @"
[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);
}");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("Id = source.Id");
            outcome.Generated.ShouldContain("Name = source.Name");
        }

        [Fact]
        public void An_included_pair_with_no_method_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Animal { public int Age { get; set; } }
public class Dog : Animal { public string Breed { get; set; } = """"; }
public class AnimalDto { public int Age { get; set; } }
public class DogDto : AnimalDto { public string Breed { get; set; } = """"; }

[Mapperion.Mapper]
public partial class Mapper
{
    [Mapperion.MapperInclude(typeof(Dog), typeof(DogDto))]
    public partial AnimalDto Map(Animal source);
}");

            outcome.Reported("MPR0007").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void An_included_pair_that_is_not_derived_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Animal { public int Age { get; set; } }
public class Mineral { public int Age { get; set; } }
public class AnimalDto { public int Age { get; set; } }
public class MineralDto { public int Age { get; set; } }

[Mapperion.Mapper]
public partial class Mapper
{
    [Mapperion.MapperInclude(typeof(Mineral), typeof(MineralDto))]
    public partial AnimalDto Map(Animal source);

    public partial MineralDto Map(Mineral source);
}");

            outcome.Reported("MPR0008").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void A_mapper_that_is_not_partial_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Types + @"
[Mapperion.Mapper]
public class Mapper
{
}");

            outcome.Reported("MPR0001").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void A_destination_member_with_no_source_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Source { public int Id { get; set; } }
public class Destination { public int Id { get; set; } public string Missing { get; set; } = """"; }

[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);
}");

            outcome.Reported("MPR0002").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void An_ignored_member_is_not_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Source { public int Id { get; set; } }
public class Destination { public int Id { get; set; } public string Missing { get; set; } = """"; }

[Mapperion.Mapper]
public partial class Mapper
{
    [Mapperion.MapperIgnore(""Missing"")]
    public partial Destination Map(Source source);
}");

            outcome.Reported("MPR0002").ShouldBeFalse(outcome.Report());
        }

        [Fact]
        public void A_member_with_no_conversion_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Nested { public int Value { get; set; } }
public class NestedDto { public int Value { get; set; } }
public class Source { public Nested Item { get; set; } = new Nested(); }
public class Destination { public NestedDto Item { get; set; } = new NestedDto(); }

[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);
}");

            outcome.Reported("MPR0003").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void Adding_a_method_for_the_nested_pair_resolves_it()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Nested { public int Value { get; set; } }
public class NestedDto { public int Value { get; set; } }
public class Source { public Nested Item { get; set; } = new Nested(); }
public class Destination { public NestedDto Item { get; set; } = new NestedDto(); }

[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);

    public partial NestedDto MapNested(Nested source);
}");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("MapNested(source.Item)");
        }

        [Fact]
        public void A_destination_that_cannot_be_constructed_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
public class Source { public int Id { get; set; } }
public class Destination
{
    private Destination() { }
    public int Id { get; set; }
}

[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);
}");

            outcome.Reported("MPR0004").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void A_method_with_the_wrong_shape_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Types + @"
[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source first, Source second);
}");

            outcome.Reported("MPR0005").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void An_attribute_naming_a_member_that_does_not_exist_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Types + @"
[Mapperion.Mapper]
public partial class Mapper
{
    [Mapperion.MapProperty(""Nope"", ""Name"")]
    public partial Destination Map(Source source);
}");

            outcome.Reported("MPR0006").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void The_generated_code_reaches_for_nothing_at_run_time()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Types + @"
[Mapperion.Mapper]
public partial class Mapper
{
    public partial Destination Map(Source source);
}");

            outcome.Generated.ShouldNotContain("Reflection");
            outcome.Generated.ShouldNotContain("Expression");
            outcome.Generated.ShouldNotContain("Activator");
        }
    }
}
