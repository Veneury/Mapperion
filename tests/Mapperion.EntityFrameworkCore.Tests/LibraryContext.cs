using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mapperion.EntityFrameworkCore.Tests
{
    public sealed class Author
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public List<Book> Books { get; set; } = new List<Book>();
    }

    public enum Binding
    {
        Paperback = 0,
        Hardback = 1,
        Digital = 2,
    }

    /// <summary>The same three names with the numbers turned round.</summary>
    public enum BindingDto
    {
        Digital = 0,
        Hardback = 1,
        Paperback = 2,
    }

    public sealed class Book
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Pages { get; set; }

        public Binding Binding { get; set; }

        public int AuthorId { get; set; }

        public Author? Author { get; set; }
    }

    public sealed class BookDto
    {
        public string Title { get; set; } = string.Empty;

        public string AuthorName { get; set; } = string.Empty;

        public string AuthorCountry { get; set; } = string.Empty;
    }

    public sealed class BookBindingDto
    {
        public string Title { get; set; } = string.Empty;

        public BindingDto Binding { get; set; }
    }

    public sealed class BookSummaryDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public sealed class AuthorDto
    {
        public string Name { get; set; } = string.Empty;

        public List<BookSummaryDto> Books { get; set; } = new List<BookSummaryDto>();
    }

    public sealed record BookRecordDto(string Title, long Pages);

    public sealed class LibraryContext : DbContext
    {
        public LibraryContext(DbContextOptions<LibraryContext> options)
            : base(options)
        {
        }

        public DbSet<Author> Authors => Set<Author>();

        public DbSet<Book> Books => Set<Book>();
    }

    public sealed class LibraryFixture : IDisposable
    {
        private readonly SqliteConnection connection;

        public LibraryFixture()
        {
            connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            DbContextOptions<LibraryContext> options = new DbContextOptionsBuilder<LibraryContext>()
                .UseSqlite(connection)
                .Options;

            Options = options;

            using var context = new LibraryContext(options);
            context.Database.EnsureCreated();

            var ada = new Author { Name = "Ada", Country = "UK" };
            ada.Books.Add(new Book { Title = "Notes", Pages = 120, Binding = Binding.Hardback });
            ada.Books.Add(new Book { Title = "Engines", Pages = 340, Binding = Binding.Digital });

            var grace = new Author { Name = "Grace", Country = "US" };
            grace.Books.Add(new Book { Title = "Compilers", Pages = 500, Binding = Binding.Paperback });

            context.Authors.AddRange(ada, grace);
            context.SaveChanges();
        }

        public DbContextOptions<LibraryContext> Options { get; }

        public LibraryContext CreateContext() => new LibraryContext(Options);

        public void Dispose() => connection.Dispose();
    }
}
