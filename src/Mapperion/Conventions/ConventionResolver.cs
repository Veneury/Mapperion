using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// Fills in every destination member the user did not configure by hand. Explicit configuration
    /// always wins; a member that resolves to nothing is left with a null source for validation to
    /// report.
    /// </summary>
    [RequiresUnreferencedCode("Convention resolution inspects types by reflection.")]
    internal sealed class ConventionResolver
    {
        private readonly TypeMembers members;
        private readonly MemberMatcher matcher;

        internal ConventionResolver(MapperOptions options)
        {
            members = new TypeMembers(options);
            matcher = new MemberMatcher(options, members);
        }

        internal TypeMapDefinition Complete(TypeMapDefinition definition)
        {
            if (definition.HasTypeConverter)
            {
                return definition;
            }

            var completed = new List<MemberDefinition>(definition.Members.Count);

            foreach (MemberDefinition configured in definition.Members)
            {
                completed.Add(Resolve(configured, definition));
            }

            foreach (MemberDescriptor destination in members.Writable(definition.DestinationType))
            {
                if (IsConfigured(definition, destination))
                {
                    continue;
                }

                MemberPath? path = matcher.Match(definition.SourceType, destination.Name);

                completed.Add(new MemberDefinition(destination)
                {
                    Source = path is null ? null : new MemberPathSource(path),
                });
            }

            return definition.WithMembers(completed.ToArray());
        }

        private MemberDefinition Resolve(MemberDefinition configured, TypeMapDefinition definition)
        {
            if (configured.IsIgnored || configured.Source is not null)
            {
                return configured;
            }

            MemberPath? path = matcher.Match(definition.SourceType, configured.DestinationMember.Name);
            return path is null ? configured : configured.WithSource(new MemberPathSource(path));
        }

        private static bool IsConfigured(TypeMapDefinition definition, MemberDescriptor destination)
        {
            foreach (MemberDefinition member in definition.Members)
            {
                if (member.DestinationMember.Equals(destination))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
