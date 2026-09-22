using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class MappingTests
    {
        private static Invoice SampleInvoice() => new Invoice
        {
            Number = 42,
            Revision = 3,
            Amount = 19.5m,
            Status = Status.Open,
            Notes = "hello",
            Client = new Client
            {
                Name = "Ada",
                Address = new Address { Street = "Main", City = new City { Name = "Lisbon" } },
            },
            Items =
            {
                new Item { Code = "A", Price = 1.5m },
                new Item { Code = "B", Price = 2.5m },
            },
        };

        private static IMapper FullMapper()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Invoice, InvoiceDto>();
                cfg.CreateMap<Item, ItemDto>();
            });

            config.AssertIsValid();
            return config.CreateMapper();
        }

        [Fact]
        public void Maps_a_flat_member()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Number.ShouldBe(42L);
        }

        [Fact]
        public void Maps_a_flattened_path()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.ClientName.ShouldBe("Ada");
            dto.ClientAddressCityName.ShouldBe("Lisbon");
        }

        [Fact]
        public void A_null_along_a_flattened_path_yields_the_default()
        {
            var invoice = SampleInvoice();
            invoice.Client!.Address = null;

            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(invoice);

            dto.ClientName.ShouldBe("Ada");
            dto.ClientAddressCityName.ShouldBeNull();
        }

        [Fact]
        public void A_null_source_maps_to_the_default()
        {
            FullMapper().Map<Invoice, InvoiceDto>(null!).ShouldBeNull();
        }

        [Fact]
        public void Widening_and_narrowing_numeric_conversions_work()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Number.ShouldBe(42L);
            dto.Items[0].Price.ShouldBe(1.5d);
        }

        [Fact]
        public void A_value_converts_to_string()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Amount.ShouldBe(19.5m.ToString(System.Globalization.CultureInfo.CurrentCulture));
        }

        [Fact]
        public void A_nullable_source_unwraps_to_its_value()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Revision.ShouldBe(3);
        }

        [Fact]
        public void A_null_nullable_source_yields_the_default()
        {
            var invoice = SampleInvoice();
            invoice.Revision = null;

            FullMapper().Map<Invoice, InvoiceDto>(invoice).Revision.ShouldBe(0);
        }

        [Fact]
        public void Enums_map_by_name_before_value()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Status.ShouldBe(StatusDto.Open);
            ((int)dto.Status).ShouldBe(8);
        }

        [Fact]
        public void Enums_map_by_value_when_configured()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.EnumMapping = Mapperion.Model.EnumMappingPolicy.ByValue;
                cfg.CreateMap<Invoice, InvoiceDto>();
                cfg.CreateMap<Item, ItemDto>();
            });

            InvoiceDto dto = config.CreateMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            ((int)dto.Status).ShouldBe((int)Status.Open);
            dto.Status.ShouldNotBe(StatusDto.Open);
        }

        [Fact]
        public void A_nested_object_uses_its_own_map()
        {
            InvoiceDto dto = FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice());

            dto.Items.Count.ShouldBe(2);
            dto.Items[0].Code.ShouldBe("A");
            dto.Items[1].Code.ShouldBe("B");
        }

        [Fact]
        public void A_collection_maps_to_an_array()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Invoice, InvoiceArrayDto>();
                cfg.CreateMap<Item, ItemDto>();
            });

            ItemDto[] items = config.CreateMapper().Map<Invoice, InvoiceArrayDto>(SampleInvoice()).Items;

            items.Length.ShouldBe(2);
            items[1].Code.ShouldBe("B");
        }

        [Fact]
        public void A_collection_maps_to_a_sequence()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Invoice, InvoiceSequenceDto>();
                cfg.CreateMap<Item, ItemDto>();
            });

            IEnumerable<ItemDto> items = config.CreateMapper()
                .Map<Invoice, InvoiceSequenceDto>(SampleInvoice()).Items;

            items.Count().ShouldBe(2);
        }

        [Fact]
        public void A_collection_maps_to_a_set_of_converted_elements()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Invoice, InvoiceSetDto>()
                   .ForMember(d => d.Items, o => o.MapFrom(s => s.Items.Select(i => i.Code))));

            HashSet<string> items = config.CreateMapper().Map<Invoice, InvoiceSetDto>(SampleInvoice()).Items;

            items.Count.ShouldBe(2);
            items.ShouldContain("A");
            items.ShouldContain("B");
        }

        [Fact]
        public void A_null_collection_becomes_an_empty_one_by_default()
        {
            var invoice = SampleInvoice();
            invoice.Items = null!;

            FullMapper().Map<Invoice, InvoiceDto>(invoice).Items.ShouldBeEmpty();
        }

        [Fact]
        public void A_null_collection_stays_null_when_allowed()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.AllowNullCollections = true;
                cfg.CreateMap<Invoice, InvoiceDto>();
                cfg.CreateMap<Item, ItemDto>();
            });

            var invoice = SampleInvoice();
            invoice.Items = null!;

            config.CreateMapper().Map<Invoice, InvoiceDto>(invoice).Items.ShouldBeNull();
        }

        [Fact]
        public void A_read_only_source_property_is_read()
        {
            FullMapper().Map<Invoice, InvoiceDto>(SampleInvoice()).Reference.ShouldBe("REF-42");
        }

        [Fact]
        public void Mapping_into_an_existing_instance_keeps_it()
        {
            var destination = new InvoiceDto();

            InvoiceDto returned = FullMapper().Map(SampleInvoice(), destination);

            returned.ShouldBeSameAs(destination);
            destination.Number.ShouldBe(42L);
        }

        [Fact]
        public void The_runtime_type_overload_finds_the_map()
        {
            var dto = FullMapper().Map<InvoiceDto>(SampleInvoice());

            dto.Number.ShouldBe(42L);
        }

        [Fact]
        public void The_non_generic_overload_finds_the_map()
        {
            object? dto = FullMapper().Map(SampleInvoice(), typeof(Invoice), typeof(InvoiceDto));

            dto.ShouldBeOfType<InvoiceDto>().Number.ShouldBe(42L);
        }

        [Fact]
        public void Structs_map_without_boxing_surprises()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Point, PointDto>());

            PointDto dto = config.CreateMapper().Map<Point, PointDto>(new Point { X = 1, Y = 2 });

            dto.X.ShouldBe(1);
            dto.Y.ShouldBe(2);
        }

        [Fact]
        public void A_self_referencing_map_terminates()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>());

            var chain = new Node { Name = "a", Next = new Node { Name = "b" } };
            NodeDto dto = config.CreateMapper().Map<Node, NodeDto>(chain);

            dto.Name.ShouldBe("a");
            dto.Next!.Name.ShouldBe("b");
            dto.Next.Next.ShouldBeNull();
        }

        [Fact]
        public void Explicit_MapFrom_expressions_are_honoured()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Invoice, InvoiceDto>()
                   .ForMember(d => d.Notes, o => o.MapFrom(s => s.Notes!.ToUpperInvariant()));
                cfg.CreateMap<Item, ItemDto>();
            });

            config.CreateMapper().Map<Invoice, InvoiceDto>(SampleInvoice()).Notes.ShouldBe("HELLO");
        }

        [Fact]
        public void A_condition_can_skip_an_assignment()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Invoice, InvoiceDto>()
                   .ForMember(d => d.Notes, o => o.Condition(s => s.Number > 100));
                cfg.CreateMap<Item, ItemDto>();
            });

            config.CreateMapper().Map<Invoice, InvoiceDto>(SampleInvoice()).Notes.ShouldBe(string.Empty);
        }

        [Fact]
        public void A_null_substitute_replaces_a_missing_value()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Invoice, InvoiceDto>()
                   .ForMember(d => d.Notes, o => o.NullSubstitute("none"));
                cfg.CreateMap<Item, ItemDto>();
            });

            var invoice = SampleInvoice();
            invoice.Notes = null;

            config.CreateMapper().Map<Invoice, InvoiceDto>(invoice).Notes.ShouldBe("none");
        }

        [Fact]
        public void An_ignored_member_keeps_its_destination_value()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.MaxFlatteningDepth = 4;
                cfg.CreateMap<Invoice, InvoiceDto>()
                   .ForMember(d => d.Notes, o => o.Ignore());
                cfg.CreateMap<Item, ItemDto>();
            });

            var destination = new InvoiceDto { Notes = "kept" };
            config.CreateMapper().Map(SampleInvoice(), destination);

            destination.Notes.ShouldBe("kept");
        }

        [Fact]
        public void Mapping_an_unconfigured_pair_fails_with_a_clear_message()
        {
            IMapper mapper = new MapperConfiguration(cfg => cfg.CreateMap<Item, ItemDto>()).CreateMapper();

            MappingException error = Should.Throw<MappingException>(
                () => mapper.Map<Invoice, InvoiceDto>(SampleInvoice()));

            error.Message.ShouldContain("CreateMap<Invoice, InvoiceDto>()");
        }

        [Fact]
        public void A_destination_without_a_parameterless_constructor_is_reported()
        {
            IMapper mapper = new MapperConfiguration(cfg =>
                cfg.CreateMap<Item, NoDefaultConstructor>()).CreateMapper();

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => mapper.Map<Item, NoDefaultConstructor>(new Item()));

            error.Message.ShouldContain("parameterless constructor");
        }

        [Fact]
        public void The_same_mapper_is_reusable_and_plans_are_cached()
        {
            IMapper mapper = FullMapper();

            for (int i = 0; i < 100; i++)
            {
                mapper.Map<Invoice, InvoiceDto>(SampleInvoice()).Number.ShouldBe(42L);
            }
        }

        [Fact]
        public void Mapping_is_safe_from_several_threads()
        {
            IMapper mapper = FullMapper();
            Invoice invoice = SampleInvoice();

            InvoiceDto[] results = Enumerable.Range(0, 64)
                .AsParallel()
                .Select(_ => mapper.Map<Invoice, InvoiceDto>(invoice))
                .ToArray();

            results.Length.ShouldBe(64);
            results.ShouldAllBe(r => r.ClientAddressCityName == "Lisbon");
        }
    }
}
