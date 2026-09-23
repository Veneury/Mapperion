using System;
using Mapperion.Execution;
using Mapperion.Internal;

namespace Mapperion
{
    /// <summary>
    /// Ways into the mapper that trade a little of the interface's convenience for speed.
    /// </summary>
    public static class MapperExtensions
    {
        /// <summary>
        /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>,
        /// skipping the cost of calling a generic method through an interface.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The result is the same as <see cref="IMapper.Map{TSource, TDestination}(TSource)"/> in
        /// every respect. The only difference is how the call gets there: a generic method reached
        /// through an interface makes the runtime work out its type arguments on each call, which
        /// on a small map is about a quarter of the time it takes. This checks for the mapper the
        /// library builds and calls it directly when it finds it, and falls back to the interface
        /// for any other implementation, so a decorator or a test double still works.
        /// </para>
        /// <para>
        /// Worth reaching for in a loop that maps a great many objects, and not worth the noise
        /// anywhere else: the saving is measured in nanoseconds per call, which is nothing next to
        /// almost anything else a program does. If mapping really is the hot path, the source
        /// generator is the answer rather than this; it runs at roughly the speed of code written
        /// by hand, which is far more than this can give back.
        /// </para>
        /// </remarks>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="mapper">The mapper to use.</param>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <returns>The mapped destination instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
        public static TDestination MapFast<TSource, TDestination>(this IMapper mapper, TSource source)
        {
            Guard.NotNull(mapper, nameof(mapper));

            return mapper is Mapper built
                ? built.Map<TSource, TDestination>(source)
                : mapper.Map<TSource, TDestination>(source);
        }

        /// <summary>
        /// Maps <paramref name="source"/> onto an existing instance, skipping the cost of calling a
        /// generic method through an interface.
        /// </summary>
        /// <remarks>
        /// The same trade as
        /// <see cref="MapFast{TSource, TDestination}(IMapper, TSource)"/>, and the same advice:
        /// worth it in a loop over a great many objects, not worth the noise otherwise.
        /// </remarks>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="mapper">The mapper to use.</param>
        /// <param name="source">The source object. May be <see langword="null"/>.</param>
        /// <param name="destination">The destination instance to populate.</param>
        /// <returns>The populated destination instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
        public static TDestination MapFast<TSource, TDestination>(
            this IMapper mapper,
            TSource source,
            TDestination destination)
        {
            Guard.NotNull(mapper, nameof(mapper));

            return mapper is Mapper built
                ? built.Map(source, destination)
                : mapper.Map(source, destination);
        }
    }
}
