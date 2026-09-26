using System;
using System.Collections.Generic;

namespace Invoicing
{
    /// <summary>
    /// The domain and the contracts, shared by both configurations. Nothing here changes in a
    /// migration, which is the point: the only thing that moves is the mapping layer.
    /// </summary>
    public sealed class Invoice
    {
        public string Number { get; set; } = string.Empty;

        public DateTime IssuedOn { get; set; }

        public Currency Currency { get; set; }

        public Party Customer { get; set; } = new Party();

        public List<InvoiceLine> Lines { get; } = new List<InvoiceLine>();

        public decimal? Discount { get; set; }

        public string? Notes { get; set; }

        public bool Cancelled { get; set; }
    }

    public sealed class Party
    {
        public string Name { get; set; } = string.Empty;

        public string TaxId { get; set; } = string.Empty;

        public Place Place { get; set; } = new Place();
    }

    public sealed class Place
    {
        public string City { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;
    }

    public sealed class InvoiceLine
    {
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TaxRate { get; set; }
    }

    public enum Currency
    {
        Eur = 0,
        Usd = 1,
        Gbp = 2,
    }

    public enum CurrencyCode
    {
        Gbp = 0,
        Usd = 1,
        Eur = 2,
    }

    public sealed class InvoiceDto
    {
        public string Number { get; set; } = string.Empty;

        public string IssuedOn { get; set; } = string.Empty;

        public CurrencyCode Currency { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPlaceCity { get; set; } = string.Empty;

        public string CustomerPlaceCountry { get; set; } = string.Empty;

        public List<LineDto> Lines { get; set; } = new List<LineDto>();

        public decimal Net { get; set; }

        public decimal Tax { get; set; }

        public decimal Discount { get; set; }

        public string Notes { get; set; } = string.Empty;

        public string PreparedBy { get; set; } = string.Empty;

        public string Internal { get; set; } = string.Empty;
    }

    public sealed record LineDto(string Description, int Quantity, decimal Amount);

    public static class Sample
    {
        public static Invoice One()
        {
            var invoice = new Invoice
            {
                Number = "INV-2026-0042",
                IssuedOn = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc),
                Currency = Currency.Gbp,
                Customer = new Party
                {
                    Name = "Ada Lovelace",
                    TaxId = "GB123456789",
                    Place = new Place { City = "London", Country = "United Kingdom" },
                },
                Discount = 5.00m,
                Notes = "Deliver to the side entrance.",
            };

            invoice.Lines.Add(new InvoiceLine { Description = "Analytical engine, hire", Quantity = 1, UnitPrice = 120.00m, TaxRate = 0.20m });
            invoice.Lines.Add(new InvoiceLine { Description = "Punch cards, box of 500", Quantity = 3, UnitPrice = 8.50m, TaxRate = 0.20m });

            return invoice;
        }

        /// <summary>The awkward one: no notes, no discount, and cancelled.</summary>
        public static Invoice Two()
        {
            var invoice = new Invoice
            {
                Number = "INV-2026-0043",
                IssuedOn = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                Currency = Currency.Eur,
                Customer = new Party
                {
                    Name = "Grace Hopper",
                    TaxId = "US987654321",
                    Place = new Place { City = "Arlington", Country = "United States" },
                },
                Cancelled = true,
            };

            invoice.Lines.Add(new InvoiceLine { Description = "Compiler, annual licence", Quantity = 1, UnitPrice = 300.00m, TaxRate = 0.10m });

            return invoice;
        }
    }
}
