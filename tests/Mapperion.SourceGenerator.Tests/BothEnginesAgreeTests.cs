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

        /// <summary>
        /// The run-time engine matches enum members by name and only falls back to the number,
        /// which is what <see cref="EnumMappingPolicy.ByNameThenValue"/> means and what it does by
        /// default. A generator that casts the number instead does not fail: it quietly returns a
        /// different member.
        /// </summary>
        [Theory]
        [InlineData(Priority.Low)]
        [InlineData(Priority.Normal)]
        [InlineData(Priority.High)]
        public void They_agree_on_an_enum_whose_numbers_moved(Priority priority)
        {
            CompareTicket(new Ticket { Priority = priority });
        }

        [Fact]
        public void They_agree_on_an_enum_that_is_not_there()
        {
            CompareTicket(new Ticket { Escalation = null });
            CompareTicket(new Ticket { Escalation = Priority.High });
        }

        /// <summary>
        /// Text reaches an enum by name ignoring case, then as a number, and empty text is the
        /// absence of a value rather than a bad one. Anything else is the default under the policy
        /// both engines use unless told otherwise.
        /// </summary>
        [Theory]
        [InlineData("Email")]
        [InlineData("sms")]
        [InlineData("2")]
        [InlineData("")]
        [InlineData("nonsense")]
        public void They_agree_on_an_enum_read_out_of_text(string kind)
        {
            CompareTicket(new Ticket { Kind = kind });
        }

        [Fact]
        public void They_agree_when_the_enum_is_the_whole_map()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Priority, PriorityDto>());
            IMapper mapper = configuration.CreateMapper();

            foreach (Priority priority in new[] { Priority.Low, Priority.Normal, Priority.High })
            {
                new TicketMapper().ToDto(priority).ShouldBe(mapper.Map<Priority, PriorityDto>(priority));
            }
        }

        private static void CompareTicket(Ticket ticket)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Ticket, TicketDto>());
            configuration.AssertIsValid();

            TicketDto generated = new TicketMapper().ToDto(ticket);
            TicketDto runtime = configuration.CreateMapper().Map<Ticket, TicketDto>(ticket);

            generated.Priority.ShouldBe(runtime.Priority);
            generated.Escalation.ShouldBe(runtime.Escalation);
            generated.Kind.ShouldBe(runtime.Kind);
        }

        /// <summary>
        /// Flattening, with nothing configured on either side.
        /// </summary>
        /// <remarks>
        /// The generator used to report these as unmapped while the run-time engine found them on
        /// its own, so one configuration meant two different things depending on which engine
        /// read it.
        /// </remarks>
        [Fact]
        public void They_agree_on_a_member_found_by_spelling_out_a_path()
        {
            CompareSummary(SampleOrder());
        }

        [Fact]
        public void They_agree_on_a_spelt_out_path_that_stops_short()
        {
            Order order = SampleOrder();
            order.Customer!.Address = null;
            CompareSummary(order);

            order.Customer = null;
            CompareSummary(order);
        }

        private static void CompareSummary(Order order)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Order, OrderSummaryDto>());
            configuration.AssertIsValid();

            OrderSummaryDto generated = new SummaryMapper().ToSummary(order);
            OrderSummaryDto runtime = configuration.CreateMapper().Map<Order, OrderSummaryDto>(order);

            generated.Id.ShouldBe(runtime.Id);
            generated.CustomerName.ShouldBe(runtime.CustomerName);
            generated.CustomerAddressCity.ShouldBe(runtime.CustomerAddressCity);
        }

        [Fact]
        public void They_agree_that_an_ignored_member_is_left_alone()
        {
            new OrderMapper().ToDto(SampleOrder()).Note
                .ShouldBe(RuntimeMapper().Map<Order, OrderDto>(SampleOrder()).Note);
        }
    }
}
