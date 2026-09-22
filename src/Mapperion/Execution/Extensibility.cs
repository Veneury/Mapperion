using Mapperion.Compilation;

namespace Mapperion
{
    /// <summary>
    /// What a converter or resolver is given about the operation it takes part in. Mostly a way
    /// back into the mapper, so user code can map nested values without holding a reference itself.
    /// </summary>
    public readonly struct ResolutionContext
    {
        private readonly MappingContext context;

        internal ResolutionContext(MappingContext context)
        {
            this.context = context;
        }

        /// <summary>Gets the mapper running the current operation.</summary>
        public IMapper Mapper => context.Engine.Mapper;
    }

    /// <summary>
    /// Replaces the mapping of one type pair entirely. When a map has a type converter, member
    /// configuration is not used at all: the converter produces the destination on its own.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    public interface ITypeConverter<in TSource, TDestination>
    {
        /// <summary>Produces the destination from the source.</summary>
        /// <param name="source">The object to read from.</param>
        /// <param name="destination">The instance to populate, or the default to create one.</param>
        /// <param name="context">The current operation.</param>
        /// <returns>The destination.</returns>
        TDestination Convert(TSource source, TDestination destination, ResolutionContext context);
    }

    /// <summary>
    /// Converts one value into another, reusable across any member with the same pair of types.
    /// </summary>
    /// <typeparam name="TSourceMember">The type read from the source.</typeparam>
    /// <typeparam name="TDestinationMember">The type written to the destination.</typeparam>
    public interface IValueConverter<in TSourceMember, out TDestinationMember>
    {
        /// <summary>Converts the value.</summary>
        /// <param name="sourceMember">The value read from the source.</param>
        /// <param name="context">The current operation.</param>
        /// <returns>The converted value.</returns>
        TDestinationMember Convert(TSourceMember sourceMember, ResolutionContext context);
    }

    /// <summary>
    /// Produces the value of one destination member with the whole source and destination in hand,
    /// for cases a single expression cannot express.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <typeparam name="TDestinationMember">The destination member type.</typeparam>
    public interface IValueResolver<in TSource, in TDestination, TDestinationMember>
    {
        /// <summary>Produces the value of the member.</summary>
        /// <param name="source">The object being mapped.</param>
        /// <param name="destination">The destination being built, or its default when the member feeds a constructor.</param>
        /// <param name="destinationMember">The value the member currently holds.</param>
        /// <param name="context">The current operation.</param>
        /// <returns>The value to assign.</returns>
        TDestinationMember Resolve(
            TSource source,
            TDestination destination,
            TDestinationMember destinationMember,
            ResolutionContext context);
    }
}
