using Microsoft.CodeAnalysis;

namespace Mapperion.Analysis
{
    /// <summary>
    /// The handful of Mapperion types the analyser recognises, looked up once per compilation.
    /// </summary>
    /// <remarks>
    /// Everything is matched by symbol rather than by name. A method called <c>ForMember</c> on
    /// somebody else's fluent builder is not this one, and an analyser that went by spelling would
    /// report on code it knows nothing about.
    /// </remarks>
    internal sealed class KnownTypes
    {
        private KnownTypes(
            INamedTypeSymbol configuration,
            INamedTypeSymbol mapping,
            INamedTypeSymbol memberOptions,
            INamedTypeSymbol profile)
        {
            Configuration = configuration;
            Mapping = mapping;
            MemberOptions = memberOptions;
            Profile = profile;
        }

        internal INamedTypeSymbol Configuration { get; }

        internal INamedTypeSymbol Mapping { get; }

        internal INamedTypeSymbol MemberOptions { get; }

        internal INamedTypeSymbol Profile { get; }

        /// <summary>
        /// Returns the types, or <see langword="null"/> when the compilation does not reference
        /// Mapperion at all and there is nothing here to analyse.
        /// </summary>
        internal static KnownTypes? From(Compilation compilation)
        {
            INamedTypeSymbol? configuration = compilation.GetTypeByMetadataName("Mapperion.IMapperConfigurationExpression");
            INamedTypeSymbol? mapping = compilation.GetTypeByMetadataName("Mapperion.IMappingExpression`2");
            INamedTypeSymbol? memberOptions = compilation.GetTypeByMetadataName("Mapperion.IMemberConfigurationExpression`3");
            INamedTypeSymbol? profile = compilation.GetTypeByMetadataName("Mapperion.Profile");

            return configuration is null || mapping is null || memberOptions is null || profile is null
                ? null
                : new KnownTypes(configuration, mapping, memberOptions, profile);
        }

        internal bool IsMapping(ISymbol? containing) =>
            containing is INamedTypeSymbol type && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, Mapping);

        /// <summary>
        /// Whether a member belongs to one of the two places a map is declared from: the callback's
        /// expression, or <c>Profile</c> itself, which declares its own <c>CreateMap</c> rather
        /// than implementing the interface.
        /// </summary>
        internal bool IsConfiguration(ISymbol? containing) =>
            containing is INamedTypeSymbol type &&
            (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, Configuration) ||
             SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, Profile));

        internal bool IsMemberOptions(ISymbol? containing) =>
            containing is INamedTypeSymbol type && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, MemberOptions);

        internal bool IsProfile(INamedTypeSymbol? type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, Profile))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
