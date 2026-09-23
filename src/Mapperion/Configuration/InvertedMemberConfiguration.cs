using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// A member of a reversed map. It already knows both ends, so unlike
    /// <see cref="MemberConfiguration{TSource,TDestination,TMember}"/> it needs no generic
    /// parameters: the member types are only known at reversal time, not at compile time.
    /// </summary>
    internal sealed class InvertedMemberConfiguration : IMemberConfiguration
    {
        private readonly MemberSource source;

        internal InvertedMemberConfiguration(MemberDescriptor destinationMember, MemberSource source)
        {
            DestinationMember = destinationMember;
            this.source = source;
        }

        public MemberDescriptor DestinationMember { get; }

        /// <remarks>
        /// Always null. A reversal only ever produces direct members: a path written into the
        /// destination has no single source member to come back from.
        /// </remarks>
        public MemberPath? DestinationPath => null;

        public MemberDefinition Build()
        {
            return new MemberDefinition(DestinationMember)
            {
                Source = source,
                IsExplicit = true,
            };
        }
    }
}
