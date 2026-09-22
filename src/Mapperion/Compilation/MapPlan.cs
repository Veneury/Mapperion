using System;

namespace Mapperion.Compilation
{
    /// <summary>
    /// The shape every compiled map takes. The destination is passed in so the same delegate serves
    /// both creating an instance and populating one the caller already has.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The object to read from.</param>
    /// <param name="destination">The instance to populate, or the default to create one.</param>
    /// <param name="context">The state shared by every map in one operation.</param>
    /// <returns>The populated destination.</returns>
    internal delegate TDestination MapDelegate<TSource, TDestination>(
        TSource source,
        TDestination destination,
        MappingContext context);

    /// <summary>
    /// Carried through a whole mapping operation. A struct holding a single reference so a simple
    /// map allocates nothing beyond its destination.
    /// </summary>
    internal readonly struct MappingContext
    {
        internal MappingContext(MapperEngine engine)
        {
            Engine = engine;
        }

        internal MapperEngine Engine { get; }
    }

    /// <summary>
    /// One compiled map, in both its typed form and a boxed one for the non-generic API.
    /// </summary>
    internal sealed class MapPlan
    {
        internal MapPlan(Delegate typed, Func<object?, object?, MappingContext, object?> boxed)
        {
            Typed = typed;
            Boxed = boxed;
        }

        internal Delegate Typed { get; }

        internal Func<object?, object?, MappingContext, object?> Boxed { get; }
    }
}
