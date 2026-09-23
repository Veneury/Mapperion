using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Mutable state gathered for one type pair while the configuration callback runs.
    /// </summary>
    internal interface ITypeMapConfiguration
    {
        TypeMapKey Key { get; }

        [RequiresUnreferencedCode("Resolving a member by name inspects types by reflection.")]
        TypeMapDefinition Build(MapperOptions options);
    }

    /// <summary>
    /// Whatever the maps are being declared into, so that a map can add its own reverse.
    /// </summary>
    internal interface ITypeMapRegistry
    {
        void Add(ITypeMapConfiguration configuration);
    }

    /// <inheritdoc cref="ITypeMapConfiguration" />
    internal sealed class TypeMapConfiguration<TSource, TDestination>
        : IMappingExpression<TSource, TDestination>, ITypeMapConfiguration
    {
        private readonly List<IMemberConfiguration> members = new List<IMemberConfiguration>();
        private readonly List<ICtorParamConfiguration> constructorParameters = new List<ICtorParamConfiguration>();
        private readonly ITypeMapRegistry registry;
        private MemberListValidation? validation;
        private Type? typeConverterType;
        private object? constructUsing;
        private readonly List<TypeMapKey> derivedMaps = new List<TypeMapKey>();
        private readonly List<TypeMapKey> baseMaps = new List<TypeMapKey>();
        private readonly List<object> beforeMapActions = new List<object>();
        private readonly List<object> afterMapActions = new List<object>();
        private int? maxDepth;
        private bool preserveReferences;

        internal TypeMapConfiguration(ITypeMapRegistry registry)
        {
            this.registry = registry;
        }

        public TypeMapKey Key { get; } = new TypeMapKey(typeof(TSource), typeof(TDestination));

        internal bool IsReverse { get; set; }

        public IMappingExpression<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
        {
            Guard.NotNull(destinationMember, nameof(destinationMember));
            Guard.NotNull(memberOptions, nameof(memberOptions));

            MemberDescriptor descriptor = MemberExpressionParser.ParseDestinationMember(destinationMember);
            MemberConfiguration<TSource, TDestination, TMember> configuration = FindOrAdd<TMember>(descriptor);

            memberOptions(configuration);
            return this;
        }

        public IMappingExpression<TSource, TDestination> ForPath<TMember>(
            Expression<Func<TDestination, TMember>> destinationPath,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> pathOptions)
        {
            Guard.NotNull(destinationPath, nameof(destinationPath));
            Guard.NotNull(pathOptions, nameof(pathOptions));

            MemberPath path = MemberExpressionParser.ParseDestinationPath(destinationPath);

            if (!path.IsFlattened)
            {
                return ForMember(destinationPath, pathOptions);
            }

            MemberConfiguration<TSource, TDestination, TMember> configuration = FindOrAddPath<TMember>(path);

            pathOptions(configuration);
            return this;
        }

        public IMappingExpression<TSource, TDestination> ForCtorParam(
            string constructorParameterName,
            Action<ICtorParamConfigurationExpression<TSource>> parameterOptions)
        {
            Guard.NotNull(constructorParameterName, nameof(constructorParameterName));
            Guard.NotNull(parameterOptions, nameof(parameterOptions));

            CtorParamConfiguration<TSource> configuration = FindOrAddParameter(constructorParameterName);
            parameterOptions(configuration);
            return this;
        }

        public IMappingExpression<TDestination, TSource> ReverseMap()
        {
            var reverse = new TypeMapConfiguration<TDestination, TSource>(registry) { IsReverse = true };

            foreach (IMemberConfiguration member in members)
            {
                MemberDefinition built = member.Build();

                if (built.IsIgnored ||
                    built.IsPath ||
                    !built.DestinationMember.CanRead ||
                    built.Source is not MemberPathSource path ||
                    path.Path.IsFlattened ||
                    !path.Path.Leaf.CanWrite)
                {
                    continue;
                }

                reverse.AddMember(new InvertedMemberConfiguration(
                    path.Path.Leaf,
                    new MemberPathSource(MemberPath.Of(built.DestinationMember))));
            }

            registry.Add(reverse);
            return reverse;
        }

        internal void AddMember(IMemberConfiguration member)
        {
            members.Add(member);
        }

        public IMappingExpression<TSource, TDestination> ConvertUsing<TTypeConverter>()
            where TTypeConverter : ITypeConverter<TSource, TDestination>
        {
            typeConverterType = typeof(TTypeConverter);
            return this;
        }

        public IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> factory)
        {
            constructUsing = Guard.NotNull(factory, nameof(factory));
            return this;
        }

        public IMappingExpression<TSource, TDestination> ConstructUsing(
            Func<TSource, ResolutionContext, TDestination> factory)
        {
            constructUsing = Guard.NotNull(factory, nameof(factory));
            return this;
        }

        public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action)
        {
            beforeMapActions.Add(Guard.NotNull(action, nameof(action)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> BeforeMap(
            Action<TSource, TDestination, ResolutionContext> action)
        {
            beforeMapActions.Add(Guard.NotNull(action, nameof(action)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> BeforeMap<TMappingAction>()
            where TMappingAction : IMappingAction<TSource, TDestination>
        {
            beforeMapActions.Add(typeof(TMappingAction));
            return this;
        }

        public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action)
        {
            afterMapActions.Add(Guard.NotNull(action, nameof(action)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> AfterMap(
            Action<TSource, TDestination, ResolutionContext> action)
        {
            afterMapActions.Add(Guard.NotNull(action, nameof(action)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> AfterMap<TMappingAction>()
            where TMappingAction : IMappingAction<TSource, TDestination>
        {
            afterMapActions.Add(typeof(TMappingAction));
            return this;
        }

        public IMappingExpression<TSource, TDestination> Include<TDerivedSource, TDerivedDestination>()
            where TDerivedSource : TSource
            where TDerivedDestination : TDestination
        {
            derivedMaps.Add(new TypeMapKey(typeof(TDerivedSource), typeof(TDerivedDestination)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
        {
            baseMaps.Add(new TypeMapKey(typeof(TBaseSource), typeof(TBaseDestination)));
            return this;
        }

        public IMappingExpression<TSource, TDestination> ValidateMemberList(MemberListValidation validation)
        {
            this.validation = validation;
            return this;
        }

        public IMappingExpression<TSource, TDestination> MaxDepth(int depth)
        {
            if (depth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "The maximum depth must be positive.");
            }

            maxDepth = depth;
            return this;
        }

        public IMappingExpression<TSource, TDestination> PreserveReferences()
        {
            preserveReferences = true;
            return this;
        }

        [RequiresUnreferencedCode("Resolving a member by name inspects types by reflection.")]
        public TypeMapDefinition Build(MapperOptions options)
        {
            if (constructUsing is not null && constructorParameters.Count != 0)
            {
                throw new MapperConfigurationException(
                    Key + ": the map builds its destination with ConstructUsing and also configures " +
                    "constructor parameters with ForCtorParam. The factory would win and the " +
                    "parameters would do nothing. Keep one of the two.");
            }

            var definitions = new MemberDefinition[members.Count];
            for (int i = 0; i < members.Count; i++)
            {
                definitions[i] = members[i].Build();
            }

            var parameters = new ConstructorParameterDefinition[constructorParameters.Count];
            for (int i = 0; i < constructorParameters.Count; i++)
            {
                ICtorParamConfiguration parameter = constructorParameters[i];
                parameters[i] = new ConstructorParameterDefinition(parameter.Name, typeof(object), i)
                {
                    Source = parameter.Source,
                    IsExplicit = true,
                };
            }

            return new TypeMapDefinition(Key)
            {
                Members = definitions,
                ConstructorParameters = parameters,
                MemberListValidation = validation ?? options.MemberListValidation,
                IsReverse = IsReverse,
                TypeConverterType = typeConverterType,
                ConstructUsing = constructUsing,
                DerivedMaps = derivedMaps.ToArray(),
                BaseMaps = baseMaps.ToArray(),
                BeforeMapActions = beforeMapActions.ToArray(),
                AfterMapActions = afterMapActions.ToArray(),
                MaxDepth = maxDepth,
                PreserveReferences = preserveReferences,
            };
        }

        private CtorParamConfiguration<TSource> FindOrAddParameter(string name)
        {
            for (int i = 0; i < constructorParameters.Count; i++)
            {
                if (string.Equals(constructorParameters[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (CtorParamConfiguration<TSource>)constructorParameters[i];
                }
            }

            var created = new CtorParamConfiguration<TSource>(name);
            constructorParameters.Add(created);
            return created;
        }

        private MemberConfiguration<TSource, TDestination, TMember> FindOrAdd<TMember>(MemberDescriptor descriptor)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].DestinationMember.Equals(descriptor))
                {
                    if (members[i] is MemberConfiguration<TSource, TDestination, TMember> existing)
                    {
                        return existing;
                    }

                    throw new MapperConfigurationException(
                        "Member '" + descriptor + "' was already configured with a different member type.");
                }
            }

            var created = new MemberConfiguration<TSource, TDestination, TMember>(descriptor);
            members.Add(created);
            return created;
        }

        private MemberConfiguration<TSource, TDestination, TMember> FindOrAddPath<TMember>(MemberPath path)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (path.Equals(members[i].DestinationPath))
                {
                    if (members[i] is MemberConfiguration<TSource, TDestination, TMember> existing)
                    {
                        return existing;
                    }

                    throw new MapperConfigurationException(
                        "Path '" + path + "' was already configured with a different member type.");
                }
            }

            var created = new MemberConfiguration<TSource, TDestination, TMember>(path.Leaf, path);
            members.Add(created);
            return created;
        }
    }
}
