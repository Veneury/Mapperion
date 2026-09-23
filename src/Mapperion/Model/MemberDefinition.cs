using System;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// How one destination member is populated. Instances are immutable; the fluent configuration
    /// layer builds them and hands them to the compiler.
    /// </summary>
    public sealed class MemberDefinition
    {
        /// <summary>Creates a definition for a destination member.</summary>
        /// <param name="destinationMember">The member being populated.</param>
        /// <exception cref="ArgumentNullException"><paramref name="destinationMember"/> is <see langword="null"/>.</exception>
        public MemberDefinition(MemberDescriptor destinationMember)
        {
            DestinationMember = Guard.NotNull(destinationMember, nameof(destinationMember));
        }

        /// <summary>Gets the destination member being populated.</summary>
        public MemberDescriptor DestinationMember { get; }

        /// <summary>
        /// Gets the chain of destination members walked to reach the one being populated, when it
        /// sits inside another object instead of directly on the destination.
        /// </summary>
        /// <remarks>
        /// Null, or a chain of one, means the member is directly on the destination, which is the
        /// ordinary case. A longer chain comes from <c>ForPath</c> and makes the compiler walk the
        /// destination, creating the objects along the way, before assigning.
        /// </remarks>
        public MemberPath? DestinationPath { get; init; }

        /// <summary>Gets a value indicating whether the member sits inside another object.</summary>
        public bool IsPath => DestinationPath is not null && DestinationPath.Length > 1;

        /// <summary>
        /// Gets where the value comes from, or <see langword="null"/> when no source could be
        /// resolved. A null source on a member that is not ignored is a configuration error.
        /// </summary>
        public MemberSource? Source { get; init; }

        /// <summary>Gets a value indicating whether the member is deliberately left unmapped.</summary>
        public bool IsIgnored { get; init; }

        /// <summary>
        /// Gets a value indicating whether the user configured this member explicitly, as opposed
        /// to it having been resolved by convention. Explicit configuration always wins.
        /// </summary>
        public bool IsExplicit { get; init; }

        /// <summary>
        /// Gets a value indicating whether the existing destination value is populated in place
        /// instead of a new instance being created.
        /// </summary>
        public bool UseDestinationValue { get; init; }

        /// <summary>Gets the relative order in which this member is assigned. Lower runs first.</summary>
        public int MappingOrder { get; init; }

        /// <summary>Gets a value indicating whether a null source value is replaced by <see cref="NullSubstitute"/>.</summary>
        public bool HasNullSubstitute { get; init; }

        /// <summary>Gets the value used when the source resolves to null.</summary>
        public object? NullSubstitute { get; init; }

        /// <summary>Gets the value converter applied to this member, when one is configured.</summary>
        public Type? ValueConverterType { get; init; }

        /// <summary>
        /// Gets the predicate evaluated after the source value is read; the assignment is skipped
        /// when it returns false. Held as <see cref="object"/> for the reason given on
        /// <see cref="CustomSource"/>.
        /// </summary>
        public object? Condition { get; init; }

        /// <summary>
        /// Gets the predicate evaluated before the source value is read, allowing an expensive or
        /// throwing source to be skipped entirely.
        /// </summary>
        public object? PreCondition { get; init; }

        internal MemberDefinition WithSource(MemberSource? source)
        {
            return new MemberDefinition(DestinationMember)
            {
                DestinationPath = DestinationPath,
                Source = source,
                IsIgnored = IsIgnored,
                IsExplicit = IsExplicit,
                UseDestinationValue = UseDestinationValue,
                MappingOrder = MappingOrder,
                HasNullSubstitute = HasNullSubstitute,
                NullSubstitute = NullSubstitute,
                ValueConverterType = ValueConverterType,
                Condition = Condition,
                PreCondition = PreCondition,
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string name = DestinationPath?.ToString() ?? DestinationMember.Name;

            if (IsIgnored)
            {
                return name + " <- (ignored)";
            }

            return name + " <- " + (Source?.ToString() ?? "(unresolved)");
        }
    }
}
