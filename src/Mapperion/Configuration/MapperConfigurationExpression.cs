using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Conventions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// The implementation handed to the user's configuration callback. Mutable while the callback
    /// runs, then turned into an immutable <see cref="MapperModel"/>.
    /// </summary>
    internal sealed class MapperConfigurationExpression : IMapperConfigurationExpression, ITypeMapRegistry
    {
        private readonly List<ITypeMapConfiguration> typeMaps = new List<ITypeMapConfiguration>();
        private readonly List<string> sourcePrefixes = new List<string>();
        private readonly List<string> sourcePostfixes = new List<string>();
        private readonly List<string> destinationPrefixes = new List<string>();
        private readonly List<string> destinationPostfixes = new List<string>();

        public MemberNameComparison NameComparison { get; set; } = MapperOptions.Defaults.NameComparison;

        public int MaxFlatteningDepth { get; set; } = MapperOptions.Defaults.MaxFlatteningDepth;

        public bool AllowNullCollections { get; set; } = MapperOptions.Defaults.AllowNullCollections;

        public bool AllowNullDestinationValues { get; set; } = MapperOptions.Defaults.AllowNullDestinationValues;

        public bool IncludeFields { get; set; } = MapperOptions.Defaults.IncludeFields;

        public bool IncludeSourceMethods { get; set; } = MapperOptions.Defaults.IncludeSourceMethods;

        public EnumMappingPolicy EnumMapping { get; set; } = MapperOptions.Defaults.EnumMapping;

        public MemberListValidation MemberListValidation { get; set; } = MapperOptions.Defaults.MemberListValidation;

        public bool ValidateOnBuild { get; set; } = MapperOptions.Defaults.ValidateOnBuild;

        public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var key = new TypeMapKey(typeof(TSource), typeof(TDestination));
            EnsureNotDeclared(key);

            var configuration = new TypeMapConfiguration<TSource, TDestination>(this);
            typeMaps.Add(configuration);
            return configuration;
        }

        void ITypeMapRegistry.Add(ITypeMapConfiguration configuration)
        {
            EnsureNotDeclared(configuration.Key);
            typeMaps.Add(configuration);
        }

        public void AddProfile(Profile profile)
        {
            Guard.NotNull(profile, nameof(profile));

            foreach (ITypeMapConfiguration configuration in profile.TypeMaps)
            {
                EnsureNotDeclared(configuration.Key);
                typeMaps.Add(configuration);
            }
        }

        public void AddProfile<TProfile>()
            where TProfile : Profile, new()
        {
            AddProfile(new TProfile());
        }

        [RequiresUnreferencedCode("Scanning assemblies for profiles is not compatible with trimming. Register profiles explicitly with AddProfile, or use the source generator.")]
        public void AddProfiles(params Assembly[] assemblies)
        {
            Guard.NotNull(assemblies, nameof(assemblies));

            foreach (Assembly assembly in assemblies)
            {
                Guard.NotNull(assembly, nameof(assemblies));

                foreach (Type type in assembly.GetTypes())
                {
                    if (IsRegistrableProfile(type))
                    {
                        AddProfile((Profile)Activator.CreateInstance(type)!);
                    }
                }
            }
        }

        public void RecognizeSourcePrefixes(params string[] prefixes) => Add(sourcePrefixes, prefixes);

        public void RecognizeSourcePostfixes(params string[] postfixes) => Add(sourcePostfixes, postfixes);

        public void RecognizeDestinationPrefixes(params string[] prefixes) => Add(destinationPrefixes, prefixes);

        public void RecognizeDestinationPostfixes(params string[] postfixes) => Add(destinationPostfixes, postfixes);

        [RequiresUnreferencedCode("Building the model resolves members by convention, which uses reflection.")]
        internal MapperModel BuildModel()
        {
            MapperOptions options = BuildOptions();
            var resolver = new ConventionResolver(options);

            var definitions = new TypeMapDefinition[typeMaps.Count];
            for (int i = 0; i < typeMaps.Count; i++)
            {
                definitions[i] = resolver.Complete(typeMaps[i].Build(options));
            }

            return new MapperModel(options, definitions);
        }

        [RequiresUnreferencedCode("Looking for a parameterless constructor is not compatible with trimming.")]
        private static bool IsRegistrableProfile(Type type)
        {
            return typeof(Profile).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && type.GetConstructor(Type.EmptyTypes) is not null;
        }

        private void EnsureNotDeclared(TypeMapKey key)
        {
            for (int i = 0; i < typeMaps.Count; i++)
            {
                if (typeMaps[i].Key == key)
                {
                    throw new MapperConfigurationException("The map " + key + " was already declared.");
                }
            }
        }

        private static void Add(List<string> target, string[] values)
        {
            Guard.NotNull(values, nameof(values));

            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    target.Add(value);
                }
            }
        }

        private MapperOptions BuildOptions()
        {
            return new MapperOptions
            {
                NameComparison = NameComparison,
                MaxFlatteningDepth = MaxFlatteningDepth,
                AllowNullCollections = AllowNullCollections,
                AllowNullDestinationValues = AllowNullDestinationValues,
                IncludeFields = IncludeFields,
                IncludeSourceMethods = IncludeSourceMethods,
                EnumMapping = EnumMapping,
                MemberListValidation = MemberListValidation,
                ValidateOnBuild = ValidateOnBuild,
                SourcePrefixes = sourcePrefixes.ToArray(),
                SourcePostfixes = sourcePostfixes.ToArray(),
                DestinationPrefixes = destinationPrefixes.ToArray(),
                DestinationPostfixes = destinationPostfixes.ToArray(),
            };
        }
    }
}
