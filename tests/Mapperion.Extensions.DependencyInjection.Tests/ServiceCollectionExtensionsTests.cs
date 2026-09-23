using System;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mapperion.Extensions.DependencyInjection.Tests
{
    public sealed class Reading
    {
        public double Celsius { get; set; }
    }

    public sealed class ReadingDto
    {
        public string Scale { get; set; } = string.Empty;

        public double Value { get; set; }
    }

    public interface IScaleProvider
    {
        string Scale { get; }
    }

    public sealed class FixedScaleProvider : IScaleProvider
    {
        public FixedScaleProvider(string scale)
        {
            Scale = scale;
        }

        public string Scale { get; }
    }

    public sealed class ScaleResolver : IValueResolver<Reading, ReadingDto, string>
    {
        private readonly IScaleProvider provider;

        public ScaleResolver(IScaleProvider provider)
        {
            this.provider = provider;
        }

        public string Resolve(Reading source, ReadingDto destination, string destinationMember, ResolutionContext context)
        {
            return provider.Scale;
        }
    }

    public sealed class PlainResolver : IValueResolver<Reading, ReadingDto, string>
    {
        public string Resolve(Reading source, ReadingDto destination, string destinationMember, ResolutionContext context)
        {
            return "plain";
        }
    }

    public sealed class ReadingProfile : Profile
    {
        public ReadingProfile()
        {
            CreateMap<Reading, ReadingDto>()
                .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                .ForMember(d => d.Scale, o => o.MapFrom<PlainResolver>());
        }
    }

    public sealed class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void The_mapper_and_its_configuration_are_registered()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.Ignore()))
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<MapperConfiguration>().ShouldNotBeNull();
            scope.ServiceProvider.GetRequiredService<IMapper>().ShouldNotBeNull();
        }

        [Fact]
        public void The_registered_mapper_maps()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.Ignore()))
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();
            IMapper mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

            mapper.Map<Reading, ReadingDto>(new Reading { Celsius = 21.5 }).Value.ShouldBe(21.5);
        }

        [Fact]
        public void A_resolver_gets_its_dependencies_from_the_container()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IScaleProvider>(new FixedScaleProvider("celsius"))
                .AddScoped<ScaleResolver>()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.MapFrom<ScaleResolver>()))
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();
            IMapper mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

            mapper.Map<Reading, ReadingDto>(new Reading()).Scale.ShouldBe("celsius");
        }

        [Fact]
        public void A_resolver_the_container_does_not_know_is_still_created()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.MapFrom<PlainResolver>()))
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();
            IMapper mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

            mapper.Map<Reading, ReadingDto>(new Reading()).Scale.ShouldBe("plain");
        }

        [Fact]
        public void The_mapper_is_scoped_by_default_but_shares_its_configuration()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.Ignore()))
                .BuildServiceProvider();

            using IServiceScope first = provider.CreateScope();
            using IServiceScope second = provider.CreateScope();

            IMapper firstMapper = first.ServiceProvider.GetRequiredService<IMapper>();
            IMapper secondMapper = second.ServiceProvider.GetRequiredService<IMapper>();

            firstMapper.ShouldNotBeSameAs(secondMapper);
            first.ServiceProvider.GetRequiredService<MapperConfiguration>()
                .ShouldBeSameAs(second.ServiceProvider.GetRequiredService<MapperConfiguration>());
        }

        [Fact]
        public void The_lifetime_can_be_changed()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(
                    cfg => cfg.CreateMap<Reading, ReadingDto>()
                        .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                        .ForMember(d => d.Scale, o => o.Ignore()),
                    ServiceLifetime.Singleton)
                .BuildServiceProvider();

            using IServiceScope first = provider.CreateScope();
            using IServiceScope second = provider.CreateScope();

            first.ServiceProvider.GetRequiredService<IMapper>()
                .ShouldBeSameAs(second.ServiceProvider.GetRequiredService<IMapper>());
        }

        [Fact]
        public void Profiles_can_be_discovered_from_an_assembly()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(typeof(ReadingProfile).Assembly)
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IMapper>()
                .Map<Reading, ReadingDto>(new Reading { Celsius = 3 }).Scale.ShouldBe("plain");
        }

        [Fact]
        public void Profiles_can_be_discovered_from_a_marker_type()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(typeof(ReadingProfile))
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IMapper>().ShouldNotBeNull();
        }

        [Fact]
        public void Registering_twice_keeps_the_first_registration()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddMapperion(cfg => cfg.CreateMap<Reading, ReadingDto>()
                    .ForMember(d => d.Value, o => o.MapFrom(s => s.Celsius))
                    .ForMember(d => d.Scale, o => o.Ignore()))
                .AddMapperion(cfg => { })
                .BuildServiceProvider();

            using IServiceScope scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<MapperConfiguration>().Model.Count.ShouldBe(1);
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            IServiceCollection services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() => services.AddMapperion((Action<IMapperConfigurationExpression>)null!));
            Should.Throw<ArgumentNullException>(() => services.AddMapperion((System.Reflection.Assembly[])null!));
            Should.Throw<ArgumentNullException>(() => services.AddMapperion((Type[])null!));
        }
    }
}
