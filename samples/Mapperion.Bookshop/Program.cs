using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mapperion.Bookshop
{
    /// <summary>
    /// A small application that uses Mapperion the way an application does, rather than the way a
    /// test does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The point of it is friction. A test suite reaches for a feature on purpose and is written
    /// by whoever built the feature; this is written the other way round, by deciding what the
    /// bookshop needs and then finding out what the library makes of it. Whatever it turns out to
    /// be awkward about is worth more than another passing test.
    /// </para>
    /// <para>
    /// It checks its own output and exits non-zero when something is wrong, so CI runs it as a
    /// test rather than printing at it.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static int Main()
        {
            using var bookshop = new Bookshop();

            ServiceProvider services = new ServiceCollection()
                .AddMapperion(typeof(Program))
                .AddSingleton(bookshop)
                .BuildServiceProvider();

            // An application finds out at startup, not on the request that happens to need the map
            // nobody declared.
            services.GetRequiredService<MapperConfiguration>().AssertIsValid();

            using IServiceScope scope = services.CreateScope();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

            var checks = new Checks();

            List<OrderSummaryDto> summaries = Summaries(bookshop, scope.ServiceProvider);
            Show(summaries);
            CheckSummaries(checks, summaries);

            OrderDetailDto detail = Detail(bookshop, mapper, "BK-1001");
            Show(detail);
            CheckDetail(checks, detail);

            OrderDetailDto unpaid = Detail(bookshop, mapper, "BK-1003");
            checks.That("an order with no payment maps to none", unpaid.Payment is null);
            checks.That("an order with no lines totals nothing", unpaid.Total == 0m);

            return checks.Report();
        }

        /// <summary>
        /// The list view. The database returns the six columns the destination asked for, and no
        /// entity is ever built.
        /// </summary>
        private static List<OrderSummaryDto> Summaries(Bookshop bookshop, IServiceProvider services)
        {
            using BookshopContext context = bookshop.Open();

            return context.Orders
                .OrderBy(o => o.Reference)
                .ProjectTo<OrderSummaryDto>(services.GetRequiredService<MapperConfiguration>())
                .ToList();
        }

        /// <summary>
        /// The detail view, from an aggregate that is already in memory.
        /// </summary>
        private static OrderDetailDto Detail(Bookshop bookshop, IMapper mapper, string reference)
        {
            using BookshopContext context = bookshop.Open();

            Order order = context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Lines)
                .Include(o => o.Payment)
                .Single(o => o.Reference == reference);

            return mapper.Map<Order, OrderDetailDto>(order);
        }

        private static void CheckSummaries(Checks checks, List<OrderSummaryDto> summaries)
        {
            checks.That("every order is listed", summaries.Count == 3);

            OrderSummaryDto first = summaries[0];

            checks.That("the reference comes across", first.Reference == "BK-1001");
            checks.That("a two-hop flattening finds the customer", first.CustomerFullName == "Ada Lovelace");
            checks.That("a three-hop flattening reaches inside the address", first.CustomerAddressCity == "London");
            checks.That("the count of a collection survives the projection", first.LineCount == 2);

            // Shipped is 2 on the entity and 1 on the contract. By number this would be Placed.
            checks.That("the status crosses by name and not by number", first.Status == OrderState.Shipped);
            checks.That("and it is not what the number would have given", (int)first.Status != (int)OrderStatus.Shipped);
        }

        private static void CheckDetail(Checks checks, OrderDetailDto detail)
        {
            checks.That("the nested customer is its own object", detail.Customer.FullName == "Ada Lovelace");
            checks.That("a member one hop in is flattened onto it", detail.Customer.City == "London");
            checks.That("the lines come across", detail.Lines.Count == 2);
            checks.That("a line is built through its constructor", detail.Lines[0].Title.Length > 0);
            checks.That("the total is what the lines add up to", detail.Total == 47.50m);
            checks.That("the payment is the concrete one", detail.Payment is CardPaymentDto);
            checks.That("and it carries what only that one has", ((CardPaymentDto)detail.Payment!).Last4 == "4242");
            checks.That("as well as what the base has", detail.Payment!.Amount == 47.50m);
        }

        private static void Show(List<OrderSummaryDto> summaries)
        {
            Console.WriteLine("Orders");
            Console.WriteLine("------");

            foreach (OrderSummaryDto summary in summaries)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0}  {1:yyyy-MM-dd}  {2,-9}  {3,-13}  {4,-9}  {5} line(s)",
                    summary.Reference,
                    summary.PlacedAt,
                    summary.Status,
                    summary.CustomerFullName,
                    summary.CustomerAddressCity,
                    summary.LineCount));
            }

            Console.WriteLine();
        }

        private static void Show(OrderDetailDto detail)
        {
            Console.WriteLine("Order " + detail.Reference);
            Console.WriteLine("---------------");
            Console.WriteLine("  " + detail.Customer.FullName + ", " + detail.Customer.City);

            foreach (OrderLineDto line in detail.Lines)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0} x{1} at {2:0.00}",
                    line.Title,
                    line.Quantity,
                    line.UnitPrice));
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  total {0:0.00}", detail.Total));

            string paid = detail.Payment switch
            {
                CardPaymentDto card => "card ending " + card.Last4,
                TransferPaymentDto transfer => "transfer from " + transfer.Iban,
                _ => "not paid",
            };

            Console.WriteLine("  " + paid);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Enough of an assertion library for a sample: it says which claim failed and makes the
    /// process say so too.
    /// </summary>
    internal sealed class Checks
    {
        private readonly List<string> failed = new List<string>();
        private int total;

        internal void That(string claim, bool holds)
        {
            total++;

            if (!holds)
            {
                failed.Add(claim);
            }
        }

        internal int Report()
        {
            if (failed.Count == 0)
            {
                Console.WriteLine(total + " checks, all of them true.");
                return 0;
            }

            Console.Error.WriteLine(failed.Count + " of " + total + " checks did not hold:");

            foreach (string claim in failed)
            {
                Console.Error.WriteLine("  " + claim);
            }

            return 1;
        }
    }
}
