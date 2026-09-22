using System;

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
        /// Maps <paramref name="source"/> to <paramref name="destinationType"/> without generics.
        /// </summary>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="sourceType">The declared source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The mapped destination instance.</returns>
        object? Map(object? source, Type sourceType, Type destinationType);
    }
}
