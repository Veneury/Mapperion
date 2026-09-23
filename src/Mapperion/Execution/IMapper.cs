using System;
using System.Linq;

namespace Mapperion
{
    /// <summary>
    /// Performs object-to-object mapping using a configuration built at startup.
    /// Instances are immutable and safe for concurrent use.
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>.
        /// </summary>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <returns>The mapped destination instance.</returns>
        TDestination Map<TDestination>(object? source);

        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>
        /// using the statically known source type, avoiding a runtime type lookup.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <returns>The mapped destination instance.</returns>
        TDestination Map<TSource, TDestination>(TSource source);

        /// <summary>
        /// Maps <paramref name="source"/> onto an existing <paramref name="destination"/> instance.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="destination">The destination instance to populate.</param>
        /// <returns>The populated destination instance.</returns>
        TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>,
        /// passing values the converters, resolvers and steps of this operation can read.
        /// </summary>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="options">Sets up the operation, typically by filling its items.</param>
        /// <returns>The mapped destination instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
        TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions> options);

        /// <summary>
        /// Maps <paramref name="source"/> using the statically known source type, passing values
        /// the converters, resolvers and steps of this operation can read.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="options">Sets up the operation, typically by filling its items.</param>
        /// <returns>The mapped destination instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
        TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions> options);

        /// <summary>
        /// Maps <paramref name="source"/> onto an existing instance, passing values the converters,
        /// resolvers and steps of this operation can read.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="destination">The destination instance to populate.</param>
        /// <param name="options">Sets up the operation, typically by filling its items.</param>
        /// <returns>The populated destination instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
        TDestination Map<TSource, TDestination>(
            TSource source,
            TDestination destination,
            Action<IMappingOperationOptions> options);

        /// <summary>
        /// Rewrites a query so the provider selects <typeparamref name="TDestination"/> directly,
        /// without materialising the source. Only maps a query provider can translate work here:
        /// converters, resolvers and the before and after steps are reported, not skipped.
        /// </summary>
        /// <typeparam name="TDestination">The type to project to.</typeparam>
        /// <param name="source">The query over the source type.</param>
        /// <returns>The projected query.</returns>
        IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source);

        /// <summary>
        /// Maps <paramref name="source"/> to <paramref name="destinationType"/> without generics.
        /// </summary>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="sourceType">The declared source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The mapped destination instance.</returns>
        object? Map(object? source, Type sourceType, Type destinationType);

        /// <summary>
        /// Maps <paramref name="source"/> to <paramref name="destinationType"/> without generics,
        /// passing values the converters, resolvers and steps of this operation can read.
        /// </summary>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="sourceType">The declared source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <param name="options">Sets up the operation, typically by filling its items.</param>
        /// <returns>The mapped destination instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="sourceType"/>, <paramref name="destinationType"/> or
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        object? Map(
            object? source,
            Type sourceType,
            Type destinationType,
            Action<IMappingOperationOptions> options);
    }
}
