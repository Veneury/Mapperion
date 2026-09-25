namespace Mapperion
{
    /// <summary>
    /// Names that separate words by capitalising each one, such as <c>FirstName</c>. This is how
    /// both sides are read unless told otherwise.
    /// </summary>
    public sealed class PascalCaseNamingConvention : INamingConvention
    {
        /// <summary>Gets the shared instance.</summary>
        public static PascalCaseNamingConvention Instance { get; } = new PascalCaseNamingConvention();

        /// <inheritdoc/>
        public string? SeparatorCharacter => string.Empty;
    }
}
