using System;
using System.Collections.Generic;

namespace Mapperion.SourceGenerator.Tests
{
    public sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    public sealed class Customer
    {
        public string Name { get; set; } = string.Empty;

        public Address? Address { get; set; }
    }

    public enum Status
    {
        Draft = 0,
        Open = 1,
    }

    public enum StatusDto
    {
        Draft = 0,
        Open = 1,
    }

    public sealed class Line
    {
        public string Code { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    public sealed class LineDto
    {
        public string Code { get; set; } = string.Empty;

        public double Price { get; set; }
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public int? Revision { get; set; }

        public Status Status { get; set; }

        public Customer? Customer { get; set; }

        public List<Line> Lines { get; set; } = new List<Line>();
    }

    public sealed class OrderDto
    {
        public long Id { get; set; }

        public int Revision { get; set; }

        public StatusDto Status { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerCity { get; set; } = string.Empty;

        public List<LineDto> Lines { get; set; } = new List<LineDto>();

        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// Names that match while the numbers do not, which is what happens the first time anybody
    /// reorders an enum. Mapping by value here gives the wrong member rather than no answer.
    /// </summary>
    public enum Priority
    {
        Low = 1,
        Normal = 2,
        High = 3,
    }

    public enum PriorityDto
    {
        High = 1,
        Normal = 2,
        Urgent = 3,
    }

    public enum Channel
    {
        Email = 1,
        Sms = 2,
    }

    public sealed class Ticket
    {
        public Priority Priority { get; set; }

        public Priority? Escalation { get; set; }

        public string Kind { get; set; } = string.Empty;
    }

    public sealed class TicketDto
    {
        public PriorityDto Priority { get; set; }

        public PriorityDto Escalation { get; set; }

        public Channel Kind { get; set; }
    }

    /// <summary>
    /// Nothing configured: every member here is found by spelling out a path through the source,
    /// which is the convention AutoMapper is known for and the one the run-time engine follows.
    /// </summary>
    public sealed class OrderSummaryDto
    {
        public long Id { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    public sealed class Batch
    {
        public Dictionary<string, Line> Lines { get; set; } = new Dictionary<string, Line>();

        public Dictionary<int, string> Labels { get; set; } = new Dictionary<int, string>();

        public List<Line>? Optional { get; set; }
    }

    public sealed class BatchDto
    {
        public Dictionary<string, LineDto> Lines { get; set; } = new Dictionary<string, LineDto>();

        public IReadOnlyDictionary<long, string> Labels { get; set; } = new Dictionary<long, string>();

        public List<LineDto> Optional { get; set; } = new List<LineDto>();
    }

    public sealed class Row
    {
        public string Reference { get; set; } = string.Empty;

        public string Opened { get; set; } = string.Empty;

        public string Amount { get; set; } = string.Empty;
    }

    public sealed class RowDto
    {
        public Guid Reference { get; set; }

        public DateOnly Opened { get; set; }

        public decimal Amount { get; set; }
    }

    public class Payment
    {
        public decimal Amount { get; set; }
    }

    public class CardPayment : Payment
    {
        public string Last4 { get; set; } = string.Empty;
    }

    public sealed class InstalmentPayment : CardPayment
    {
        public int Months { get; set; }
    }

    public class PaymentDto
    {
        public decimal Amount { get; set; }
    }

    public class CardPaymentDto : PaymentDto
    {
        public string Last4 { get; set; } = string.Empty;
    }

    public sealed class InstalmentPaymentDto : CardPaymentDto
    {
        public int Months { get; set; }
    }

    public sealed record LineRecordDto(string Code, double Price);

    /// <summary>
    /// The generator writes every body in this class at compile time. If it wrote something that
    /// does not compile, this project does not build, which makes the build itself the first test.
    /// </summary>
    [Mapper]
    public partial class OrderMapper
    {
        [MapProperty("Customer.Name", "CustomerName")]
        [MapProperty("Customer.Address.City", "CustomerCity")]
        [MapperIgnore("Note")]
        public partial OrderDto ToDto(Order source);

        public partial LineDto ToDto(Line source);

        public partial List<LineDto> ToDtos(IEnumerable<Line> source);

        public partial LineRecordDto ToRecord(Line source);
    }

    [Mapper]
    public partial class PaymentMapper
    {
        [MapperInclude(typeof(CardPayment), typeof(CardPaymentDto))]
        [MapperInclude(typeof(InstalmentPayment), typeof(InstalmentPaymentDto))]
        public partial PaymentDto ToDto(Payment source);

        public partial CardPaymentDto ToDto(CardPayment source);

        public partial InstalmentPaymentDto ToDto(InstalmentPayment source);
    }

    [Mapper]
    public partial class RowMapper
    {
        public partial RowDto ToDto(Row source);
    }

    [Mapper]
    public partial class BatchMapper
    {
        public partial BatchDto ToDto(Batch source);

        public partial LineDto ToDto(Line source);
    }

    [Mapper]
    public partial class SummaryMapper
    {
        public partial OrderSummaryDto ToSummary(Order source);
    }

    [Mapper]
    public partial class TicketMapper
    {
        public partial TicketDto ToDto(Ticket source);

        public partial PriorityDto ToDto(Priority source);
    }
}
