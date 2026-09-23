using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Basket
    {
        public List<string> Codes { get; set; } = new List<string>();

        public List<string>? Missing { get; set; }
    }

    public sealed class BasketSameDto
    {
        public List<string> Codes { get; set; } = new List<string>();
    }

    public sealed class BasketReadOnlyDto
    {
        public IReadOnlyList<string> Codes { get; set; } = new List<string>();

        public IReadOnlyList<string>? Missing { get; set; }
    }

    public sealed class CollectionCopyTests
    {
        private static Basket SampleBasket() => new Basket { Codes = { "a", "b" } };

        [Fact]
        public void An_identical_collection_type_is_rebuilt_not_shared()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Basket, BasketSameDto>());
            Basket source = SampleBasket();

            BasketSameDto dto = config.CreateMapper().Map<Basket, BasketSameDto>(source);

            dto.Codes.ShouldBe(source.Codes);
            dto.Codes.ShouldNotBeSameAs(source.Codes);
        }

        [Fact]
        public void An_assignable_collection_type_is_rebuilt_not_shared()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Basket, BasketReadOnlyDto>());
            Basket source = SampleBasket();

            BasketReadOnlyDto dto = config.CreateMapper().Map<Basket, BasketReadOnlyDto>(source);

            dto.Codes.ShouldBe(source.Codes);
            dto.Codes.ShouldNotBeSameAs(source.Codes);
        }

        [Fact]
        public void Changing_the_destination_does_not_reach_the_source()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Basket, BasketSameDto>());
            Basket source = SampleBasket();

            BasketSameDto dto = config.CreateMapper().Map<Basket, BasketSameDto>(source);
            dto.Codes.Add("c");

            source.Codes.Count.ShouldBe(2);
        }

        [Fact]
        public void An_assignable_collection_honours_AllowNullCollections()
        {
            var allowed = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<Basket, BasketReadOnlyDto>();
            });

            var refused = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = false;
                cfg.CreateMap<Basket, BasketReadOnlyDto>();
            });

            allowed.CreateMapper().Map<Basket, BasketReadOnlyDto>(new Basket()).Missing.ShouldBeNull();
            refused.CreateMapper().Map<Basket, BasketReadOnlyDto>(new Basket()).Missing.ShouldBeEmpty();
        }
    }
}
