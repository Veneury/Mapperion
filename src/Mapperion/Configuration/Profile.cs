using System;
using System.Collections.Generic;
using Mapperion.Configuration;

namespace Mapperion
{
    /// <summary>
    /// Groups related maps so a module can own its own configuration. Derive from this class,
    /// declare the maps in the constructor, and register it with
    /// <see cref="IMapperConfigurationExpression.AddProfile{TProfile}"/>.
    /// </summary>
    public abstract class Profile : ITypeMapRegistry
    {
        private readonly List<ITypeMapConfiguration> typeMaps = new List<ITypeMapConfiguration>();

        /// <summary>Initializes the profile.</summary>
        protected Profile()
        {
        }

        internal IReadOnlyList<ITypeMapConfiguration> TypeMaps => typeMaps;

        /// <summary>Declares a map between two types.</summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <returns>The expression used to configure the map.</returns>
        protected IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var configuration = new TypeMapConfiguration<TSource, TDestination>(this);
            typeMaps.Add(configuration);
            return configuration;
        }

        /// <summary>
        /// Declares a map with types rather than type arguments, for an open generic pair.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The expression used to configure the map.</returns>
        protected IOpenMappingExpression CreateMap(Type sourceType, Type destinationType)
        {
            var configuration = new OpenTypeMapConfiguration(sourceType, destinationType);
            typeMaps.Add(configuration);
            return configuration;
        }

        void ITypeMapRegistry.Add(ITypeMapConfiguration configuration)
        {
            typeMaps.Add(configuration);
        }
    }
}
