using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Mapperion.Conventions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Validation
{
    /// <summary>
    /// Checks a built model and reports every problem it finds, never just the first one: a single
    /// startup run should surface the whole list.
    /// </summary>
    /// <remarks>
    /// A missing map is only reported when the destination is a complex type. Pairs whose
    /// destination is a simple value are left alone because the conversion rules that would handle
    /// them do not exist yet, and reporting them would raise false errors for pairs such as
    /// <c>int</c> to <c>long</c>.
    /// </remarks>
    [RequiresUnreferencedCode("Validation inspects types by reflection.")]
    internal static class ConfigurationValidator
    {
        private const int MaxElementDepth = 4;
        private const int MaxCycleDepth = 64;

        internal static IReadOnlyList<string> Validate(MapperModel model)
        {
            var errors = new List<string>();
            var members = new TypeMembers(model.Options);

            foreach (TypeMapDefinition map in model.TypeMaps)
            {
                if (map.HasTypeConverter)
                {
                    continue;
                }

                ValidateReferences(map, errors);
                ValidateHierarchy(map, model, errors);
                ValidateConstructor(map, model, errors);
                ValidateDestination(map, model, errors);

                if (map.MemberListValidation == MemberListValidation.Source)
                {
                    ValidateSource(map, members, errors);
                }
            }

            ValidateCycles(model, errors);

            return errors;
        }

        private static void ValidateCycles(MapperModel model, List<string> errors)
        {
            var reported = new HashSet<TypeMapKey>();

            foreach (TypeMapDefinition map in model.TypeMaps)
            {
                Walk(map.Key, model, new List<TypeMapKey>(), reported, errors);
            }
        }

        private static void Walk(
            TypeMapKey key,
            MapperModel model,
            List<TypeMapKey> path,
            HashSet<TypeMapKey> reported,
            List<string> errors)
        {
            int start = path.IndexOf(key);

            if (start >= 0)
            {
                ReportCycle(key, model, path, start, reported, errors);
                return;
            }

            if (path.Count > MaxCycleDepth || !model.TryGetTypeMap(key, out TypeMapDefinition? definition))
            {
                return;
            }

            path.Add(key);

            foreach (MemberDefinition member in definition!.Members)
            {
                if (!member.IsIgnored && member.Source is not null &&
                    TryResolveNested(member.Source.ValueType, member.DestinationMember.MemberType, model, out TypeMapKey nested))
                {
                    Walk(nested, model, path, reported, errors);
                }
            }

            foreach (ConstructorParameterDefinition parameter in definition.ConstructorParameters)
            {
                if (parameter.Source is not null &&
                    TryResolveNested(parameter.Source.ValueType, parameter.ParameterType, model, out TypeMapKey nested))
                {
                    Walk(nested, model, path, reported, errors);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private static void ReportCycle(
            TypeMapKey key,
            MapperModel model,
            List<TypeMapKey> path,
            int start,
            HashSet<TypeMapKey> reported,
            List<string> errors)
        {
            for (int i = start; i < path.Count; i++)
            {
                if (model.TryGetTypeMap(path[i], out TypeMapDefinition? guard) &&
                    (guard!.MaxDepth is not null || guard.PreserveReferences))
                {
                    return;
                }
            }

            if (!reported.Add(key))
            {
                return;
            }

            var cycle = new StringBuilder();

            for (int i = start; i < path.Count; i++)
            {
                cycle.Append(path[i]).Append(" -> ");
            }

            cycle.Append(key);

            errors.Add(
                "The maps " + cycle + " form a cycle with nothing to stop it. Mapping an object " +
                "graph that loops would recurse until the stack runs out. Add MaxDepth or " +
                "PreserveReferences to one of them, or ignore the member that closes the cycle.");
        }

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

        internal static string BuildMessage(IReadOnlyList<string> errors)
        {
            var text = new StringBuilder();
            text.Append("The mapper configuration is not valid. ")
                .Append(errors.Count)
                .Append(errors.Count == 1 ? " problem was found:" : " problems were found:");

            for (int i = 0; i < errors.Count; i++)
            {
                text.AppendLine().Append("  ").Append(i + 1).Append(". ").Append(errors[i]);
            }

            return text.ToString();
        }

        private static void ValidateHierarchy(TypeMapDefinition map, MapperModel model, List<string> errors)
        {
            foreach (TypeMapKey derived in map.DerivedMaps)
            {
                if (!model.Contains(derived))
                {
                    errors.Add(
                        map.Key + " includes " + derived + ", which is not declared. Add CreateMap<" +
                        derived.SourceType.Name + ", " + derived.DestinationType.Name + ">().");
                    continue;
                }

                if (!map.DestinationType.IsAssignableFrom(derived.DestinationType))
                {
                    errors.Add(
                        map.Key + " includes " + derived + ", but " + derived.DestinationType.Name +
                        " does not derive from " + map.DestinationType.Name +
                        ", so it could not be returned in its place.");
                }
            }

            foreach (TypeMapKey baseKey in map.BaseMaps)
            {
                if (!model.Contains(baseKey))
                {
                    errors.Add(
                        map.Key + " inherits from " + baseKey + ", which is not declared. Add CreateMap<" +
                        baseKey.SourceType.Name + ", " + baseKey.DestinationType.Name + ">().");
                }
            }
        }

        private static void ValidateReferences(TypeMapDefinition map, List<string> errors)
        {
            if (!map.PreserveReferences)
            {
                return;
            }

            if (map.SourceType.IsValueType || map.DestinationType.IsValueType)
            {
                errors.Add(
                    map.Key + ": PreserveReferences needs both sides to be reference types. A value " +
                    "type has no identity to preserve, so the option would do nothing.");
            }
        }

        private static void ValidateConstructor(TypeMapDefinition map, MapperModel model, List<string> errors)
        {
            foreach (ConstructorParameterDefinition parameter in map.ConstructorParameters)
            {
                if (parameter.Source is null)
                {
                    if (!parameter.HasDefaultValue)
                    {
                        errors.Add(
                            map.Key + ": constructor parameter '" + parameter.Name +
                            "' has no source. Map it with ForCtorParam.");
                    }

                    continue;
                }

                if (NeedsMap(
                        parameter.Source.ValueType,
                        parameter.ParameterType,
                        model,
                        0,
                        out Type missingSource,
                        out Type missingDestination))
                {
                    errors.Add(
                        map.Key + ": constructor parameter '" + parameter.Name + "' needs a map from '" +
                        missingSource.Name + "' to '" + missingDestination.Name + "'. Declare it with CreateMap<" +
                        missingSource.Name + ", " + missingDestination.Name + ">().");
                }
            }
        }

        private static void ValidateDestination(TypeMapDefinition map, MapperModel model, List<string> errors)
        {
            foreach (MemberDefinition member in map.Members)
            {
                if (member.IsIgnored)
                {
                    continue;
                }

                if (member.Source is null)
                {
                    if (map.MemberListValidation != MemberListValidation.None)
                    {
                        errors.Add(
                            map.Key + ": destination member '" + member.DestinationMember.Name +
                            "' has no source. Map it with ForMember, or ignore it.");
                    }

                    continue;
                }

                if (member.ValueConverterType is not null)
                {
                    continue;
                }

                ReportMissingMap(map, member, model, errors);
            }
        }

        private static void ReportMissingMap(
            TypeMapDefinition map,
            MemberDefinition member,
            MapperModel model,
            List<string> errors)
        {
            Type source = member.Source!.ValueType;
            Type destination = member.DestinationMember.MemberType;

            if (NeedsMap(source, destination, model, 0, out Type missingSource, out Type missingDestination))
            {
                errors.Add(
                    map.Key + ": member '" + member.DestinationMember.Name + "' needs a map from '" +
                    missingSource.Name + "' to '" + missingDestination.Name + "'. Declare it with CreateMap<" +
                    missingSource.Name + ", " + missingDestination.Name + ">().");
            }
        }

        private static bool NeedsMap(
            Type source,
            Type destination,
            MapperModel model,
            int depth,
            out Type missingSource,
            out Type missingDestination)
        {
            missingSource = source;
            missingDestination = destination;

            Type actualSource = TypeClassifier.Underlying(source);
            Type actualDestination = TypeClassifier.Underlying(destination);

            if (actualDestination.IsAssignableFrom(actualSource))
            {
                return false;
            }

            if (model.Contains(new TypeMapKey(actualSource, actualDestination)))
            {
                return false;
            }

            if (depth < MaxElementDepth &&
                TypeClassifier.TryGetDictionaryTypes(actualSource, out Type? sourceKey, out Type? sourceValue) &&
                TypeClassifier.TryGetDictionaryTypes(actualDestination, out Type? destinationKey, out Type? destinationValue))
            {
                return NeedsMap(sourceKey, destinationKey, model, depth + 1, out missingSource, out missingDestination)
                    || NeedsMap(sourceValue, destinationValue, model, depth + 1, out missingSource, out missingDestination);
            }

            if (depth < MaxElementDepth &&
                TypeClassifier.TryGetElementType(actualSource, out Type? sourceElement) &&
                TypeClassifier.TryGetElementType(actualDestination, out Type? destinationElement))
            {
                return NeedsMap(
                    sourceElement,
                    destinationElement,
                    model,
                    depth + 1,
                    out missingSource,
                    out missingDestination);
            }

            if (TypeClassifier.IsSimple(actualDestination))
            {
                return false;
            }

            missingSource = actualSource;
            missingDestination = actualDestination;
            return true;
        }

        private static void ValidateSource(TypeMapDefinition map, TypeMembers members, List<string> errors)
        {
            var consumed = new HashSet<MemberDescriptor>();

            foreach (MemberDefinition member in map.Members)
            {
                if (member.IsIgnored)
                {
                    continue;
                }

                if (member.Source is MemberPathSource path)
                {
                    consumed.Add(path.Path.Steps[0]);
                }
            }

            foreach (MemberDescriptor candidate in members.Readable(map.SourceType))
            {
                if (!consumed.Contains(candidate))
                {
                    errors.Add(
                        map.Key + ": source member '" + candidate.Name +
                        "' is not used by any destination member.");
                }
            }
        }
    }
}
