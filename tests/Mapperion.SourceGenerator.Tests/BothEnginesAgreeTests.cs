using System;
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

        /// <summary>
        /// Dictionaries, which the generator had nothing for: a member the other engine copied
        /// was reported here as having no conversion at all.
        /// </summary>
        [Fact]
        public void They_agree_on_a_dictionary()
        {
            CompareBatch(SampleBatch());
        }

        /// <summary>
        /// A collection that is not there gives an empty one rather than throwing, which is what
        /// the run-time engine does with it unless null collections are asked for.
        /// </summary>
        [Fact]
        public void They_agree_on_a_collection_that_is_not_there()
        {
            Batch batch = SampleBatch();
            batch.Optional = null;

            CompareBatch(batch);
        }

        private static Batch SampleBatch() => new Batch
        {
            Lines = { ["a"] = new Line { Code = "A", Price = 1m } },
            Labels = { [1] = "one" },
            Optional = new List<Line> { new Line { Code = "B", Price = 2m } },
        };

        private static void CompareBatch(Batch batch)
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Batch, BatchDto>();
                cfg.CreateMap<Line, LineDto>();
            });

            configuration.AssertIsValid();

            BatchDto generated = new BatchMapper().ToDto(batch);
            BatchDto runtime = configuration.CreateMapper().Map<Batch, BatchDto>(batch);

            generated.Lines.Count.ShouldBe(runtime.Lines.Count);
            generated.Lines["a"].Code.ShouldBe(runtime.Lines["a"].Code);
            generated.Labels.Count.ShouldBe(runtime.Labels.Count);
            generated.Labels[1L].ShouldBe(runtime.Labels[1L]);
            generated.Optional.Count.ShouldBe(runtime.Optional.Count);
        }

        /// <remarks>
        /// The destination gets its own collection. Handing the source's over means two objects
        /// share one list, and a change to either is a change to both.
        /// </remarks>
        [Fact]
        public void They_agree_that_the_destination_does_not_share_the_source_collection()
        {
            Batch batch = SampleBatch();

            new BatchMapper().ToDto(batch).Lines.ShouldNotBeSameAs(batch.Lines);
        }

        /// <summary>
        /// Values read out of text, which the run-time engine gained in 0.11.0 and the generator
        /// never did: the same configuration mapped on one engine and stopped the build on the
        /// other.
        /// </summary>
        [Fact]
        public void They_agree_on_values_read_out_of_text()
        {
            CompareRow(new Row
            {
                Reference = "6f9619ff-8b86-d011-b42d-00c04fc964ff",
                Opened = "2026-09-27",
                Amount = "12.50",
            });
        }

        [Fact]
        public void They_agree_that_empty_text_is_an_absent_value()
        {
            CompareRow(new Row { Reference = string.Empty, Opened = string.Empty, Amount = "0" });
        }

        /// <remarks>
        /// Both refuse text that is meant to be a value and is not. They raise different types
        /// doing it — the run-time engine wraps everything in <c>MappingException</c> and the
        /// generated code has no such thing, because it emits no handler at all.
        /// </remarks>
        [Fact]
        public void They_agree_that_rubbish_in_a_value_is_refused()
        {
            var row = new Row { Reference = "not a guid", Opened = "2026-09-27", Amount = "0" };

            Should.Throw<Exception>(() => new RowMapper().ToDto(row));

            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Row, RowDto>());
            Should.Throw<Exception>(() => configuration.CreateMapper().Map<Row, RowDto>(row));
        }

        private static void CompareRow(Row row)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Row, RowDto>());
            configuration.AssertIsValid();

            RowDto generated = new RowMapper().ToDto(row);
            RowDto runtime = configuration.CreateMapper().Map<Row, RowDto>(row);

            generated.Reference.ShouldBe(runtime.Reference);
            generated.Opened.ShouldBe(runtime.Opened);
            generated.Amount.ShouldBe(runtime.Amount);
        }

        [Fact]
        public void They_agree_that_an_ignored_member_is_left_alone()
        {
            new OrderMapper().ToDto(SampleOrder()).Note
                .ShouldBe(RuntimeMapper().Map<Order, OrderDto>(SampleOrder()).Note);
        }
    }
}
