using System.Collections.Generic;

namespace Mapperion.Tests.Execution
{
    public enum Status
    {
        Draft = 0,
        Open = 1,
        Closed = 2,
    }

    public enum StatusDto
    {
        Closed = 7,
        Open = 8,
        Draft = 9,
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

    public sealed class Item
    {
        public string Code { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    public sealed class ItemDto
    {
        public string Code { get; set; } = string.Empty;

        public double Price { get; set; }
    }

    public sealed class Invoice
    {
        public int Number { get; set; }

        public int? Revision { get; set; }

        public decimal Amount { get; set; }

        public Status Status { get; set; }

        public string? Notes { get; set; }

        public Client? Client { get; set; }

        public List<Item> Items { get; set; } = new List<Item>();

        public string Reference => "REF-" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class InvoiceDto
    {
        public long Number { get; set; }

        public int Revision { get; set; }

        public string Amount { get; set; } = string.Empty;

        public StatusDto Status { get; set; }

        public string Notes { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;

        public string ClientAddressCityName { get; set; } = string.Empty;

        public List<ItemDto> Items { get; set; } = new List<ItemDto>();

        public string Reference { get; set; } = string.Empty;
    }

    public sealed class InvoiceArrayDto
    {
        public ItemDto[] Items { get; set; } = System.Array.Empty<ItemDto>();
    }

    public sealed class InvoiceSetDto
    {
        public HashSet<string> Items { get; set; } = new HashSet<string>();
    }

    public sealed class InvoiceSequenceDto
    {
        public IEnumerable<ItemDto> Items { get; set; } = System.Array.Empty<ItemDto>();
    }

    public sealed class Node
    {
        public string Name { get; set; } = string.Empty;

        public Node? Next { get; set; }
    }

    public sealed class NodeDto
    {
        public string Name { get; set; } = string.Empty;

        public NodeDto? Next { get; set; }
    }

    public struct Point
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public struct PointDto
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class NoDefaultConstructor
    {
        public NoDefaultConstructor(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
