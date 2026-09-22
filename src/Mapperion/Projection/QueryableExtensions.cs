using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Mapperion.Internal;

namespace Mapperion
{
    /// <summary>
    /// Turns a query over entities into a query over destination types, so the database returns
    /// only the columns the destination needs.
    /// </summary>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Rewrites the query to select <typeparamref name="TDestination"/> directly, without
        /// materialising the source.
        /// </summary>
        /// <typeparam name="TDestination">The type to project to.</typeparam>
        /// <param name="source">The query over the source type.</param>
        /// <param name="configuration">The configuration holding the map.</param>
        /// <returns>The projected query.</returns>
        /// <exception cref="System.ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException">The map cannot be expressed as a projection.</exception>
        [RequiresUnreferencedCode("Building a projection inspects types by reflection.")]
        [RequiresDynamicCode("Building a projection emits code at run time.")]
        public static IQueryable<TDestination> ProjectTo<TDestination>(
            this IQueryable source,
            MapperConfiguration configuration)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(configuration, nameof(configuration));

            return configuration.CreateMapper().ProjectTo<TDestination>(source);
        }
    }
}
