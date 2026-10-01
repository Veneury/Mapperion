using System;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Depot
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class DepotDto
    {
        public string Code { get; set; } = string.Empty;

        public string StampedBy { get; set; } = string.Empty;
    }

    public interface IStamp
    {
        string Who { get; }
    }

    /// <summary>
    /// Takes its dependency through the constructor, which is the shape a container is for. Left
    /// out of one, it cannot be built at all.
    /// </summary>
    public sealed class StampResolver : IValueResolver<Depot, DepotDto, string>
    {
        private readonly IStamp stamp;

        public StampResolver(IStamp stamp)
        {
            this.stamp = stamp;
        }

        public string Resolve(Depot source, DepotDto destination, string destinationMember, ResolutionContext context)
        {
            return stamp.Who;
        }
    }

    public sealed class ResolverCreationTests
    {
        /// <summary>
        /// The advice was there and unreachable. Activator throws for a type with no parameterless
        /// constructor rather than returning null, so the message that said what to do about it sat
        /// behind a null check that only an empty Nullable&lt;T&gt; can reach, and what came out was
        /// the runtime's MissingMethodException.
        /// </summary>
        [Fact]
        public void A_resolver_that_cannot_be_built_says_what_to_do_about_it()
        {
            MapperConfiguration configuration = new MapperConfiguration(cfg =>
                cfg.CreateMap<Depot, DepotDto>()
                    .ForMember(d => d.StampedBy, o => o.MapFrom<StampResolver>()));

            IMapper mapper = configuration.CreateMapper();

            Exception thrown = Should.Throw<Exception>(
                () => mapper.Map<Depot, DepotDto>(new Depot { Code = "D-1" }));

            MapperConfigurationException? reason = Find(thrown);

            reason.ShouldNotBeNull();
            reason!.Message.ShouldContain("StampResolver");
            reason.Message.ShouldContain("registered in the container");
            reason.InnerException.ShouldBeOfType<MissingMethodException>();
        }

        private static MapperConfigurationException? Find(Exception exception)
        {
            for (Exception? e = exception; e is not null; e = e.InnerException)
            {
                if (e is MapperConfigurationException configuration)
                {
                    return configuration;
                }
            }

            return null;
        }
    }
}
