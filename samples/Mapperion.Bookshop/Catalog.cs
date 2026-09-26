using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mapperion.Bookshop
{
    public sealed class BookshopContext : DbContext
    {
        public BookshopContext(DbContextOptions<BookshopContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<OrderLine> Lines => Set<OrderLine>();

        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().OwnsOne(c => c.Address);
            modelBuilder.Entity<Payment>().UseTptMappingStrategy();
            modelBuilder.Entity<CardPayment>();
            modelBuilder.Entity<TransferPayment>();
        }
    }

    /// <summary>
    /// A database with something in it, held open for as long as the process runs.
    /// </summary>
    public sealed class Bookshop : IDisposable
    {
        private readonly SqliteConnection connection;

        public Bookshop()
        {
            connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            Options = new DbContextOptionsBuilder<BookshopContext>().UseSqlite(connection).Options;

            using var context = new BookshopContext(Options);
            context.Database.EnsureCreated();
            Seed(context);
        }

        public DbContextOptions<BookshopContext> Options { get; }

        public BookshopContext Open() => new BookshopContext(Options);

        public void Dispose() => connection.Dispose();

        private static void Seed(BookshopContext context)
        {
            var ada = new Customer
            {
                FullName = "Ada Lovelace",
                Email = "ada@example.org",
                Address = new Address
                {
                    Street = "12 Noel Street",
                    City = "London",
                    PostCode = "W1F 8GQ",
                    Country = "United Kingdom",
                },
            };

            var grace = new Customer
            {
                FullName = "Grace Hopper",
                Email = "grace@example.org",
                Address = new Address
                {
                    Street = "9 Elm Avenue",
                    City = "Arlington",
                    PostCode = "22204",
                    Country = "United States",
                },
            };

            var first = new Order
            {
                Reference = "BK-1001",
                PlacedAt = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc),
                Status = OrderStatus.Shipped,
                Customer = ada,
                Payment = new CardPayment { Amount = 47.50m, Last4 = "4242" },
            };

            first.Lines.Add(new OrderLine { Isbn = "9780262033848", Title = "Introduction to Algorithms", Quantity = 1, UnitPrice = 38.00m });
            first.Lines.Add(new OrderLine { Isbn = "9780132350884", Title = "Clean Code", Quantity = 1, UnitPrice = 9.50m });

            var second = new Order
            {
                Reference = "BK-1002",
                PlacedAt = new DateTime(2026, 3, 15, 16, 5, 0, DateTimeKind.Utc),
                Status = OrderStatus.Placed,
                Customer = grace,
                Payment = new TransferPayment { Amount = 22.00m, Iban = "GB33BUKB20201555555555" },
            };

            second.Lines.Add(new OrderLine { Isbn = "9781491950357", Title = "Building Microservices", Quantity = 2, UnitPrice = 11.00m });

            var third = new Order
            {
                Reference = "BK-1003",
                PlacedAt = new DateTime(2026, 3, 16, 11, 0, 0, DateTimeKind.Utc),
                Status = OrderStatus.Draft,
                Customer = ada,
            };

            context.Orders.AddRange(first, second, third);
            context.SaveChanges();
        }
    }
}
