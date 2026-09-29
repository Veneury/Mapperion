namespace Mapperion.Configuration
{
    /// <summary>
    /// What <c>ForSourceMember</c> collects. One flag, because there is one thing to say.
    /// </summary>
    internal sealed class SourceMemberConfiguration : ISourceMemberConfigurationExpression
    {
        internal bool IsExcludedFromValidation { get; private set; }

        public void DoNotValidate() => IsExcludedFromValidation = true;
    }
}
