using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    /// <summary>
    /// A mapper that is not the one the library builds, so the fallback in
    /// <c>MapFast</c> is exercised rather than assumed. A decorator like this is the realistic
    /// case: logging, timing, or a scope of its own around a real mapper.
    /// </summary>
    public sealed class CountingMapper : IMapper
    {
        private readonly IMapper inner;

        public CountingMapper(IMapper inner) => this.inner = inner;

        public int Calls { get; private set; }

        public TDestination Map<TDestination>(object? source)
        {
            Calls++;
            return inner.Map<TDestination>(source);
        }

        public TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions> options)
        {
            Calls++;
            return inner.Map<TDestination>(source, options);
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            Calls++;
            return inner.Map<TSource, TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions> options)
        {
            Calls++;
            return inner.Map<TSource, TDestination>(source, options);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            Calls++;
            return inner.Map(source, destination);
        }

        public TDestination Map<TSource, TDestination>(
            TSource source,
            TDestination destination,
            Action<IMappingOperationOptions> options)
        {
            Calls++;
            return inner.Map(source, destination, options);
        }

        public object? Map(object? source, Type sourceType, Type destinationType)
        {
            Calls++;
            return inner.Map(source, sourceType, destinationType);
        }

        public object? Map(
            object? source,
            Type sourceType,
            Type destinationType,
            Action<IMappingOperationOptions> options)
        {
            Calls++;
            return inner.Map(source, sourceType, destinationType, options);
        }

        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source) =>
            inner.ProjectTo<TDestination>(source);
    }

    /// <summary>
    /// <c>MapFast</c> exists only to take a different route to the same answer, so what these check
    /// is that the answer really is the same, including for a mapper it does not recognise.
    /// </summary>
    public sealed class MapFastTests
    {
        private static IMapper Readings() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Reading, ReadingDto>()
               .ForMember(d => d.Ratio, o => o.MapFrom(s => s.Value))
               .ForMember(d => d.Label, o => o.Ignore())).CreateMapper();

        [Fact]
        public void It_gives_what_Map_gives()
        {
            IMapper mapper = Readings();
            var source = new Reading { Value = 7, Divisor = 1 };

            ReadingDto through = mapper.Map<Reading, ReadingDto>(source);
            ReadingDto fast = mapper.MapFast<Reading, ReadingDto>(source);

            fast.Ratio.ShouldBe(through.Ratio);
            fast.Ratio.ShouldBe(7);
        }

        [Fact]
        public void It_populates_a_destination_the_caller_supplied()
        {
            IMapper mapper = Readings();
            var existing = new ReadingDto();

            ReadingDto result = mapper.MapFast(new Reading { Value = 3 }, existing);

            result.ShouldBeSameAs(existing);
            result.Ratio.ShouldBe(3);
        }

        [Fact]
        public void A_null_source_behaves_the_same_as_it_does_through_Map()
        {
            IMapper mapper = Readings();

            mapper.MapFast<Reading, ReadingDto>(null!).ShouldBe(mapper.Map<Reading, ReadingDto>(null!));
        }

        [Fact]
        public void A_failure_is_reported_the_same_way()
        {
            IMapper mapper = new MapperConfiguration(cfg =>
                cfg.CreateMap<Reading, ReadingDto>()
                   .ForMember(d => d.Ratio, o => o.MapFrom(s => s.Value / s.Divisor))
                   .ForMember(d => d.Label, o => o.Ignore())).CreateMapper();

            MappingException error = Should.Throw<MappingException>(
                () => mapper.MapFast<Reading, ReadingDto>(new Reading { Value = 1, Divisor = 0 }));

            error.MemberPath.ShouldBe("Ratio");
        }

        /// <remarks>
        /// The whole point of the shortcut is that it recognises the mapper the library builds. A
        /// mapper it does not recognise has to keep working, through the interface, which is what
        /// this covers.
        /// </remarks>
        [Fact]
        public void A_mapper_it_does_not_recognise_still_works()
        {
            var counting = new CountingMapper(Readings());

            ReadingDto dto = counting.MapFast<Reading, ReadingDto>(new Reading { Value = 5 });

            dto.Ratio.ShouldBe(5);
            counting.Calls.ShouldBe(1);
        }

        [Fact]
        public void A_mapper_it_does_not_recognise_still_works_onto_a_destination()
        {
            var counting = new CountingMapper(Readings());
            var existing = new ReadingDto();

            counting.MapFast(new Reading { Value = 5 }, existing).ShouldBeSameAs(existing);
            counting.Calls.ShouldBe(1);
        }

        [Fact]
        public void A_null_mapper_is_rejected()
        {
            Should.Throw<ArgumentNullException>(
                () => ((IMapper)null!).MapFast<Reading, ReadingDto>(new Reading()));
        }
    }
}
