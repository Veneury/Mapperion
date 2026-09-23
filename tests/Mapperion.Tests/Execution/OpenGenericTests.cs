using System;
using System.Collections.Generic;
using System.Linq;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Page<T>
    {
        public List<T> Items { get; set; } = new List<T>();

        public int Total { get; set; }

        public string Cursor { get; set; } = string.Empty;
    }

    public sealed class PageDto<T>
    {
        public List<T> Items { get; set; } = new List<T>();

        public long Total { get; set; }

        public string Cursor { get; set; } = string.Empty;
    }

    public sealed class Pair<TLeft, TRight>
    {
        public TLeft? Left { get; set; }

        public TRight? Right { get; set; }
    }

    public sealed class PairDto<TLeft, TRight>
    {
        public TLeft? Left { get; set; }

        public TRight? Right { get; set; }
    }

    public sealed class Widget
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class WidgetDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Lopsided<T>
    {
        public T? Value { get; set; }
    }

    public sealed class LopsidedDto
    {
        public object? Value { get; set; }
    }

    public sealed class PageProfile : Profile
    {
        public PageProfile()
        {
            CreateMap(typeof(Page<>), typeof(PageDto<>));
            CreateMap<Widget, WidgetDto>();
        }
    }

    public sealed class OpenGenericTests
    {
        private static Page<Widget> SamplePage() => new Page<Widget>
        {
            Items = { new Widget { Name = "a" }, new Widget { Name = "b" } },
            Total = 2,
            Cursor = "next",
        };

        private static MapperConfiguration Configured() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap(typeof(Page<>), typeof(PageDto<>));
            cfg.CreateMap<Widget, WidgetDto>();
        });

        [Fact]
        public void A_template_closes_on_first_use()
        {
            PageDto<WidgetDto> dto = Configured()
                .CreateMapper()
                .Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage());

            dto.Total.ShouldBe(2L);
            dto.Cursor.ShouldBe("next");
            dto.Items.Count.ShouldBe(2);
            dto.Items[0].Name.ShouldBe("a");
        }

        [Fact]
        public void The_same_template_serves_several_closings()
        {
            IMapper mapper = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap(typeof(Page<>), typeof(PageDto<>));
                cfg.CreateMap<Widget, WidgetDto>();
            }).CreateMapper();

            mapper.Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage()).Items.Count.ShouldBe(2);

            PageDto<string> strings = mapper.Map<Page<string>, PageDto<string>>(
                new Page<string> { Items = { "x" }, Total = 1 });

            strings.Items.ShouldHaveSingleItem().ShouldBe("x");
        }

        [Fact]
        public void A_closed_pair_is_reused_rather_than_rebuilt()
        {
            IMapper mapper = Configured().CreateMapper();

            for (int i = 0; i < 20; i++)
            {
                mapper.Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage()).Total.ShouldBe(2L);
            }
        }

        [Fact]
        public void A_template_with_two_type_arguments_closes()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap(typeof(Pair<,>), typeof(PairDto<,>));
                cfg.CreateMap<Widget, WidgetDto>();
            });

            PairDto<WidgetDto, int> dto = config.CreateMapper()
                .Map<Pair<Widget, int>, PairDto<WidgetDto, int>>(
                    new Pair<Widget, int> { Left = new Widget { Name = "l" }, Right = 7 });

            dto.Left!.Name.ShouldBe("l");
            dto.Right.ShouldBe(7);
        }

        [Fact]
        public void A_template_works_inside_a_profile()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<PageProfile>());

            config.CreateMapper()
                .Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage())
                .Items[1].Name.ShouldBe("b");
        }

        [Fact]
        public void A_closed_map_takes_precedence_over_the_template()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap(typeof(Page<>), typeof(PageDto<>));
                cfg.CreateMap<Page<Widget>, PageDto<WidgetDto>>()
                   .ForMember(d => d.Cursor, o => o.MapFrom(s => "closed"));
                cfg.CreateMap<Widget, WidgetDto>();
            });

            config.CreateMapper()
                .Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage())
                .Cursor.ShouldBe("closed");
        }

        [Fact]
        public void A_template_member_can_be_ignored_by_name()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap(typeof(Page<>), typeof(PageDto<>)).IgnoreMember(nameof(PageDto<int>.Cursor));
                cfg.CreateMap<Widget, WidgetDto>();
            });

            config.CreateMapper()
                .Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage())
                .Cursor.ShouldBe(string.Empty);
        }

        [Fact]
        public void Ignoring_a_member_that_does_not_exist_is_reported()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg =>
                    cfg.CreateMap(typeof(Page<>), typeof(PageDto<>)).IgnoreMember("Nope")));

            error.Message.ShouldContain("'Nope'");
        }

        [Fact]
        public void A_template_is_left_out_of_member_validation()
        {
            Should.NotThrow(Configured().AssertIsValid);
        }

        [Fact]
        public void Mismatched_arity_is_reported()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap(typeof(Pair<,>), typeof(Page<>)));

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("type argument"));
        }

        [Fact]
        public void Mixing_an_open_type_with_a_closed_one_is_reported()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg => cfg.CreateMap(typeof(Lopsided<>), typeof(LopsidedDto))));

            error.Message.ShouldContain("mixes an open generic type with a closed one");
        }

        [Fact]
        public void A_pair_with_no_template_is_still_reported_as_missing()
        {
            IMapper mapper = new MapperConfiguration(cfg => cfg.CreateMap<Widget, WidgetDto>()).CreateMapper();

            Should.Throw<MappingException>(
                () => mapper.Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage()));
        }

        [Fact]
        public void A_closed_template_can_be_projected()
        {
            List<PageDto<WidgetDto>> pages = new[] { SamplePage() }
                .AsQueryable()
                .ProjectTo<PageDto<WidgetDto>>(Configured())
                .ToList();

            pages[0].Items.Count.ShouldBe(2);
            pages[0].Total.ShouldBe(2L);
        }

        [Fact]
        public void Two_threads_closing_the_same_template_agree()
        {
            IMapper mapper = Configured().CreateMapper();

            PageDto<WidgetDto>[] results = Enumerable.Range(0, 32)
                .AsParallel()
                .Select(_ => mapper.Map<Page<Widget>, PageDto<WidgetDto>>(SamplePage()))
                .ToArray();

            results.Length.ShouldBe(32);
            results.ShouldAllBe(r => r.Items.Count == 2);
        }

        [Fact]
        public void Null_types_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap(null!, typeof(PageDto<>))));

            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap(typeof(Page<>), null!)));
        }
    }
}
