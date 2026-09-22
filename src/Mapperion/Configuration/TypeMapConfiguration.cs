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

        TypeMapDefinition Build();
    }

    /// <inheritdoc cref="ITypeMapConfiguration" />
    internal sealed class TypeMapConfiguration<TSource, TDestination>
        : IMappingExpression<TSource, TDestination>, ITypeMapConfiguration
    {
        private readonly List<IMemberConfiguration> members = new List<IMemberConfiguration>();
        private MemberListValidation validation = MemberListValidation.Destination;
        private int? maxDepth;
        private bool preserveReferences;

        public TypeMapKey Key { get; } = new TypeMapKey(typeof(TSource), typeof(TDestination));

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

        public TypeMapDefinition Build()
        {
            var definitions = new MemberDefinition[members.Count];
            for (int i = 0; i < members.Count; i++)
            {
                definitions[i] = members[i].Build();
            }

            return new TypeMapDefinition(Key)
            {
                Members = definitions,
                MemberListValidation = validation,
                MaxDepth = maxDepth,
                PreserveReferences = preserveReferences,
            };
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
