using System;
using System.Collections.Generic;

namespace Mapperion.Bookshop
{
    /// <summary>
    /// What the bookshop hands out. These are shaped for the caller rather than for the database,
    /// which is the whole reason a mapper is in the picture: flat where the entity is nested, a
    /// record where the entity is a class, and its own enum so the wire format does not move when
    /// somebody reorders the one inside.
    /// </summary>
    public sealed class OrderSummaryDto
    {
        public string Reference { get; set; } = string.Empty;

        public DateTime PlacedAt { get; set; }

        public OrderState Status { get; set; }

        public string CustomerFullName { get; set; } = string.Empty;

        public string CustomerAddressCity { get; set; } = string.Empty;

        public int LineCount { get; set; }
    }

    /// <summary>
    /// The same four states in another order, so that a value crossing by number rather than by
    /// name would be visibly wrong rather than accidentally right.
    /// </summary>
    public enum OrderState
    {
        Cancelled = 0,
        Shipped = 1,
        Placed = 2,
        Draft = 3,
    }

    public sealed class OrderDetailDto
    {
        public string Reference { get; set; } = string.Empty;

        public OrderState Status { get; set; }

        public CustomerDto Customer { get; set; } = new CustomerDto();

        public List<OrderLineDto> Lines { get; set; } = new List<OrderLineDto>();

        public PaymentDto? Payment { get; set; }

        public decimal Total { get; set; }
    }

    public sealed class CustomerDto
    {
        public string FullName { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;
    }

    public sealed record OrderLineDto(string Isbn, string Title, int Quantity, decimal UnitPrice);

    public abstract class PaymentDto
    {
        public decimal Amount { get; set; }
    }

    public sealed class CardPaymentDto : PaymentDto
    {
        public string Last4 { get; set; } = string.Empty;
    }

    public sealed class TransferPaymentDto : PaymentDto
    {
        public string Iban { get; set; } = string.Empty;
    }
}
