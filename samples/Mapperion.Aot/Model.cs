using System;
using System.Collections.Generic;

namespace Mapperion.Aot
{
    /// <summary>
    /// The shapes the sample maps. Between them they reach every part of the generator worth
    /// proving under AOT: primitives, a nested object, a collection, an explicit path, a record
    /// built through its constructor, an enum and a nullable.
    /// </summary>
    public sealed class Order
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public DateTime PlacedOn { get; set; }

        public decimal? Discount { get; set; }

        public Status Status { get; set; }

        public Customer Customer { get; set; } = new Customer();

        public List<Line> Lines { get; set; } = new List<Line>();
    }

    public sealed class Customer
    {
        public string Name { get; set; } = string.Empty;

        public Address Address { get; set; } = new Address();
    }

    public sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    public sealed class Line
    {
        public string Code { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }

    public enum Status
    {
        Draft,
        Placed,
        Shipped,
    }

    public sealed class OrderDto
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public DateTime PlacedOn { get; set; }

        public decimal? Discount { get; set; }

        public StatusDto Status { get; set; }

        public CustomerDto Customer { get; set; } = new CustomerDto();

        public List<LineDto> Lines { get; set; } = new List<LineDto>();

        public string CustomerCity { get; set; } = string.Empty;
    }

    public sealed class CustomerDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class LineDto
    {
        public string Code { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }

    public sealed record LineSummary(string Code, decimal Price);

    public enum StatusDto
    {
        Draft,
        Placed,
        Shipped,
    }

    /// <summary>
    /// The compile-time mapper. Everything it does is written into the assembly by the generator,
    /// so nothing here needs reflection, run-time code generation or anything else the AOT
    /// compiler cannot see through.
    /// </summary>
    [Mapperion.Mapper]
    public partial class OrderMapper
    {
        [Mapperion.MapProperty("Customer.Address.City", "CustomerCity")]
        public partial OrderDto ToDto(Order source);

        public partial CustomerDto ToDto(Customer source);

        public partial LineDto ToDto(Line source);

        public partial List<LineDto> ToDtos(List<Line> source);

        public partial LineSummary ToSummary(Line source);
    }
}
