using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mapperion
{
    /// <summary>
    /// Registers Mapperion with <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Builds the configuration once and registers it, plus an <see cref="IMapper"/> bound to
        /// the container so converters, resolvers and mapping actions can take dependencies.
        /// </summary>
        /// <param name="services">The collection to add to.</param>
        /// <param name="configure">The callback that declares maps and sets options.</param>
        /// <param name="mapperLifetime">
        /// How long a mapper lives. Scoped by default, so a resolver may depend on scoped services;
        /// the compiled plans are shared across every mapper regardless, so this is cheap.
        /// </param>
        /// <returns>The same collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Mapping resolves members by reflection.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        public static IServiceCollection AddMapperion(
            this IServiceCollection services,
            Action<IMapperConfigurationExpression> configure,
            ServiceLifetime mapperLifetime = ServiceLifetime.Scoped)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(configure, nameof(configure));

            var configuration = new MapperConfiguration(configure);

            services.TryAddSingleton(configuration);
            services.TryAdd(ServiceDescriptor.Describe(
                typeof(IMapper),
                provider => provider.GetRequiredService<MapperConfiguration>().CreateMapper(provider),
                mapperLifetime));

            return services;
        }

        /// <summary>
        /// Registers every profile found in the given assemblies, then the mapper.
        /// </summary>
        /// <param name="services">The collection to add to.</param>
        /// <param name="assemblies">The assemblies to scan for profiles.</param>
        /// <returns>The same collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Scanning assemblies for profiles is not compatible with trimming.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        public static IServiceCollection AddMapperion(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(assemblies, nameof(assemblies));

            return services.AddMapperion(cfg => cfg.AddProfiles(assemblies));
        }

        /// <summary>
        /// Registers every profile found in the assemblies the given types come from, then the
        /// mapper. Handy when a marker type is easier to name than an assembly.
        /// </summary>
        /// <param name="services">The collection to add to.</param>
        /// <param name="markers">Types whose assemblies are scanned.</param>
        /// <returns>The same collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Scanning assemblies for profiles is not compatible with trimming.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        public static IServiceCollection AddMapperion(
            this IServiceCollection services,
            params Type[] markers)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(markers, nameof(markers));

            var assemblies = new Assembly[markers.Length];

            for (int i = 0; i < markers.Length; i++)
            {
                assemblies[i] = markers[i].Assembly;
            }

            return services.AddMapperion(assemblies);
        }
    }
}
