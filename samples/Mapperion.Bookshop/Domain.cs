using System;
using System.Collections.Generic;

namespace Mapperion.Bookshop
{
    /// <summary>
    /// What the bookshop stores. Written the way an application writes its entities and not the
    /// way a test writes a fixture: navigation properties both ways, a value object held inline,
    /// an inheritance hierarchy, and no concession anywhere to what a mapper might prefer.
    /// </summary>
    public sealed class Customer
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public Address Address { get; set; } = new Address();

        public List<Order> Orders { get; } = new List<Order>();
    }

    public sealed class Address
    {
        public string Street { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string PostCode { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;
    }

    public enum OrderStatus
    {
        Draft = 0,
        Placed = 1,
        Shipped = 2,
        Cancelled = 3,
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public DateTime PlacedAt { get; set; }

        public OrderStatus Status { get; set; }

        public int CustomerId { get; set; }

        public Customer Customer { get; set; } = null!;

        public List<OrderLine> Lines { get; } = new List<OrderLine>();

        public Payment? Payment { get; set; }
    }

    public sealed class OrderLine
    {
        public int Id { get; set; }

        public string Isbn { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public int OrderId { get; set; }
    }

    public abstract class Payment
    {
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public int OrderId { get; set; }
    }

    public sealed class CardPayment : Payment
    {
        public string Last4 { get; set; } = string.Empty;
    }

    public sealed class TransferPayment : Payment
    {
        public string Iban { get; set; } = string.Empty;
    }
}
