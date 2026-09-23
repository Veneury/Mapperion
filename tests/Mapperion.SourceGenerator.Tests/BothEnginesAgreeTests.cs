using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.SourceGenerator.Tests
{
    /// <summary>
    /// The guarantee ADR-0005 rests on. The two engines share no code — one compiles expression
    /// trees against <c>System.Type</c>, the other writes C# against Roslyn symbols — so the only
    /// thing keeping them honest is running the same case through both and comparing.
    /// </summary>
    public sealed class BothEnginesAgreeTests
    {
        private static Order SampleOrder() => new Order
        {
            Id = 7,
            Revision = 3,
            Status = Status.Open,
            Customer = new Customer { Name = "Ada", Address = new Address { City = "Lisbon" } },
            Lines = { new Line { Code = "A", Price = 1.5m }, new Line { Code = "B", Price = 2m } },
        };

        private static IMapper RuntimeMapper()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Order, OrderDto>()
                   .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer!.Name))
                   .ForMember(d => d.CustomerCity, o => o.MapFrom(s => s.Customer!.Address!.City))
                   .ForMember(d => d.Note, o => o.Ignore());

                cfg.CreateMap<Line, LineDto>();
            });

            configuration.AssertIsValid();
            return configuration.CreateMapper();
        }

        private static void Compare(Order order)
        {
            OrderDto generated = new OrderMapper().ToDto(order);
            OrderDto runtime = RuntimeMapper().Map<Order, OrderDto>(order);

            generated.Id.ShouldBe(runtime.Id);
            generated.Revision.ShouldBe(runtime.Revision);
            generated.Status.ShouldBe(runtime.Status);
            generated.CustomerName.ShouldBe(runtime.CustomerName);
            generated.CustomerCity.ShouldBe(runtime.CustomerCity);
            generated.Lines.Count.ShouldBe(runtime.Lines.Count);

            for (int i = 0; i < generated.Lines.Count; i++)
            {
                generated.Lines[i].Code.ShouldBe(runtime.Lines[i].Code);
                generated.Lines[i].Price.ShouldBe(runtime.Lines[i].Price);
            }
        }

        [Fact]
        public void They_agree_on_a_full_object()
        {
            Compare(SampleOrder());
        }

        [Fact]
        public void They_agree_when_a_nested_object_is_missing()
        {
            Order order = SampleOrder();
            order.Customer!.Address = null;

            Compare(order);
        }

        [Fact]
        public void They_agree_when_a_nullable_is_empty()
        {
            Order order = SampleOrder();
            order.Revision = null;

            Compare(order);
        }

        [Fact]
        public void They_agree_on_an_empty_collection()
        {
            Order order = SampleOrder();
            order.Lines = new List<Line>();

            Compare(order);
        }

        [Fact]
        public void They_agree_that_a_null_source_yields_nothing()
        {
            new OrderMapper().ToDto((Order)null!).ShouldBeNull();
            RuntimeMapper().Map<Order, OrderDto>(null!).ShouldBeNull();
        }

        [Fact]
        public void They_agree_that_an_ignored_member_is_left_alone()
        {
            new OrderMapper().ToDto(SampleOrder()).Note
                .ShouldBe(RuntimeMapper().Map<Order, OrderDto>(SampleOrder()).Note);
        }
    }
}
