namespace Mapperion.Model
{
    /// <summary>
    /// The kind of type member a mapping reads from or writes to.
    /// </summary>
    public enum MemberKind
    {
        /// <summary>A property.</summary>
        Property = 0,

        /// <summary>A field.</summary>
        Field = 1,

        /// <summary>A parameterless method used as a read-only source member.</summary>
        Method = 2,
    }

    /// <summary>
    /// Identifies which flavour of <see cref="MemberSource"/> a definition carries.
    /// </summary>
    public enum MemberSourceKind
    {
        /// <summary>A path of one or more source members, as in <c>Customer.Address.City</c>.</summary>
        MemberPath = 0,

        /// <summary>A value resolver type instantiated when the map runs.</summary>
        ValueResolver = 1,

        /// <summary>A constant value baked into the map.</summary>
        Constant = 2,

        /// <summary>An engine-specific payload, such as a user-supplied lambda.</summary>
        Custom = 3,
    }

    /// <summary>
    /// Which side of a map must be fully covered for the configuration to be considered valid.
    /// </summary>
    public enum MemberListValidation
    {
        /// <summary>Unmapped members on either side are accepted.</summary>
        None = 0,

        /// <summary>Every writable destination member must have a source.</summary>
        Destination = 1,

        /// <summary>Every readable source member must be consumed.</summary>
        Source = 2,
    }

    /// <summary>
    /// How enum values are translated when the source and destination enums differ.
    /// </summary>
    public enum EnumMappingPolicy
    {
        /// <summary>Match by member name, falling back to the underlying numeric value.</summary>
        ByNameThenValue = 0,

        /// <summary>Match by member name only; an unmatched name is an error.</summary>
        ByName = 1,

        /// <summary>Match by underlying numeric value only.</summary>
        ByValue = 2,
    }

    /// <summary>
    /// How source and destination member names are compared when matching by convention.
    /// </summary>
    public enum MemberNameComparison
    {
        /// <summary>Compare ignoring case.</summary>
        OrdinalIgnoreCase = 0,

        /// <summary>Compare with exact casing.</summary>
        Ordinal = 1,
    }
}
