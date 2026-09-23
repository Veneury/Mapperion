using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Conventions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Validation
{
    /// <summary>
    /// Walks the maps as a graph and finds the loops that nothing stops.
    /// </summary>
    /// <remarks>
    /// Two callers need the same answer for different reasons. The validator turns each loop into
    /// a message so the problem can be fixed. The compiler puts a ceiling on the map that closes
    /// each loop, so a graph that does loop fails with an exception the caller can catch instead of
    /// recursing until the stack runs out, which takes the process with it.
    /// </remarks>
    [RequiresUnreferencedCode("Walking the maps inspects types by reflection.")]
    internal static class CycleFinder
    {
        private const int MaxLength = 64;

        /// <summary>
        /// Finds every loop in the configuration that has neither a depth limit nor reference
        /// tracking anywhere along it. Each result lists the maps that form the loop, starting and
        /// ending with the one that closes it.
        /// </summary>
        internal static List<TypeMapKey[]> Unguarded(MapperModel model)
        {
            var found = new List<TypeMapKey[]>();
            var closed = new HashSet<TypeMapKey>();
            var path = new List<TypeMapKey>();

            foreach (TypeMapDefinition map in model.TypeMaps)
            {
                if (!ConventionResolver.IsTemplate(map))
                {
                    Walk(map.Key, model, path, closed, found);
                }
            }

            return found;
        }

        /// <summary>
        /// Returns the maps that close an unguarded loop. Every path around a loop passes through
        /// the map that closes it, so counting there is enough to bound the whole loop.
        /// </summary>
        internal static HashSet<TypeMapKey> Closing(MapperModel model)
        {
            var closing = new HashSet<TypeMapKey>();

            foreach (TypeMapKey[] cycle in Unguarded(model))
            {
                closing.Add(cycle[cycle.Length - 1]);
            }

            return closing;
        }

        private static void Walk(
            TypeMapKey key,
            MapperModel model,
            List<TypeMapKey> path,
            HashSet<TypeMapKey> closed,
            List<TypeMapKey[]> found)
        {
            int start = path.IndexOf(key);

            if (start >= 0)
            {
                Collect(key, model, path, start, closed, found);
                return;
            }

            if (path.Count > MaxLength || !model.TryGetTypeMap(key, out TypeMapDefinition? definition))
            {
                return;
            }

            path.Add(key);

            foreach (MemberDefinition member in definition!.Members)
            {
                if (!member.IsIgnored && member.Source is not null &&
                    TryResolveNested(member.Source.ValueType, member.DestinationMember.MemberType, model, out TypeMapKey nested))
                {
                    Walk(nested, model, path, closed, found);
                }
            }

            foreach (ConstructorParameterDefinition parameter in definition.ConstructorParameters)
            {
                if (parameter.Source is not null &&
                    TryResolveNested(parameter.Source.ValueType, parameter.ParameterType, model, out TypeMapKey nested))
                {
                    Walk(nested, model, path, closed, found);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private static void Collect(
            TypeMapKey key,
            MapperModel model,
            List<TypeMapKey> path,
            int start,
            HashSet<TypeMapKey> closed,
            List<TypeMapKey[]> found)
        {
            for (int i = start; i < path.Count; i++)
            {
                if (model.TryGetTypeMap(path[i], out TypeMapDefinition? guard) &&
                    (guard!.MaxDepth is not null || guard.PreserveReferences))
                {
                    return;
                }
            }

            if (!closed.Add(key))
            {
                return;
            }

            var cycle = new TypeMapKey[path.Count - start + 1];

            for (int i = start; i < path.Count; i++)
            {
                cycle[i - start] = path[i];
            }

            cycle[cycle.Length - 1] = key;
            found.Add(cycle);
        }

        /// <summary>
        /// Finds the map a member would reach, looking through nullables, collections and the value
        /// side of a dictionary, since a loop that runs through a list is still a loop.
        /// </summary>
        private static bool TryResolveNested(
            Type source,
            Type destination,
            MapperModel model,
            out TypeMapKey key)
        {
            Type actualSource = TypeClassifier.Underlying(source);
            Type actualDestination = TypeClassifier.Underlying(destination);

            if (TypeClassifier.TryGetDictionaryTypes(actualSource, out _, out Type? sourceValue) &&
                TypeClassifier.TryGetDictionaryTypes(actualDestination, out _, out Type? destinationValue))
            {
                actualSource = sourceValue;
                actualDestination = destinationValue;
            }
            else if (TypeClassifier.TryGetElementType(actualSource, out Type? sourceElement) &&
                TypeClassifier.TryGetElementType(actualDestination, out Type? destinationElement))
            {
                actualSource = sourceElement;
                actualDestination = destinationElement;
            }

            key = new TypeMapKey(actualSource, actualDestination);
            return model.Contains(key);
        }
    }
}
