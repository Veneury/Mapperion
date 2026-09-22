using System;
using System.Collections.Generic;
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

        public TypeMapDefinition Build(MapperOptions options)
        {
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
    }
}
