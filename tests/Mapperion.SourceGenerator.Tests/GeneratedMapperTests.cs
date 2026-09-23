using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.SourceGenerator.Tests
{
    /// <summary>
    /// Exercises the code the generator wrote for <see cref="OrderMapper"/> at build time. Nothing
    /// here is reflective: these are ordinary method calls into ordinary generated C#.
    /// </summary>
    public sealed class GeneratedMapperTests
    {
        private static Order SampleOrder() => new Order
        {
            Id = 7,
            Revision = 3,
            Status = Status.Open,
            Customer = new Customer { Name = "Ada", Address = new Address { City = "Lisbon" } },
            Lines = { new Line { Code = "A", Price = 1.5m }, new Line { Code = "B", Price = 2m } },
        };

        [Fact]
        public void Members_are_matched_by_name()
        {
            OrderDto dto = new OrderMapper().ToDto(SampleOrder());

            dto.Id.ShouldBe(7L);
        }

        [Fact]
        public void An_explicit_path_reaches_through_nested_objects()
        {
            OrderDto dto = new OrderMapper().ToDto(SampleOrder());

            dto.CustomerName.ShouldBe("Ada");
            dto.CustomerCity.ShouldBe("Lisbon");
        }

        [Fact]
        public void A_null_along_an_explicit_path_yields_the_default()
        {
            Order order = SampleOrder();
            order.Customer!.Address = null;

            OrderDto dto = new OrderMapper().ToDto(order);

            dto.CustomerName.ShouldBe("Ada");
            dto.CustomerCity.ShouldBeNull();
        }

        [Fact]
        public void A_nullable_source_unwraps_to_its_value()
        {
            new OrderMapper().ToDto(SampleOrder()).Revision.ShouldBe(3);

            Order withoutRevision = SampleOrder();
            withoutRevision.Revision = null;

            new OrderMapper().ToDto(withoutRevision).Revision.ShouldBe(0);
        }

        [Fact]
        public void Enums_are_converted()
        {
            new OrderMapper().ToDto(SampleOrder()).Status.ShouldBe(StatusDto.Open);
        }

        [Fact]
        public void An_ignored_member_keeps_whatever_the_destination_gave_it()
        {
            new OrderMapper().ToDto(SampleOrder()).Note.ShouldBe(string.Empty);
        }

        [Fact]
        public void A_collection_member_uses_the_element_method()
        {
            List<LineDto> lines = new OrderMapper().ToDto(SampleOrder()).Lines;

            lines.Count.ShouldBe(2);
            lines[0].Code.ShouldBe("A");
            lines[0].Price.ShouldBe(1.5d);
        }

        [Fact]
        public void A_collection_method_maps_every_element()
        {
            List<LineDto> lines = new OrderMapper().ToDtos(SampleOrder().Lines);

            lines.Count.ShouldBe(2);
            lines[1].Code.ShouldBe("B");
        }

        [Fact]
        public void A_record_destination_is_built_through_its_constructor()
        {
            LineRecordDto dto = new OrderMapper().ToRecord(new Line { Code = "R", Price = 9m });

            dto.Code.ShouldBe("R");
            dto.Price.ShouldBe(9d);
        }

        [Fact]
        public void A_null_source_yields_the_default()
        {
            new OrderMapper().ToDto((Order)null!).ShouldBeNull();
        }
    }
}
