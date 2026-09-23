using System.Threading;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Hands out one number per type pair, for the whole process.
    /// </summary>
    internal static class TypeMapSlot
    {
        private static int next = -1;

        internal static int Next() => Interlocked.Increment(ref next);
    }

    /// <summary>
    /// The number belonging to one type pair, so an engine can reach that pair's plan through an
    /// array instead of looking a key up in a dictionary.
    /// </summary>
    /// <remarks>
    /// The point is that the number is a <c>static readonly</c> on a generic type, which the JIT
    /// folds into a constant once the static constructor has run. A call to
    /// <c>Map&lt;Order, OrderDto&gt;</c> therefore turns into an array load at a fixed offset, with
    /// no key to build and nothing to hash. Numbers are handed out per pair rather than per engine
    /// so the generic type can hold them; each engine keeps its own array, so two configurations
    /// never see each other's plans.
    /// </remarks>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    internal static class TypeMapSlot<TSource, TDestination>
    {
        internal static readonly int Index = TypeMapSlot.Next();
    }
}
