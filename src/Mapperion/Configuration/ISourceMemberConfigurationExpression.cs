namespace Mapperion
{
    /// <summary>
    /// What can be said about a member of the source.
    /// </summary>
    /// <remarks>
    /// There is one thing, and it only means anything under
    /// <see cref="Model.MemberListValidation.Source"/>: validation against the source list reports
    /// every readable member that no destination member reads, and this excuses one of them.
    /// </remarks>
    public interface ISourceMemberConfigurationExpression
    {
        /// <summary>
        /// Leaves this source member out of validation, so it may go unread without being
        /// reported.
        /// </summary>
        void DoNotValidate();
    }
}
