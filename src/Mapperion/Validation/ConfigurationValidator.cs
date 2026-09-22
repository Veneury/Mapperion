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
                if (map.HasTypeConverter)
                {
                    continue;
                }

                ValidateDestination(map, model, errors);

                if (map.MemberListValidation == MemberListValidation.Source)
                {
                    ValidateSource(map, members, errors);
                }
            }

            return errors;
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
