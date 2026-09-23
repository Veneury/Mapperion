using Mapperion.Model;

namespace Mapperion
{
    /// <summary>
    /// Configures a map declared with types rather than type arguments, which is how an open
    /// generic pair such as <c>Page&lt;&gt;</c> to <c>PageDto&lt;&gt;</c> is declared.
    /// </summary>
    /// <remarks>
    /// Only the options that do not need the types to be known appear here. A member cannot be
    /// configured with an expression against a type that has no type arguments yet, so members are
    /// left to the conventions once the pair is closed, and the one thing that can be said in
    /// advance is which of them to leave alone.
    /// </remarks>
    public interface IOpenMappingExpression
    {
        /// <summary>Leaves a destination member unmapped, by name.</summary>
        /// <param name="destinationMemberName">The member to skip.</param>
        /// <returns>This expression, for chaining.</returns>
        IOpenMappingExpression IgnoreMember(string destinationMemberName);

        /// <summary>Sets which side must be fully covered for this map to validate.</summary>
        /// <param name="validation">The validation mode.</param>
        /// <returns>This expression, for chaining.</returns>
        IOpenMappingExpression ValidateMemberList(MemberListValidation validation);

        /// <summary>Limits how deep recursive maps descend.</summary>
        /// <param name="depth">The maximum depth.</param>
        /// <returns>This expression, for chaining.</returns>
        IOpenMappingExpression MaxDepth(int depth);

        /// <summary>Tracks already-mapped instances so reference cycles terminate.</summary>
        /// <returns>This expression, for chaining.</returns>
        IOpenMappingExpression PreserveReferences();
    }
}
