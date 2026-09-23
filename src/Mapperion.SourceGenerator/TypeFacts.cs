using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Mapperion.SourceGeneration
{
    /// <summary>
    /// The questions the generator asks about a type, answered against Roslyn symbols rather than
    /// reflection. This is the compile-time half of what <c>TypeClassifier</c> does at run time.
    /// </summary>
    internal static class TypeFacts
    {
        internal static readonly SymbolDisplayFormat FullName =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        internal static string Display(ITypeSymbol type) => type.ToDisplayString(FullName);

        internal static string Bare(ITypeSymbol type) =>
            type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat);

        internal static bool Same(ITypeSymbol left, ITypeSymbol right) =>
            SymbolEqualityComparer.Default.Equals(
                left.WithNullableAnnotation(NullableAnnotation.None),
                right.WithNullableAnnotation(NullableAnnotation.None));

        internal static bool CanBeNull(ITypeSymbol type) =>
            type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        internal static ITypeSymbol? NullableUnderlying(ITypeSymbol type) =>
            type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? named.TypeArguments[0]
                : null;

        /// <summary>Every readable instance property, most derived first, one entry per name.</summary>
        internal static List<IPropertySymbol> Readable(ITypeSymbol type) =>
            Collect(type, p => p.GetMethod is not null && p.GetMethod.DeclaredAccessibility == Accessibility.Public);

        /// <summary>Every instance property that can be assigned from outside the type.</summary>
        internal static List<IPropertySymbol> Writable(ITypeSymbol type) =>
            Collect(type, p => p.SetMethod is not null && p.SetMethod.DeclaredAccessibility == Accessibility.Public);

        private static List<IPropertySymbol> Collect(ITypeSymbol type, System.Func<IPropertySymbol, bool> keep)
        {
            var found = new List<IPropertySymbol>();
            var seen = new HashSet<string>();

            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>())
                {
                    if (!property.IsStatic &&
                        property.Parameters.Length == 0 &&
                        keep(property) &&
                        seen.Add(property.Name))
                    {
                        found.Add(property);
                    }
                }
            }

            found.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            return found;
        }

        /// <summary>The element type when the type is a sequence, leaving strings out of it.</summary>
        internal static ITypeSymbol? ElementOf(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
            {
                return null;
            }

            if (type is IArrayTypeSymbol array)
            {
                return array.ElementType;
            }

            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return named.TypeArguments[0];
            }

            foreach (INamedTypeSymbol contract in type.AllInterfaces)
            {
                if (contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                {
                    return contract.TypeArguments[0];
                }
            }

            return null;
        }

        /// <summary>How a destination sequence should be materialised, or null when it is not one.</summary>
        internal static string? Materialiser(ITypeSymbol destination)
        {
            if (destination is IArrayTypeSymbol)
            {
                return "ToArray";
            }

            if (destination is not INamedTypeSymbol named || !named.IsGenericType)
            {
                return null;
            }

            string definition = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            switch (definition)
            {
                case "global::System.Collections.Generic.HashSet<T>":
                case "global::System.Collections.Generic.ISet<T>":
                    return "ToHashSet";

                case "global::System.Collections.Generic.List<T>":
                case "global::System.Collections.Generic.IList<T>":
                case "global::System.Collections.Generic.ICollection<T>":
                case "global::System.Collections.Generic.IEnumerable<T>":
                case "global::System.Collections.Generic.IReadOnlyList<T>":
                case "global::System.Collections.Generic.IReadOnlyCollection<T>":
                    return "ToList";

                default:
                    return null;
            }
        }

        internal static IMethodSymbol? Parameterless(INamedTypeSymbol type) =>
            type.InstanceConstructors.FirstOrDefault(c =>
                c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);

        internal static IMethodSymbol? SoleConstructor(INamedTypeSymbol type)
        {
            List<IMethodSymbol> usable = type.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
                .ToList();

            usable.Sort(static (left, right) => right.Parameters.Length.CompareTo(left.Parameters.Length));
            return usable.Count == 0 ? null : usable[0];
        }
    }
}
