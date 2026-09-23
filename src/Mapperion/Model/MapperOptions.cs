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
