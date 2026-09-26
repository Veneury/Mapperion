using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Mapperion.EntityFrameworkCore.Tests
{
    public sealed class ProjectToTests : IClassFixture<LibraryFixture>
    {
        private readonly LibraryFixture fixture;

        public ProjectToTests(LibraryFixture fixture)
        {
            this.fixture = fixture;
        }

        private static MapperConfiguration BookConfiguration() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Book, BookDto>());

        [Fact]
        public void A_flattened_member_is_translated_to_sql()
        {
            using LibraryContext context = fixture.CreateContext();

            IQueryable<BookDto> query = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookDto>(BookConfiguration());

            string sql = query.ToQueryString();

            sql.ShouldContain("SELECT");
            sql.ShouldContain("JOIN");
        }

        [Fact]
        public void Only_the_columns_the_destination_needs_are_selected()
        {
            using LibraryContext context = fixture.CreateContext();

            string sql = context.Books.ProjectTo<BookDto>(BookConfiguration()).ToQueryString();

            sql.ShouldNotContain("Pages");
        }

        [Fact]
        public void The_projection_returns_the_expected_rows()
        {
            using LibraryContext context = fixture.CreateContext();

            List<BookDto> books = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookDto>(BookConfiguration())
                .ToList();

            books.Count.ShouldBe(3);
            books[0].Title.ShouldBe("Compilers");
            books[0].AuthorName.ShouldBe("Grace");
            books[0].AuthorCountry.ShouldBe("US");
        }

        [Fact]
        public void Filtering_and_paging_still_happen_in_the_database()
        {
            using LibraryContext context = fixture.CreateContext();

            IQueryable<BookDto> query = context.Books
                .ProjectTo<BookDto>(BookConfiguration())
                .Where(b => b.AuthorName == "Ada")
                .OrderBy(b => b.Title)
                .Take(1);

            query.ToQueryString().ShouldContain("LIMIT");
            query.Single().Title.ShouldBe("Engines");
        }

        [Fact]
        public void A_collection_is_projected_as_a_nested_select()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Author, AuthorDto>();
                cfg.CreateMap<Book, BookSummaryDto>();
            });

            using LibraryContext context = fixture.CreateContext();

            List<AuthorDto> authors = context.Authors
                .OrderBy(a => a.Name)
                .ProjectTo<AuthorDto>(configuration)
                .ToList();

            authors.Count.ShouldBe(2);
            authors[0].Name.ShouldBe("Ada");
            authors[0].Books.Count.ShouldBe(2);
            authors[0].Books.Select(b => b.Title).OrderBy(t => t).ShouldBe(new[] { "Engines", "Notes" });
        }

        [Fact]
        public void A_record_destination_is_built_through_its_constructor()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookRecordDto>());

            using LibraryContext context = fixture.CreateContext();

            List<BookRecordDto> books = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookRecordDto>(configuration)
                .ToList();

            books[0].Title.ShouldBe("Compilers");
            books[0].Pages.ShouldBe(500L);
        }

        [Fact]
        public void The_mapper_overload_projects_the_same_way()
        {
            using LibraryContext context = fixture.CreateContext();

            IMapper mapper = BookConfiguration().CreateMapper();

            mapper.ProjectTo<BookDto>(context.Books.OrderBy(b => b.Title)).First().Title.ShouldBe("Compilers");
        }

        [Fact]
        public void A_map_with_an_after_step_is_reported_instead_of_silently_skipped()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Book, BookDto>()
                   .AfterMap((source, destination) => destination.Title = destination.Title.ToUpperInvariant()));

            using LibraryContext context = fixture.CreateContext();

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => context.Books.ProjectTo<BookDto>(configuration).ToList());

            error.Message.ShouldContain("before or after step");
        }

        [Fact]
        public void A_map_with_a_value_resolver_is_reported()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Book, BookDto>()
                   .ForMember(d => d.AuthorName, o => o.MapFrom<TitleResolver>()));

            using LibraryContext context = fixture.CreateContext();

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => context.Books.ProjectTo<BookDto>(configuration).ToList());

            error.Message.ShouldContain("query provider cannot run");
        }

        [Fact]
        public void An_expression_member_is_translated_too()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Book, BookDto>()
                   .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author!.Name + " of " + s.Author.Country))
                   .ForMember(d => d.AuthorCountry, o => o.Ignore()));

            using LibraryContext context = fixture.CreateContext();

            List<BookDto> books = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookDto>(configuration)
                .ToList();

            books[0].AuthorName.ShouldBe("Grace of US");
        }

        /// <remarks>
        /// A projection used to carry enums across by number while the mapping engine carried them
        /// across by name, so a list view and a detail view of the same row could disagree about
        /// what a binding was called. The pilot application is what noticed.
        /// </remarks>
        [Fact]
        public void An_enum_crosses_by_name_in_a_projection_too()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookBindingDto>());

            using LibraryContext context = fixture.CreateContext();

            List<BookBindingDto> books = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookBindingDto>(configuration)
                .ToList();

            // Compilers is Paperback, which is 0 on the entity and 2 on the contract.
            books[0].Title.ShouldBe("Compilers");
            books[0].Binding.ShouldBe(BindingDto.Paperback);
            ((int)books[0].Binding).ShouldBe(2);
        }

        /// <remarks>
        /// And it has to be the database doing it. Crossing by name in the client would mean the
        /// rows were read first, which is the one thing a projection exists to avoid.
        /// </remarks>
        [Fact]
        public void The_database_is_what_crosses_it()
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Book, BookBindingDto>());

            using LibraryContext context = fixture.CreateContext();

            string sql = context.Books.ProjectTo<BookBindingDto>(configuration).ToQueryString();

            sql.ShouldContain("CASE");
            sql.ShouldNotContain("Pages");
        }

        [Fact]
        public void By_value_still_means_by_value()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.EnumMapping = Mapperion.Model.EnumMappingPolicy.ByValue;
                cfg.CreateMap<Book, BookBindingDto>();
            });

            using LibraryContext context = fixture.CreateContext();

            List<BookBindingDto> books = context.Books
                .OrderBy(b => b.Title)
                .ProjectTo<BookBindingDto>(configuration)
                .ToList();

            ((int)books[0].Binding).ShouldBe(0);
        }
    }

    public sealed class TitleResolver : IValueResolver<Book, BookDto, string>
    {
        public string Resolve(Book source, BookDto destination, string destinationMember, ResolutionContext context)
        {
            return source.Title;
        }
    }
}
