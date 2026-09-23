using System.Collections.Generic;
using Mapperion.Compilation;

namespace Mapperion
{
    /// <summary>
    /// What the caller can set for one mapping operation, as opposed to for the configuration.
    /// </summary>
    public interface IMappingOperationOptions
    {
        /// <summary>
        /// Gets the values shared with every converter, resolver and step taking part in this
        /// operation. Use it to hand context down that is not part of the source object, such as
        /// the current user or tenant.
        /// </summary>
        /// <remarks>
        /// The dictionary belongs to one operation and is not shared with another, so nothing put
        /// here leaks between two calls to the same mapper. Keys are compared exactly.
        /// </remarks>
        IDictionary<string, object?> Items { get; }
    }
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
        public IMapper Mapper => context.Mapper;

        /// <summary>
        /// Gets the values the caller passed for this operation, and anything another converter or
        /// resolver has put there since.
        /// </summary>
        /// <exception cref="MappingException">
        /// The operation was started by invoking a compiled plan directly rather than through
        /// <see cref="IMapper"/>, so it has nowhere to keep them.
        /// </exception>
        public IDictionary<string, object?> Items =>
            (context.State ?? throw new MappingException(
                "This operation has no shared items because it was not started through IMapper."))
            .Items;
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
    /// A step that runs before or after a map, reusable across configurations. Use it instead of a
    /// lambda when the step needs its own type, for example to be tested on its own.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    public interface IMappingAction<in TSource, in TDestination>
    {
        /// <summary>Runs the step.</summary>
        /// <param name="source">The object being mapped.</param>
        /// <param name="destination">The destination being built.</param>
        /// <param name="context">The current operation.</param>
        void Process(TSource source, TDestination destination, ResolutionContext context);
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
