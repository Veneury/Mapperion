using System;
using System.Collections.Generic;

namespace Mapperion.Aot
{
    /// <summary>
    /// Proves that the generated mappers work in an application published ahead of time. It checks
    /// its own output and exits non-zero when anything is wrong, so publishing it and running it is
    /// a test rather than something a person has to read.
    /// </summary>
    /// <remarks>
    /// The run-time engine is deliberately absent. It compiles expression trees, which an AOT
    /// application cannot do, and it says so through <c>RequiresDynamicCode</c>. Referencing it
    /// here would be the wrong thing to prove.
    /// </remarks>
    public static class Program
    {
        public static int Main()
        {
            var mapper = new OrderMapper();
            var failures = new List<string>();

            var order = new Order
            {
                Id = 42,
                Reference = "ORD-42",
                PlacedOn = new DateTime(2026, 9, 23, 10, 30, 0, DateTimeKind.Utc),
                Discount = 12.5m,
                Status = Status.Shipped,
                Customer = new Customer
                {
                    Name = "Ana",
                    Address = new Address { City = "Madrid" },
                },
                Lines =
                {
                    new Line { Code = "A", Quantity = 2, Price = 10m },
                    new Line { Code = "B", Quantity = 1, Price = 5m },
                },
            };

            OrderDto dto = mapper.ToDto(order);

            Check(failures, "Id", dto.Id, 42);
            Check(failures, "Reference", dto.Reference, "ORD-42");
            Check(failures, "PlacedOn", dto.PlacedOn, order.PlacedOn);
            Check(failures, "Discount", dto.Discount, 12.5m);
            Check(failures, "Status", dto.Status, StatusDto.Shipped);
            Check(failures, "Customer.Name", dto.Customer.Name, "Ana");
            Check(failures, "CustomerCity", dto.CustomerCity, "Madrid");
            Check(failures, "Lines.Count", dto.Lines.Count, 2);
            Check(failures, "Lines[0].Code", dto.Lines[0].Code, "A");
            Check(failures, "Lines[1].Price", dto.Lines[1].Price, 5m);

            List<LineDto> lines = mapper.ToDtos(order.Lines);
            Check(failures, "ToDtos.Count", lines.Count, 2);
            Check(failures, "ToDtos[0].Quantity", lines[0].Quantity, 2);

            LineSummary summary = mapper.ToSummary(order.Lines[0]);
            Check(failures, "Summary.Code", summary.Code, "A");
            Check(failures, "Summary.Price", summary.Price, 10m);

            OrderDto empty = mapper.ToDto(new Order());
            Check(failures, "empty.Lines.Count", empty.Lines.Count, 0);
            Check(failures, "empty.Discount", empty.Discount, null);

            if (failures.Count == 0)
            {
                Console.WriteLine("Mapperion ahead-of-time sample: every check passed.");
                return 0;
            }

            Console.Error.WriteLine("Mapperion ahead-of-time sample: " + failures.Count + " check(s) failed.");

            foreach (string failure in failures)
            {
                Console.Error.WriteLine("  " + failure);
            }

            return 1;
        }

        private static void Check<T>(List<string> failures, string what, T actual, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                failures.Add(what + ": expected '" + expected + "' but was '" + actual + "'.");
            }
        }
    }
}
