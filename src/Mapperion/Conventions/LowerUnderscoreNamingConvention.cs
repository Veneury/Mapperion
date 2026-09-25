namespace Mapperion
{
    /// <summary>
    /// Names that separate words with an underscore, such as <c>first_name</c>.
    /// </summary>
    public sealed class LowerUnderscoreNamingConvention : INamingConvention
    {
        /// <summary>Gets the shared instance.</summary>
        public static LowerUnderscoreNamingConvention Instance { get; } = new LowerUnderscoreNamingConvention();

        /// <inheritdoc/>
        public string? SeparatorCharacter => "_";
    }
}
