using System.Collections.Generic;

namespace Mapperion.Tests.Validation
{
    public sealed class Line
    {
        public decimal Price { get; set; }
    }

    public sealed class LineDto
    {
        public decimal Price { get; set; }
    }

    public sealed class Buyer
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class BuyerDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Sale
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        public Buyer Buyer { get; set; } = new Buyer();

        public List<Line> Lines { get; set; } = new List<Line>();
    }

    public sealed class SaleDto
    {
        public long Id { get; set; }

        public BuyerDto Buyer { get; set; } = new BuyerDto();

        public List<LineDto> Lines { get; set; } = new List<LineDto>();

        public string Reference { get; set; } = string.Empty;
    }

    public sealed class SaleSummaryDto
    {
        public int Id { get; set; }
    }
}
