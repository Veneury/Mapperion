namespace Mapperion
{
    /// <summary>
    /// Says how the member names on one side of a map are written, so that names following two
    /// different spellings still match each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A source coming from a database or a JSON payload often spells its members
    /// <c>first_name</c> while the destination spells them <c>FirstName</c>. Telling each side
    /// which spelling it uses is enough for the conventions to see those as the same name, with no
    /// <c>ForMember</c> per property.
    /// </para>
    /// <para>
    /// The shape is AutoMapper's, so a convention written against it compiles here unchanged. Its
    /// one member is the separator that divides words in a name: <see langword="null"/> for a
    /// spelling with no separator at all, <c>""</c> for one that separates by capital letters, and
    /// the separator itself for anything else.
    /// </para>
    /// <para>
    /// Member names are CLR identifiers, so the only separator that can appear in one is the
    /// underscore. A convention of your own is therefore for its variants, such as the doubled
    /// separator some code generators emit.
    /// </para>
    /// </remarks>
    public interface INamingConvention
    {
        /// <summary>
        /// Gets the text that separates the words of a member name, or <see langword="null"/> when
        /// names are taken exactly as written.
        /// </summary>
        string? SeparatorCharacter { get; }
    }
}
