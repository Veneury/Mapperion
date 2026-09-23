using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    /// <summary>
    /// Mapping a collection without declaring a map for the collection pair itself, which is how
    /// most callers reach for a mapper: the map is for the element, the call is for the list.
    /// </summary>
    public sealed class TopLevelCollectionTests
    {
        private static IMapper Mapper() =>
            new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>()).CreateMapper();

        private static List<Item> Items() => new List<Item>
        {
            new Item { Code = "a", Price = 1m },
            new Item { Code = "b", Price = 2m },
        };

        [Fact]
        public void A_list_maps_to_a_list()
        {
            List<ItemDto> mapped = Mapper().Map<List<Item>, List<ItemDto>>(Items());

            mapped.Count.ShouldBe(2);
            mapped[1].Code.ShouldBe("b");
        }

        [Fact]
        public void A_list_maps_to_an_array()
        {
            ItemDto[] mapped = Mapper().Map<List<Item>, ItemDto[]>(Items());

            mapped.Length.ShouldBe(2);
        }

        [Fact]
        public void A_sequence_maps_to_a_read_only_list()
        {
            IReadOnlyList<ItemDto> mapped = Mapper().Map<IEnumerable<Item>, IReadOnlyList<ItemDto>>(Items());

            mapped.Count.ShouldBe(2);
        }

        [Fact]
        public void The_result_is_a_new_collection()
        {
            List<Item> source = Items();

            Mapper().Map<List<Item>, List<ItemDto>>(source).ShouldNotBeSameAs(source);
        }

        [Fact]
        public void A_dictionary_maps_at_the_top_level()
        {
            var source = new Dictionary<string, Item> { ["x"] = new Item { Code = "a" } };

            Dictionary<string, ItemDto> mapped =
                Mapper().Map<Dictionary<string, Item>, Dictionary<string, ItemDto>>(source);

            mapped["x"].Code.ShouldBe("a");
        }

        [Fact]
        public void An_element_pair_with_no_map_is_still_reported()
        {
            IMapper mapper = new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>()).CreateMapper();

            Should.Throw<MapperConfigurationException>(
                () => mapper.Map<List<Item>, List<Invoice>>(Items()));
        }

        [Fact]
        public void A_pair_that_is_not_a_collection_is_still_reported_as_missing()
        {
            IMapper mapper = new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>()).CreateMapper();

            Should.Throw<MappingException>(() => mapper.Map<Invoice, InvoiceDto>(new Invoice()));
        }
    }
}
