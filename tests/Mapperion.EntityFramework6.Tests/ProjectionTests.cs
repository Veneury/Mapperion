using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.EntityFramework6.Tests
{
    public class Author
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public virtual ICollection<Book> Books { get; set; } = new List<Book>();
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

    public class Book
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public Binding Binding { get; set; }

        public int Year { get; set; }

        public decimal? Price { get; set; }

        public int AuthorId { get; set; }

        public virtual Author Author { get; set; } = new Author();
    }

    public sealed class BookDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Year { get; set; }

        public decimal? Price { get; set; }

        public string AuthorName { get; set; } = string.Empty;

        public string AuthorCountry { get; set; } = string.Empty;
    }

    public sealed class BookBindingDto
    {
        public string Title { get; set; } = string.Empty;

        public BindingDto Binding { get; set; }
    }

    public sealed class AuthorDto
    {
        public string Name { get; set; } = string.Empty;

        public List<BookTitleDto> Books { get; set; } = new List<BookTitleDto>();
    }

    public sealed class BookTitleDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public sealed class LibraryContext : DbContext
    {
        static LibraryContext()
        {
            Database.SetInitializer<LibraryContext>(null);
        }

        public LibraryContext()
            : base("Server=.;Database=Mapperion;Integrated Security=true")
        {
        }

        public DbSet<Author> Authors { get; set; } = null!;

        public DbSet<Book> Books { get; set; } = null!;
    }

    /// <summary>
    /// Checks that what <c>ProjectTo</c> emits is something Entity Framework 6 can turn into SQL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No database is involved and none is needed. EF6 builds the SQL from its model, and
    /// <c>ToString()</c> on a query returns it; an expression the provider cannot translate throws
    /// at that point instead. So the SQL text is the assertion, and it is produced without a server
    /// anywhere near the build.
    /// </para>
    /// <para>
    /// EF6 is the reason these exist separately from the EF Core ones. It is an older and stricter
    /// translator, and the point is to find out where it differs rather than to assume it does not.
    /// </para>
    /// </remarks>
    public sealed class ProjectionTests
    {
        private static string Sql<TDestination>(
            Func<LibraryContext, IQueryable> query,
            MapperConfiguration configuration)
        {
            using var context = new LibraryContext();
            return query(context).ProjectTo<TDestination>(configuration).ToString();
        }

        [Fact]
        public void A_flat_projection_becomes_sql()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Book, BookDto>()
                   .ForMember(d => d.AuthorName, o => o.Ignore())
                   .ForMember(d => d.AuthorCountry, o => o.Ignore()));

            string sql = Sql<BookDto>(c => c.Books, configuration);

            sql.ShouldContain("SELECT");
            sql.ShouldContain("Title");
            sql.ShouldNotContain("Country");
        }

        /// <remarks>
        /// The whole point of projecting: only the columns the destination needs are read, and a
        /// join replaces loading the related entity.
        /// </remarks>
        [Fact]
        public void A_flattened_path_becomes_a_join()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookDto>());

            string sql = Sql<BookDto>(c => c.Books, configuration);

            sql.ShouldContain("JOIN");
            sql.ShouldContain("Name");
            sql.ShouldContain("Country");
        }

        [Fact]
        public void A_collection_becomes_a_correlated_query()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Author, AuthorDto>();
                cfg.CreateMap<Book, BookTitleDto>();
            });

            string sql = Sql<AuthorDto>(c => c.Authors, configuration);

            sql.ShouldContain("SELECT");
            sql.ShouldContain("Title");
        }

        [Fact]
        public void A_nullable_column_survives_the_translation()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Book, BookDto>()
                   .ForMember(d => d.AuthorName, o => o.Ignore())
                   .ForMember(d => d.AuthorCountry, o => o.Ignore()));

            Sql<BookDto>(c => c.Books, configuration).ShouldContain("Price");
        }

        /// <remarks>
        /// Crossing an enum by name is emitted as a chain of conditions, and EF6 is the translator
        /// most likely to refuse one. It does not: this is a CASE like any other.
        /// </remarks>
        [Fact]
        public void An_enum_crossing_by_name_becomes_a_case()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookBindingDto>());

            string sql = Sql<BookBindingDto>(c => c.Books, configuration);

            sql.ShouldContain("CASE");
            sql.ShouldContain("WHEN");
            sql.ShouldNotContain("Year");
        }

        [Fact]
        public void The_projection_composes_with_the_rest_of_the_query()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookDto>());

            using var context = new LibraryContext();

            string sql = context.Books
                .Where(b => b.Year > 2000)
                .OrderBy(b => b.Title)
                .ProjectTo<BookDto>(configuration)
                .Take(10)
                .ToString();

            sql.ShouldContain("WHERE");
            sql.ShouldContain("ORDER BY");
        }
    }
}
