using System.Collections.Generic;
using Mapperion.Configuration;

namespace Mapperion
{
    /// <summary>
    /// Groups related maps so a module can own its own configuration. Derive from this class,
    /// declare the maps in the constructor, and register it with
    /// <see cref="IMapperConfigurationExpression.AddProfile{TProfile}"/>.
    /// </summary>
    public abstract class Profile
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
            var configuration = new TypeMapConfiguration<TSource, TDestination>();
            typeMaps.Add(configuration);
            return configuration;
        }
    }
}
