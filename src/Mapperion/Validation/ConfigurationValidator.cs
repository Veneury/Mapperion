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

        internal static IReadOnlyList<string> Validate(MapperModel model)
        {
            var errors = new List<string>();
            var members = new TypeMembers(model.Options);

            foreach (TypeMapDefinition map in model.TypeMaps)
            {
                if (ConventionResolver.IsTemplate(map))
                {
                    ValidateTemplate(map, errors);
                    continue;
                }

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

        private static void ValidateTemplate(TypeMapDefinition map, List<string> errors)
        {
            int source = map.SourceType.GetGenericArguments().Length;
            int destination = map.DestinationType.GetGenericArguments().Length;

            if (source != destination)
            {
                errors.Add(
                    map.Key + ": the template takes " + source + " type argument(s) on the source and " +
                    destination + " on the destination. Closing it would have nothing to close with.");
            }
        }

        private static void ValidateCycles(MapperModel model, List<string> errors)
        {
            foreach (TypeMapKey[] cycle in CycleFinder.Unguarded(model))
            {
                var text = new StringBuilder();

                for (int i = 0; i < cycle.Length - 1; i++)
                {
                    text.Append(cycle[i]).Append(" -> ");
                }

                text.Append(cycle[cycle.Length - 1]);

                errors.Add(
                    "The maps " + text + " form a cycle with nothing to stop it. Mapping an object " +
                    "graph that loops would recurse until it hits the recursion limit and then " +
                    "fail. Add MaxDepth or PreserveReferences to one of them, or ignore the member " +
                    "that closes the cycle.");
            }
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
