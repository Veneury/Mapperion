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
        /// Settles the map for a pair once and hands back the function, for a loop that maps the
        /// same pair many times.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>mapper.Map&lt;Order, OrderDto&gt;(order)</c> is a generic method reached through an
        /// interface, and the runtime works out its type arguments on every call. None of that
        /// work depends on the object being mapped, so in a loop it is the same answer found over
        /// and over. This asks for it once:
        /// </para>
        /// <code>
        /// Func&lt;Order, OrderDto&gt; toDto = mapper.MapperFor&lt;Order, OrderDto&gt;();
        ///
        /// foreach (Order order in orders)
        /// {
        ///     results.Add(toDto(order));
        /// }
        /// </code>
        /// <para>
        /// The result is an ordinary <see cref="Func{T, TResult}"/>, so it also goes straight into
        /// a <c>Select</c>. It maps exactly what <see cref="IMapper.Map{TSource, TDestination}(TSource)"/>
        /// maps, and it is bound to the configuration it came from.
        /// </para>
        /// <para>
        /// Hold it for as long as the loop, or as a field beside the mapper. Asking for one per
        /// call costs more than it saves, since the work it avoids is the work it does. For a
        /// single map, <c>Map</c> is the one to write, and where mapping is genuinely what a
        /// program spends its time on, the source generator beats all of this.
        /// </para>
        /// </remarks>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="mapper">The mapper to bind.</param>
        /// <returns>A function mapping one <typeparamref name="TSource"/> to a new <typeparamref name="TDestination"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
        public static Func<TSource, TDestination> MapperFor<TSource, TDestination>(this IMapper mapper)
        {
            Guard.NotNull(mapper, nameof(mapper));

            // Anything that is not the mapper this library builds keeps its own behaviour: a
            // decorator that counts calls has to go on counting them.
            return mapper is Mapper built
                ? built.Bind<TSource, TDestination>()
                : source => mapper.Map<TSource, TDestination>(source);
        }

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
