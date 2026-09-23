using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Article
    {
        public string Title { get; set; } = string.Empty;

        public int Words { get; set; }
    }

    public sealed class ArticleDto
    {
        public string Title { get; set; } = string.Empty;

        public int Words { get; set; }

        public string Summary { get; set; } = string.Empty;
    }

    public sealed class StampSummary : IMappingAction<Article, ArticleDto>
    {
        internal static int Instances;

        public StampSummary()
        {
            Instances++;
        }

        public void Process(Article source, ArticleDto destination, ResolutionContext context)
        {
            destination.Summary = source.Title + " (" + source.Words + " words)";
        }
    }

    public sealed class MapActionTests
    {
        private static Article SampleArticle() => new Article { Title = "Mapping", Words = 900 };

        [Fact]
        public void An_after_step_sees_the_assigned_members()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap((source, destination) => destination.Summary = destination.Title + "!"));

            config.CreateMapper().Map<Article, ArticleDto>(SampleArticle()).Summary.ShouldBe("Mapping!");
        }

        [Fact]
        public void A_before_step_runs_while_the_members_are_still_unassigned()
        {
            string? titleSeenBefore = null;

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .BeforeMap((source, destination) => titleSeenBefore = destination.Title));

            ArticleDto dto = config.CreateMapper().Map<Article, ArticleDto>(SampleArticle());

            titleSeenBefore.ShouldBe(string.Empty);
            dto.Title.ShouldBe("Mapping");
        }

        [Fact]
        public void Steps_run_in_the_order_they_were_declared()
        {
            var log = new List<string>();

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .BeforeMap((s, d) => log.Add("before 1"))
                   .BeforeMap((s, d) => log.Add("before 2"))
                   .AfterMap((s, d) => log.Add("after 1"))
                   .AfterMap((s, d) => log.Add("after 2")));

            config.CreateMapper().Map<Article, ArticleDto>(SampleArticle());

            log.ShouldBe(new[] { "before 1", "before 2", "after 1", "after 2" });
        }

        [Fact]
        public void A_before_step_runs_before_an_after_step()
        {
            var log = new List<string>();

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap((s, d) => log.Add("after"))
                   .BeforeMap((s, d) => log.Add("before")));

            config.CreateMapper().Map<Article, ArticleDto>(SampleArticle());

            log.ShouldBe(new[] { "before", "after" });
        }

        [Fact]
        public void A_step_can_reach_the_running_mapper()
        {
            IMapper? seen = null;

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap((source, destination, context) => seen = context.Mapper));

            IMapper mapper = config.CreateMapper();
            mapper.Map<Article, ArticleDto>(SampleArticle());

            seen.ShouldBeSameAs(mapper);
        }

        [Fact]
        public void A_step_with_its_own_type_runs()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap<StampSummary>());

            config.CreateMapper()
                .Map<Article, ArticleDto>(SampleArticle())
                .Summary.ShouldBe("Mapping (900 words)");
        }

        [Fact]
        public void A_step_with_its_own_type_is_created_once_and_reused()
        {
            StampSummary.Instances = 0;

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap<StampSummary>());

            IMapper mapper = config.CreateMapper();

            for (int i = 0; i < 5; i++)
            {
                mapper.Map<Article, ArticleDto>(SampleArticle());
            }

            StampSummary.Instances.ShouldBe(1);
        }

        [Fact]
        public void Steps_run_when_mapping_into_an_existing_destination()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>()
                   .ForMember(d => d.Summary, o => o.Ignore())
                   .AfterMap((s, d) => d.Summary = "done"));

            var destination = new ArticleDto();
            config.CreateMapper().Map(SampleArticle(), destination);

            destination.Summary.ShouldBe("done");
        }

        [Fact]
        public void Null_steps_are_rejected()
        {
            Should.Throw<System.ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Article, ArticleDto>().AfterMap((System.Action<Article, ArticleDto>)null!)));
        }
    }
}
