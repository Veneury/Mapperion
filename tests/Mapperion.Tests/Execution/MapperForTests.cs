using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    /// <summary>
    /// <c>MapperFor</c> settles everything that does not depend on the object being mapped and
    /// hands back the rest as a function. These check that "the rest" really is everything else:
    /// the function has to mean exactly what <c>Map</c> means, including in the places where
    /// settling something early would quietly be wrong.
    /// </summary>
    public sealed class MapperForTests
    {
        private static IMapper Mapper() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Bag, BagDto>()).CreateMapper();

        [Fact]
        public void The_function_maps_what_Map_maps()
        {
            IMapper mapper = Mapper();
            var source = new Bag { Label = "one", Count = 3 };

            BagDto direct = mapper.Map<Bag, BagDto>(source);
            BagDto bound = mapper.MapperFor<Bag, BagDto>()(source);

            bound.Label.ShouldBe(direct.Label);
            bound.Count.ShouldBe(direct.Count);
        }

        [Fact]
        public void One_function_maps_many_objects()
        {
            Func<Bag, BagDto> toDto = Mapper().MapperFor<Bag, BagDto>();

            List<BagDto> mapped = Enumerable.Range(0, 5)
                .Select(i => toDto(new Bag { Label = "b" + i, Count = i }))
                .ToList();

            mapped.Count.ShouldBe(5);
            mapped[4].Label.ShouldBe("b4");
            mapped[4].Count.ShouldBe(4);
        }

        [Fact]
        public void Each_call_builds_its_own_destination()
        {
            Func<Bag, BagDto> toDto = Mapper().MapperFor<Bag, BagDto>();

            BagDto first = toDto(new Bag { Label = "one" });
            BagDto second = toDto(new Bag { Label = "two" });

            ReferenceEquals(first, second).ShouldBeFalse();
            first.Label.ShouldBe("one");
        }

        [Fact]
        public void A_null_source_comes_back_as_nothing()
        {
            Mapper().MapperFor<Bag, BagDto>()(null!).ShouldBeNull();
        }

        [Fact]
        public void A_failure_still_names_the_member()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Bag, BagDto>()
                   .ForMember(d => d.Count, o => o.MapFrom(s => Boom(s))));

            MappingException error = Should.Throw<MappingException>(
                () => configuration.CreateMapper().MapperFor<Bag, BagDto>()(new Bag()));

            error.ToString().ShouldContain("Count");
        }

        /// <remarks>
        /// The one that would have been a quiet disaster. When a configuration reads per-operation
        /// state, every call is its own operation; settling the context alongside the plan would
        /// have handed two callers the same bag, and two requests in a web application would have
        /// seen each other's values.
        /// </remarks>
        [Fact]
        public void Two_calls_through_one_function_do_not_share_per_operation_state()
        {
            var configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Audited, AuditedDto>()
                   .ForMember(d => d.Actor, o => o.MapFrom<ActorResolver>()));

            Func<Audited, AuditedDto> toDto = configuration.CreateMapper()
                .MapperFor<Audited, AuditedDto>();

            // Nobody put an actor in either call, so neither may see one left by the other.
            toDto(new Audited { Text = "one" }).Actor.ShouldBe("anonymous");
            toDto(new Audited { Text = "two" }).Actor.ShouldBe("anonymous");
        }

        /// <remarks>
        /// Anything that is not the mapper this library builds keeps its own behaviour, the same
        /// way <c>MapFast</c> does: a decorator that counts calls has to go on counting them. The
        /// decorator is the one the <c>MapFast</c> tests already use, because it is the same
        /// question asked of a second door.
        /// </remarks>
        [Fact]
        public void Another_implementation_is_still_the_one_that_maps()
        {
            var counting = new CountingMapper(Mapper());

            Func<Bag, BagDto> toDto = counting.MapperFor<Bag, BagDto>();

            toDto(new Bag { Label = "one" }).Label.ShouldBe("one");
            toDto(new Bag { Label = "two" }).Label.ShouldBe("two");

            counting.Calls.ShouldBe(2);
        }

        [Fact]
        public void A_null_mapper_is_refused()
        {
            Should.Throw<ArgumentNullException>(() => ((IMapper)null!).MapperFor<Bag, BagDto>());
        }

        private static int Boom(Bag bag) => throw new InvalidOperationException("no");
    }

    public sealed class Bag
    {
        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public sealed class BagDto
    {
        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}
