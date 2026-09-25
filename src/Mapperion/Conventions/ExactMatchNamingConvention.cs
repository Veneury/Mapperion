namespace Mapperion
{
    /// <summary>
    /// Names taken exactly as written, with no word structure read into them.
    /// </summary>
    /// <remarks>
    /// As the source convention this also turns flattening off, since flattening works by reading
    /// a destination name as a series of words and walking one source member per word. A
    /// destination <c>CustomerName</c> then only matches a source member of that name, and never
    /// <c>Customer.Name</c>. That is what it does in AutoMapper too.
    /// </remarks>
    public sealed class ExactMatchNamingConvention : INamingConvention
    {
        /// <summary>Gets the shared instance.</summary>
        public static ExactMatchNamingConvention Instance { get; } = new ExactMatchNamingConvention();

        /// <inheritdoc/>
        public string? SeparatorCharacter => null;
    }
}
