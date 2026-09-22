using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// Chooses the constructor used to build a destination that cannot be created empty, and works
    /// out where each of its arguments comes from.
    /// </summary>
    /// <remarks>
    /// Parameter names are always matched ignoring case. C# names constructor parameters in
    /// camelCase and properties in PascalCase, so an exact comparison would never match a record's
    /// primary constructor against its source properties.
    /// </remarks>
    [RequiresUnreferencedCode("Selecting a constructor inspects types by reflection.")]
    internal sealed class ConstructorSelector
    {
        private static readonly ConstructorParameterDefinition[] None =
            Array.Empty<ConstructorParameterDefinition>();

        private readonly MemberMatcher matcher;

        internal ConstructorSelector(MemberMatcher matcher)
        {
            this.matcher = matcher;
        }

        internal static bool IsNeeded(TypeMapDefinition definition)
        {
            if (definition.ConstructorParameters.Count != 0)
            {
                return true;
            }

            Type destination = definition.DestinationType;

            return !destination.IsValueType
                && !destination.IsAbstract
                && destination.GetConstructor(Type.EmptyTypes) is null;
        }

        internal ConstructorInfo? Select(
            TypeMapDefinition definition,
            out IReadOnlyList<ConstructorParameterDefinition> parameters)
        {
            ConstructorInfo? best = null;
            ConstructorParameterDefinition[] bestParameters = None;
            int bestUnresolved = int.MaxValue;

            foreach (ConstructorInfo candidate in Candidates(definition.DestinationType))
            {
                ConstructorParameterDefinition[] resolved = Resolve(definition, candidate);
                int unresolved = CountUnresolved(resolved);

                if (best is null ||
                    unresolved < bestUnresolved ||
                    (unresolved == bestUnresolved && resolved.Length > bestParameters.Length))
                {
                    best = candidate;
                    bestParameters = resolved;
                    bestUnresolved = unresolved;
                }

                if (bestUnresolved == 0)
                {
                    break;
                }
            }

            parameters = bestParameters;
            return best;
        }

        private static List<ConstructorInfo> Candidates(Type destinationType)
        {
            var candidates = new List<ConstructorInfo>();

            foreach (ConstructorInfo constructor in destinationType.GetConstructors())
            {
                if (constructor.GetParameters().Length != 0)
                {
                    candidates.Add(constructor);
                }
            }

            candidates.Sort(static (left, right) =>
                right.GetParameters().Length.CompareTo(left.GetParameters().Length));

            return candidates;
        }

        private static int CountUnresolved(ConstructorParameterDefinition[] parameters)
        {
            int unresolved = 0;

            foreach (ConstructorParameterDefinition parameter in parameters)
            {
                if (parameter.Source is null && !parameter.HasDefaultValue)
                {
                    unresolved++;
                }
            }

            return unresolved;
        }

        private ConstructorParameterDefinition[] Resolve(
            TypeMapDefinition definition,
            ConstructorInfo constructor)
        {
            ParameterInfo[] declared = constructor.GetParameters();
            var resolved = new ConstructorParameterDefinition[declared.Length];

            for (int i = 0; i < declared.Length; i++)
            {
                ParameterInfo parameter = declared[i];
                ConstructorParameterDefinition? configured = FindConfigured(definition, parameter.Name!);
                MemberSource? source = configured?.Source;

                if (source is null)
                {
                    MemberPath? path = matcher.Match(definition.SourceType, parameter.Name!);

                    if (path is not null)
                    {
                        source = new MemberPathSource(path);
                    }
                }

                resolved[i] = new ConstructorParameterDefinition(parameter.Name!, parameter.ParameterType, i)
                {
                    Source = source,
                    IsExplicit = configured is not null,
                    HasDefaultValue = parameter.HasDefaultValue,
                    DefaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null,
                };
            }

            return resolved;
        }

        private static ConstructorParameterDefinition? FindConfigured(TypeMapDefinition definition, string name)
        {
            foreach (ConstructorParameterDefinition parameter in definition.ConstructorParameters)
            {
                if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return parameter;
                }
            }

            return null;
        }
    }
}
