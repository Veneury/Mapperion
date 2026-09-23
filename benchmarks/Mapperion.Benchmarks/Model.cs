using System.Collections.Generic;

namespace Mapperion.Benchmarks
{
    public enum Status
    {
        Draft = 0,
        Open = 1,
        Closed = 2,
    }

    public enum StatusDto
    {
        Closed = 0,
        Open = 1,
        Draft = 2,
    }

    public sealed class City
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Address
    {
        public string Street { get; set; } = string.Empty;

        public City? City { get; set; }
    }

    public sealed class Client
    {
        public string Name { get; set; } = string.Empty;

        public Address? Address { get; set; }
    }

    public sealed class ClientDto
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Ten primitive members, the shape of B01.</summary>
    public sealed class Flat
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public double Weight { get; set; }

        public bool Active { get; set; }

        public long Sequence { get; set; }

        public short Revision { get; set; }

        public byte Priority { get; set; }

        public string Owner { get; set; } = string.Empty;
    }

    public sealed class FlatDto
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public double Weight { get; set; }

        public bool Active { get; set; }

        public long Sequence { get; set; }

        public short Revision { get; set; }

        public byte Priority { get; set; }

        public string Owner { get; set; } = string.Empty;
    }

    public sealed class Line
    {
        public string Code { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    public sealed class LineDto
    {
        public string Code { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public Status Status { get; set; }

        public Client? Client { get; set; }

        public List<Line> Lines { get; set; } = new List<Line>();
    }

    public sealed class OrderDto
    {
        public int Id { get; set; }

        public StatusDto Status { get; set; }

        public ClientDto? Client { get; set; }

        public List<LineDto> Lines { get; set; } = new List<LineDto>();
    }

    public sealed class FlattenedDto
    {
        public int Id { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string ClientAddressStreet { get; set; } = string.Empty;

        public string ClientAddressCityName { get; set; } = string.Empty;
    }

    public sealed record LineRecordDto(string Code, decimal Price);

    /// <summary>The floor every measurement is compared against.</summary>
    public static class ByHand
    {
        public static FlatDto Map(Flat source) => new FlatDto
        {
            Id = source.Id,
            Code = source.Code,
            Description = source.Description,
            Amount = source.Amount,
            Weight = source.Weight,
            Active = source.Active,
            Sequence = source.Sequence,
            Revision = source.Revision,
            Priority = source.Priority,
            Owner = source.Owner,
        };

        public static LineDto Map(Line source) => new LineDto
        {
            Code = source.Code,
            Price = source.Price,
        };

        public static LineRecordDto MapToRecord(Line source) => new LineRecordDto(source.Code, source.Price);

        public static ClientDto Map(Client source) => new ClientDto { Name = source.Name };

        public static OrderDto Map(Order source)
        {
            var lines = new List<LineDto>(source.Lines.Count);

            foreach (Line line in source.Lines)
            {
                lines.Add(Map(line));
            }

            return new OrderDto
            {
                Id = source.Id,
                Status = (StatusDto)source.Status,
                Client = source.Client is null ? null : Map(source.Client),
                Lines = lines,
            };
        }

        public static FlattenedDto MapFlattened(Order source) => new FlattenedDto
        {
            Id = source.Id,
            ClientName = source.Client is null ? null! : source.Client.Name,
            ClientAddressStreet = source.Client?.Address is null ? null! : source.Client.Address.Street,
            ClientAddressCityName = source.Client?.Address?.City is null ? null! : source.Client.Address.City.Name,
        };

        public static void Into(Flat source, FlatDto destination)
        {
            destination.Id = source.Id;
            destination.Code = source.Code;
            destination.Description = source.Description;
            destination.Amount = source.Amount;
            destination.Weight = source.Weight;
            destination.Active = source.Active;
            destination.Sequence = source.Sequence;
            destination.Revision = source.Revision;
            destination.Priority = source.Priority;
            destination.Owner = source.Owner;
        }
    }

    public static class Samples
    {
        public static Flat Flat() => new Flat
        {
            Id = 7,
            Code = "A-7",
            Description = "a description that is not interned",
            Amount = 19.5m,
            Weight = 1.25,
            Active = true,
            Sequence = 90000L,
            Revision = 3,
            Priority = 2,
            Owner = "ada",
        };

        public static Order Order(int lines)
        {
            var order = new Order
            {
                Id = 7,
                Status = Status.Open,
                Client = new Client
                {
                    Name = "Ada",
                    Address = new Address { Street = "Main", City = new City { Name = "Lisbon" } },
                },
            };

            for (int i = 0; i < lines; i++)
            {
                order.Lines.Add(new Line { Code = "L" + i, Price = i });
            }

            return order;
        }
    }
}
