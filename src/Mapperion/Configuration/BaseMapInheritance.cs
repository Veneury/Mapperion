using System;
using System.Collections.Generic;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Folds the member configuration of a base map into the maps that asked for it with
    /// <c>IncludeBase</c>, before the conventions run.
    /// </summary>
    /// <remarks>
    /// Only members the derived map has not configured itself are taken, so the derived
    /// configuration always wins. A base member reads through reflection members declared on the
    /// base type, which a derived instance answers to just as well, so the definitions copy across
    /// unchanged.
    /// </remarks>
    internal static class BaseMapInheritance
    {
        internal static void Apply(TypeMapDefinition[] definitions)
        {
            var byKey = new Dictionary<TypeMapKey, TypeMapDefinition>(definitions.Length);

            foreach (TypeMapDefinition definition in definitions)
            {
                byKey[definition.Key] = definition;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].BaseMaps.Count != 0)
                {
                    definitions[i] = Inherit(definitions[i], byKey, new HashSet<TypeMapKey>());
                }
            }
        }

        private static TypeMapDefinition Inherit(
            TypeMapDefinition definition,
            Dictionary<TypeMapKey, TypeMapDefinition> byKey,
            HashSet<TypeMapKey> visiting)
        {
            if (!visiting.Add(definition.Key))
            {
                throw new MapperConfigurationException(
                    definition.Key + " inherits from itself through IncludeBase.");
            }

            var members = new List<MemberDefinition>(definition.Members);

            foreach (TypeMapKey baseKey in definition.BaseMaps)
            {
                if (!byKey.TryGetValue(baseKey, out TypeMapDefinition? baseDefinition))
                {
                    throw new MapperConfigurationException(
                        definition.Key + " asks to inherit from " + baseKey +
                        ", which is not declared. Add CreateMap<" + baseKey.SourceType.Name + ", " +
                        baseKey.DestinationType.Name + ">().");
                }

                TypeMapDefinition resolved = baseDefinition!.BaseMaps.Count == 0
                    ? baseDefinition
                    : Inherit(baseDefinition, byKey, visiting);

                foreach (MemberDefinition inherited in resolved.Members)
                {
                    if (!IsConfigured(members, inherited.DestinationMember) &&
                        Fits(inherited, definition.DestinationType))
                    {
                        members.Add(inherited);
                    }
                }
            }

            visiting.Remove(definition.Key);

            return definition.WithResolved(
                members.ToArray(),
                definition.Constructor,
                definition.ConstructorParameters);
        }

        private static bool IsConfigured(List<MemberDefinition> members, MemberDescriptor destination)
        {
            foreach (MemberDefinition member in members)
            {
                if (member.DestinationMember.Equals(destination))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Fits(MemberDefinition inherited, Type destinationType)
        {
            Type declaring = inherited.DestinationMember.DeclaringType;
            return declaring.IsAssignableFrom(destinationType);
        }
    }
}
