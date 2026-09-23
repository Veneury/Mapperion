using System;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Audited
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class AuditedDto
    {
        public string Text { get; set; } = string.Empty;

        public string Actor { get; set; } = string.Empty;
    }

    public sealed class ActorResolver : IValueResolver<Audited, AuditedDto, string>
    {
        public string Resolve(Audited source, AuditedDto destination, string member, ResolutionContext context)
        {
            return context.Items.TryGetValue("actor", out object? actor)
                ? (string)actor!
                : "anonymous";
        }
    }

    public sealed class TrailResolver : IValueResolver<Audited, AuditedDto, string>
    {
        public string Resolve(Audited source, AuditedDto destination, string member, ResolutionContext context)
        {
            context.Items["seen"] = source.Text;
            return source.Text;
        }
    }

    public sealed class OperationOptionsTests
    {
        private static MapperConfiguration Audits() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Audited, AuditedDto>()
               .ForMember(d => d.Actor, o => o.MapFrom<ActorResolver>()));

        [Fact]
        public void A_resolver_reads_what_the_caller_passed()
        {
            AuditedDto dto = Audits().CreateMapper().Map<Audited, AuditedDto>(
                new Audited { Text = "t" },
                options => options.Items["actor"] = "ana");

            dto.Actor.ShouldBe("ana");
        }

        [Fact]
        public void A_resolver_sees_an_empty_bag_when_the_caller_passed_nothing()
        {
            AuditedDto dto = Audits().CreateMapper().Map<Audited, AuditedDto>(new Audited { Text = "t" });

            dto.Actor.ShouldBe("anonymous");
        }

        [Fact]
        public void The_bag_belongs_to_one_operation()
        {
            IMapper mapper = new MapperConfiguration(cfg =>
                cfg.CreateMap<Audited, AuditedDto>()
                   .ForMember(d => d.Actor, o => o.MapFrom<TrailResolver>())).CreateMapper();

            mapper.Map<Audited, AuditedDto>(new Audited { Text = "first" });

            string? leaked = null;

            mapper.Map<Audited, AuditedDto>(
                new Audited { Text = "second" },
                options => leaked = options.Items.TryGetValue("seen", out object? seen) ? (string?)seen : null);

            leaked.ShouldBeNull();
        }

        [Fact]
        public void The_items_reach_a_before_step_too()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Audited, AuditedDto>()
                   .ForMember(d => d.Actor, o => o.Ignore())
                   .BeforeMap((source, destination, context) =>
                       destination.Actor = (string)context.Items["actor"]!));

            AuditedDto dto = config.CreateMapper().Map<Audited, AuditedDto>(
                new Audited { Text = "t" },
                options => options.Items["actor"] = "leo");

            dto.Actor.ShouldBe("leo");
        }

        [Fact]
        public void Every_overload_carries_the_items()
        {
            IMapper mapper = Audits().CreateMapper();
            var source = new Audited { Text = "t" };

            mapper.Map<AuditedDto>(source, o => o.Items["actor"] = "one").Actor.ShouldBe("one");
            mapper.Map<Audited, AuditedDto>(source, o => o.Items["actor"] = "two").Actor.ShouldBe("two");

            mapper.Map(source, new AuditedDto(), o => o.Items["actor"] = "three").Actor.ShouldBe("three");

            var mapped = (AuditedDto)mapper.Map(
                source,
                typeof(Audited),
                typeof(AuditedDto),
                o => o.Items["actor"] = "four")!;

            mapped.Actor.ShouldBe("four");
        }

        [Fact]
        public void A_missing_options_callback_is_rejected()
        {
            IMapper mapper = Audits().CreateMapper();

            Should.Throw<ArgumentNullException>(
                () => mapper.Map<Audited, AuditedDto>(new Audited(), (Action<IMappingOperationOptions>)null!));
        }
    }
}
