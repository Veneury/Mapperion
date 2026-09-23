using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// Fills in what a map leaves unresolved from the maps of the members it included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs after every map has been through <see cref="ConventionResolver"/>, and not as part
    /// of it, because an included map has to be finished before it can be read from. That also
    /// settles the precedence without any extra rule: by the time this runs, everything the map
    /// itself could resolve already has a source, and only what is left is offered to the included
    /// members.
    /// </para>
    /// <para>
    /// A chain of includes is handled by going round again until nothing changes. Each pass only
    /// fills sources that are missing and never replaces one, so the loop always settles, and two
    /// maps that include each other settle as well instead of chasing each other.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Matching members by convention inspects types by reflection.")]
    internal static class IncludedMemberResolver
    {
        private const int MaxPasses = 8;

        internal static void Apply(TypeMapDefinition[] definitions, MapperOptions options)
        {
            if (!AnyIncludes(definitions))
            {
                return;
            }

            var matcher = new MemberMatcher(options, new TypeMembers(options));

            for (int pass = 0; pass < MaxPasses; pass++)
            {
                bool changed = false;

                for (int i = 0; i < definitions.Length; i++)
                {
                    if (definitions[i].IncludedMembers.Count != 0 &&
                        TryFill(definitions[i], definitions, matcher, out TypeMapDefinition? filled))
                    {
                        definitions[i] = filled!;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        private static bool AnyIncludes(TypeMapDefinition[] definitions)
        {
            foreach (TypeMapDefinition definition in definitions)
            {
                if (definition.IncludedMembers.Count != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFill(
            TypeMapDefinition definition,
            TypeMapDefinition[] definitions,
            MemberMatcher matcher,
            out TypeMapDefinition? filled)
        {
            MemberDefinition[]? members = null;

            for (int i = 0; i < definition.Members.Count; i++)
            {
                MemberDefinition member = definition.Members[i];

                if (member.IsIgnored || member.Source is not null)
                {
                    continue;
                }

                MemberDefinition? resolved = Resolve(definition, member, definitions, matcher);

                if (resolved is null)
                {
                    continue;
                }

                members ??= ToArray(definition.Members);
                members[i] = resolved;
            }

            if (members is null)
            {
                filled = null;
                return false;
            }

            filled = definition.WithResolved(members, definition.Constructor, definition.ConstructorParameters);
            return true;
        }

        private static MemberDefinition? Resolve(
            TypeMapDefinition definition,
            MemberDefinition member,
            TypeMapDefinition[] definitions,
            MemberMatcher matcher)
        {
            foreach (MemberPath prefix in definition.IncludedMembers)
            {
                var key = new TypeMapKey(prefix.MemberType, definition.DestinationType);
                TypeMapDefinition? included = Find(definitions, key);

                MemberDefinition? contributed = included is null
                    ? ByConvention(prefix, member, matcher)
                    : FromMap(definition, prefix, member, included);

                if (contributed is not null)
                {
                    return contributed;
                }
            }

            return null;
        }

        /// <summary>
        /// Takes what the included map says about the member. A path is folded into a longer path
        /// and a constant is used as it stands, because neither needs the included instance; a
        /// resolver or a lambda does, and is wrapped so the compiler reads it against one.
        /// </summary>
        private static MemberDefinition? FromMap(
            TypeMapDefinition definition,
            MemberPath prefix,
            MemberDefinition member,
            TypeMapDefinition included)
        {
            MemberDefinition? inner = included.FindMember(member.DestinationMember.Name);

            if (inner is null || inner.IsIgnored || inner.Source is null)
            {
                return null;
            }

            if (inner.Condition is not null || inner.PreCondition is not null)
            {
                throw new MapperConfigurationException(
                    definition.Key + ": '" + member.DestinationMember.Name + "' comes from the " +
                    "included member '" + prefix + "', whose map " + included.Key + " puts a " +
                    "condition on it. A condition is written against " + included.SourceType.Name +
                    " and cannot be moved onto " + definition.SourceType.Name + ". Configure the " +
                    "member on " + definition.Key + " instead.");
            }

            MemberSource source = inner.Source is MemberPathSource path
                ? new MemberPathSource(Concat(prefix, path.Path))
                : inner.Source is ConstantSource
                    ? inner.Source
                    : new IncludedMemberSource(prefix, inner.Source);

            return new MemberDefinition(member.DestinationMember)
            {
                DestinationPath = member.DestinationPath,
                Source = source,
                MappingOrder = member.MappingOrder,
                UseDestinationValue = member.UseDestinationValue,
                ValueConverterType = member.ValueConverterType ?? inner.ValueConverterType,
                HasNullSubstitute = inner.HasNullSubstitute,
                NullSubstitute = inner.NullSubstitute,
            };
        }

        /// <summary>
        /// Used when no map was declared for the included type. The member is matched against it by
        /// the same conventions as anywhere else, which is what makes the common case work without
        /// declaring a map nobody would otherwise need.
        /// </summary>
        private static MemberDefinition? ByConvention(
            MemberPath prefix,
            MemberDefinition member,
            MemberMatcher matcher)
        {
            MemberPath? inner = matcher.Match(prefix.MemberType, member.DestinationMember.Name);

            if (inner is null)
            {
                return null;
            }

            return new MemberDefinition(member.DestinationMember)
            {
                DestinationPath = member.DestinationPath,
                Source = new MemberPathSource(Concat(prefix, inner)),
                MappingOrder = member.MappingOrder,
                UseDestinationValue = member.UseDestinationValue,
                ValueConverterType = member.ValueConverterType,
            };
        }

        private static MemberPath Concat(MemberPath prefix, MemberPath inner)
        {
            MemberPath combined = prefix;

            foreach (MemberDescriptor step in inner.Steps)
            {
                combined = combined.Append(step);
            }

            return combined;
        }

        private static TypeMapDefinition? Find(TypeMapDefinition[] definitions, TypeMapKey key)
        {
            foreach (TypeMapDefinition definition in definitions)
            {
                if (definition.Key.Equals(key))
                {
                    return definition;
                }
            }

            return null;
        }

        private static MemberDefinition[] ToArray(IReadOnlyList<MemberDefinition> members)
        {
            var copy = new MemberDefinition[members.Count];

            for (int i = 0; i < members.Count; i++)
            {
                copy[i] = members[i];
            }

            return copy;
        }
    }
}
