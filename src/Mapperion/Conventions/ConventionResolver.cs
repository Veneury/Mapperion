using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// Fills in everything the user did not configure by hand: the constructor to build the
    /// destination with, its arguments, and every remaining writable member. Explicit configuration
    /// always wins, and anything that resolves to nothing is left with a null source for validation
    /// to report.
    /// </summary>
    [RequiresUnreferencedCode("Convention resolution inspects types by reflection.")]
    internal sealed class ConventionResolver
    {
        private static readonly ConstructorParameterDefinition[] NoParameters =
            Array.Empty<ConstructorParameterDefinition>();

        private readonly TypeMembers members;
        private readonly MemberMatcher matcher;
        private readonly ConstructorSelector constructors;

        internal ConventionResolver(MapperOptions options)
        {
            members = new TypeMembers(options);
            matcher = new MemberMatcher(options, members);
            constructors = new ConstructorSelector(matcher);
        }

        internal TypeMapDefinition Complete(TypeMapDefinition definition)
        {
            if (definition.HasTypeConverter)
            {
                return definition;
            }

            ConstructorInfo? constructor = null;
            IReadOnlyList<ConstructorParameterDefinition> parameters = NoParameters;

            if (ConstructorSelector.IsNeeded(definition))
            {
                constructor = constructors.Select(definition, out parameters);
            }

            var completed = new List<MemberDefinition>(definition.Members.Count);

            foreach (MemberDefinition configured in definition.Members)
            {
                completed.Add(Resolve(configured, definition));
            }

            foreach (MemberDescriptor destination in members.Writable(definition.DestinationType))
            {
                if (IsConfigured(definition, destination) || IsBuiltByConstructor(parameters, destination))
                {
                    continue;
                }

                MemberPath? path = matcher.Match(definition.SourceType, destination.Name);

                completed.Add(new MemberDefinition(destination)
                {
                    Source = path is null ? null : new MemberPathSource(path),
                });
            }

            return definition.WithResolved(completed.ToArray(), constructor, parameters);
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

        private static bool IsBuiltByConstructor(
            IReadOnlyList<ConstructorParameterDefinition> parameters,
            MemberDescriptor destination)
        {
            foreach (ConstructorParameterDefinition parameter in parameters)
            {
                if (string.Equals(parameter.Name, destination.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
