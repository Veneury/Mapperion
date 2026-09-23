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
}
