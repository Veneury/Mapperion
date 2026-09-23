using System;
using System.Collections.Generic;

namespace Mapperion.Model
{
    /// <summary>
    /// Settings that apply to every map unless a single map overrides them.
    /// </summary>
    public sealed class MapperOptions
    {
        private static readonly string[] NoAffixes = Array.Empty<string>();

        /// <summary>Gets the settings used when nothing is configured.</summary>
        public static MapperOptions Defaults { get; } = new MapperOptions();

        /// <summary>Gets how member names are compared when matching by convention.</summary>
        public MemberNameComparison NameComparison { get; init; } = MemberNameComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Gets how many levels the flattening convention walks when looking for a source, so that
        /// <c>CustomerAddressCity</c> can resolve to <c>Customer.Address.City</c>.
        /// </summary>
        public int MaxFlatteningDepth { get; init; } = 3;

        /// <summary>Gets a value indicating whether a null source collection maps to null instead of an empty one.</summary>
        public bool AllowNullCollections { get; init; }

        /// <summary>
        /// Gets a value indicating whether a member whose source resolves to null is left null.
        /// </summary>
        /// <remarks>
        /// Turning it off replaces a null with the destination type's empty content: an empty
        /// string, or a new instance when the type has a parameterless constructor. Value types are
        /// untouched, since they already carry their own default, and collections keep answering to
        /// <see cref="AllowNullCollections"/>, which is the setting about them. In a projection only
        /// the string case applies: a query provider cannot build an object out of nothing.
        /// </remarks>
        public bool AllowNullDestinationValues { get; init; } = true;

        /// <summary>
        /// Gets how many levels deep a map that can reach itself may recurse before the operation
        /// fails with <see cref="RecursionLimitException"/>.
        /// </summary>
        /// <remarks>
        /// Only the maps that close a loop with nothing else to stop them are counted, so a
        /// configuration whose types cannot recurse pays nothing for this. The ceiling exists so a
        /// looping object graph raises an exception the caller can catch instead of a
        /// <see cref="StackOverflowException"/>, which it cannot. Raise it for a graph that really
        /// is deeper than this; zero or less removes the ceiling and brings the stack overflow
        /// back.
        /// </remarks>
        /// <remarks>
        /// The default matches what <c>System.Text.Json</c> and Newtonsoft use for the same kind of
        /// protection. It has to stay well under the depth at which the stack itself gives out,
        /// which for a compiled plan on a one-megabyte stack is only a few hundred levels, and it
        /// counts one pair rather than total nesting, so a loop through several maps reaches a
        /// deeper stack for the same count.
        /// </remarks>
        public int RecursionLimit { get; init; } = 64;

        /// <summary>Gets a value indicating whether public fields are considered alongside properties.</summary>
        public bool IncludeFields { get; init; }

        /// <summary>Gets a value indicating whether parameterless methods are considered as source members.</summary>
        public bool IncludeSourceMethods { get; init; } = true;

        /// <summary>Gets how enum values are translated.</summary>
        public EnumMappingPolicy EnumMapping { get; init; } = EnumMappingPolicy.ByNameThenValue;

        /// <summary>Gets which side must be fully covered for a map to validate.</summary>
        public MemberListValidation MemberListValidation { get; init; } = MemberListValidation.Destination;

        /// <summary>
        /// Gets a value indicating whether the configuration is validated when it is built.
        /// Off by default, matching AutoMapper: call <c>AssertIsValid</c> when you want the check.
        /// </summary>
        public bool ValidateOnBuild { get; init; }

        /// <summary>Gets the source member name prefixes stripped before matching, such as <c>Get</c>.</summary>
        public IReadOnlyList<string> SourcePrefixes { get; init; } = NoAffixes;

        /// <summary>Gets the source member name suffixes stripped before matching.</summary>
        public IReadOnlyList<string> SourcePostfixes { get; init; } = NoAffixes;

        /// <summary>Gets the destination member name prefixes stripped before matching.</summary>
        public IReadOnlyList<string> DestinationPrefixes { get; init; } = NoAffixes;

        /// <summary>Gets the destination member name suffixes stripped before matching, such as <c>Dto</c>.</summary>
        public IReadOnlyList<string> DestinationPostfixes { get; init; } = NoAffixes;

        /// <summary>Gets the string comparison implied by <see cref="NameComparison"/>.</summary>
        public StringComparison NameStringComparison =>
            NameComparison == MemberNameComparison.Ordinal ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }
}
