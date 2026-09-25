using System;
using System.Collections.Generic;
using System.Linq;
using AgileObjects.AgileMapper;
using AutoMapper.QueryableExtensions;
using BenchmarkDotNet.Attributes;
using Mapster;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mapperion.Benchmarks
{
    public sealed class Writer
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public List<Title> Titles { get; set; } = new List<Title>();
    }

    public sealed class Title
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Pages { get; set; }

        public int WriterId { get; set; }

        public Writer? Writer { get; set; }
    }

    public sealed class TitleRowDto
    {
        public string Name { get; set; } = string.Empty;

        public int Pages { get; set; }

        public string WriterName { get; set; } = string.Empty;

        public string WriterCountry { get; set; } = string.Empty;
    }

    public sealed class CatalogContext : DbContext
    {
        public CatalogContext(DbContextOptions<CatalogContext> options)
            : base(options)
        {
        }

        public DbSet<Writer> Writers => Set<Writer>();

        public DbSet<Title> Titles => Set<Title>();
    }

    /// <summary>
    /// B09: a thousand rows read out of SQLite through each library's projection, with one join.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read this one for what it is. Most of the time belongs to Entity Framework and to SQLite,
    /// which are the same for everybody, so the numbers sit much closer together than in the
    /// in-memory scenarios and a few per cent between two entrants means nothing.
    /// </para>
    /// <para>
    /// What it does catch is the thing worth catching. A projection that cannot be translated
    /// falls back to pulling the rows and finishing in the client, and that does not show up as a
    /// few per cent: it shows up as a different order of magnitude, alongside the whole entity
    /// being read instead of the four columns asked for. Writing the same query by hand is the
    /// floor, so the distance to it says whether the projection is still a query.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    public class ProjectionBenchmarks : IDisposable
    {
        private const int Rows = 1000;

        private SqliteConnection connection = null!;
        private CatalogContext context = null!;
        private MapperConfiguration mapperion = null!;
        private AutoMapper.IConfigurationProvider automapper = null!;
        private TypeAdapterConfig mapster = null!;

        [GlobalSetup]
        public void Setup()
        {
            connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            DbContextOptions<CatalogContext> options = new DbContextOptionsBuilder<CatalogContext>()
                .UseSqlite(connection)
                .Options;

            using (var seed = new CatalogContext(options))
            {
                seed.Database.EnsureCreated();

                var writer = new Writer { Name = "Ada", Country = "UK" };

                for (int i = 0; i < Rows; i++)
                {
                    writer.Titles.Add(new Title { Name = "T" + i, Pages = i });
                }

                seed.Writers.Add(writer);
                seed.SaveChanges();
            }

            context = new CatalogContext(options);

            mapperion = new MapperConfiguration(cfg => cfg.CreateMap<Title, TitleRowDto>());
            mapperion.AssertIsValid();

            automapper = new AutoMapper.MapperConfiguration(cfg => cfg.CreateMap<Title, TitleRowDto>());

            mapster = new TypeAdapterConfig();
            mapster.Compile();

            Manual();
            Mapperion_Runtime();
            AutoMapper_();
            Mapster_();
            AgileMapper_();
        }

        [GlobalCleanup]
        public void Cleanup() => Dispose();

        /// <summary>Closing the connection is what drops the in-memory database.</summary>
        public void Dispose()
        {
            context?.Dispose();
            connection?.Dispose();
            GC.SuppressFinalize(this);
        }

        [Benchmark(Baseline = true)]
        public List<TitleRowDto> Manual() => context.Titles
            .AsNoTracking()
            .Select(t => new TitleRowDto
            {
                Name = t.Name,
                Pages = t.Pages,
                WriterName = t.Writer!.Name,
                WriterCountry = t.Writer!.Country,
            })
            .ToList();

        [Benchmark]
        public List<TitleRowDto> Mapperion_Runtime() => context.Titles
            .AsNoTracking()
            .ProjectTo<TitleRowDto>(mapperion)
            .ToList();

        [Benchmark]
        public List<TitleRowDto> AutoMapper_() => context.Titles
            .AsNoTracking()
            .ProjectTo<TitleRowDto>(automapper)
            .ToList();

        [Benchmark]
        public List<TitleRowDto> Mapster_() => context.Titles
            .AsNoTracking()
            .ProjectToType<TitleRowDto>(mapster)
            .ToList();

        [Benchmark]
        public List<TitleRowDto> AgileMapper_() => context.Titles
            .AsNoTracking()
            .Project().To<TitleRowDto>()
            .ToList();
    }
}
