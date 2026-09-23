using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Model;

namespace Mapperion
{
    /// <summary>
    /// The surface handed to the configuration callback. Every setting here applies to all maps
    /// unless a single map overrides it.
    /// </summary>
    public interface IMapperConfigurationExpression
    {
        /// <summary>Declares a map between two types.</summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <returns>The expression used to configure the map.</returns>
        /// <exception cref="MapperConfigurationException">The pair was already declared.</exception>
        IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();

        /// <summary>
        /// Declares a map with types rather than type arguments. Pass two generic type definitions,
        /// such as <c>typeof(Page&lt;&gt;)</c> and <c>typeof(PageDto&lt;&gt;)</c>, to declare a
        /// template the engine closes the first time a matching pair is mapped.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The expression used to configure the map.</returns>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException">The pair was already declared, or only one side is open.</exception>
        IOpenMappingExpression CreateMap(Type sourceType, Type destinationType);

        /// <summary>Registers an already-built profile.</summary>
        /// <param name="profile">The profile whose maps are added.</param>
        /// <exception cref="MapperConfigurationException">One of its pairs was already declared.</exception>
        void AddProfile(Profile profile);

        /// <summary>Registers a profile by type.</summary>
        /// <typeparam name="TProfile">The profile type, which needs a parameterless constructor.</typeparam>
        /// <exception cref="MapperConfigurationException">One of its pairs was already declared.</exception>
        void AddProfile<TProfile>()
            where TProfile : Profile, new();

        /// <summary>
        /// Registers every concrete <see cref="Profile"/> found in the given assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <exception cref="MapperConfigurationException">Two profiles declare the same pair.</exception>
        [RequiresUnreferencedCode("Scanning assemblies for profiles is not compatible with trimming. Register profiles explicitly with AddProfile, or use the source generator.")]
        void AddProfiles(params Assembly[] assemblies);

        /// <summary>Gets or sets how member names are compared when matching by convention.</summary>
        MemberNameComparison NameComparison { get; set; }

        /// <summary>Gets or sets how many levels the flattening convention walks.</summary>
        int MaxFlatteningDepth { get; set; }

        /// <summary>Gets or sets a value indicating whether a null source collection maps to null.</summary>
        bool AllowNullCollections { get; set; }

        /// <summary>Gets or sets a value indicating whether null values are written to the destination.</summary>
        bool AllowNullDestinationValues { get; set; }

        /// <summary>
        /// Gets or sets how many levels deep a map that can reach itself may recurse before the
        /// operation fails with <see cref="RecursionLimitException"/>. Zero or less removes the
        /// ceiling, which brings back an uncatchable stack overflow on a looping graph.
        /// </summary>
        int RecursionLimit { get; set; }

        /// <summary>Gets or sets a value indicating whether public fields are considered alongside properties.</summary>
        bool IncludeFields { get; set; }

        /// <summary>Gets or sets a value indicating whether parameterless methods are considered as source members.</summary>
        bool IncludeSourceMethods { get; set; }

        /// <summary>Gets or sets how enum values are translated.</summary>
        EnumMappingPolicy EnumMapping { get; set; }

        /// <summary>Gets or sets which side must be fully covered for a map to validate.</summary>
        MemberListValidation MemberListValidation { get; set; }

        /// <summary>Gets or sets a value indicating whether the configuration is validated when built.</summary>
        bool ValidateOnBuild { get; set; }

        /// <summary>Strips these prefixes from source member names before matching.</summary>
        /// <param name="prefixes">The prefixes, such as <c>Get</c>.</param>
        void RecognizeSourcePrefixes(params string[] prefixes);

        /// <summary>Strips these suffixes from source member names before matching.</summary>
        /// <param name="postfixes">The suffixes.</param>
        void RecognizeSourcePostfixes(params string[] postfixes);

        /// <summary>Strips these prefixes from destination member names before matching.</summary>
        /// <param name="prefixes">The prefixes.</param>
        void RecognizeDestinationPrefixes(params string[] prefixes);

        /// <summary>Strips these suffixes from destination member names before matching.</summary>
        /// <param name="postfixes">The suffixes, such as <c>Dto</c>.</param>
        void RecognizeDestinationPostfixes(params string[] postfixes);
    }
}
