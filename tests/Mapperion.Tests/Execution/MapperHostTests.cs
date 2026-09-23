using System;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Tag
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TagDto
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <remarks>
    /// The host is global, so every case here starts by clearing it. They share one class because
    /// xUnit runs the cases of a class one at a time, and two of these running at once would be
    /// fighting over the same field.
    /// </remarks>
    public sealed class MapperHostTests : IDisposable
    {
        private static IMapper Tags() => new MapperConfiguration(
            cfg => cfg.CreateMap<Tag, TagDto>()).CreateMapper();

        public MapperHostTests() => MapperHost.Reset();

        public void Dispose() => MapperHost.Reset();

        [Fact]
        public void The_installed_mapper_is_what_comes_back()
        {
            IMapper mapper = Tags();
            MapperHost.Initialize(mapper);

            MapperHost.Instance.ShouldBeSameAs(mapper);
            MapperHost.Instance.Map<Tag, TagDto>(new Tag { Name = "b" }).Name.ShouldBe("b");
        }

        [Fact]
        public void Asking_before_installing_says_what_to_do()
        {
            InvalidOperationException error = Should.Throw<InvalidOperationException>(() => MapperHost.Instance);

            error.Message.ShouldContain("MapperHost.Initialize");
        }

        [Fact]
        public void Installing_twice_is_refused()
        {
            MapperHost.Initialize(Tags());

            Should.Throw<InvalidOperationException>(() => MapperHost.Initialize(Tags()));
        }

        [Fact]
        public void Resetting_allows_installing_again()
        {
            MapperHost.Initialize(Tags());
            MapperHost.Reset();

            IMapper second = Tags();
            MapperHost.Initialize(second);

            MapperHost.Instance.ShouldBeSameAs(second);
        }

        [Fact]
        public void Whether_one_is_installed_can_be_asked()
        {
            MapperHost.IsInitialized.ShouldBeFalse();

            MapperHost.Initialize(Tags());

            MapperHost.IsInitialized.ShouldBeTrue();
        }

        [Fact]
        public void A_null_mapper_is_rejected()
        {
            Should.Throw<ArgumentNullException>(() => MapperHost.Initialize(null!));
        }
    }
}
