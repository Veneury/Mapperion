using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// A map declared with types instead of type arguments. When both sides are generic type
    /// definitions it is a template the engine closes the first time a matching pair is mapped.
    /// </summary>
    internal sealed class OpenTypeMapConfiguration : IOpenMappingExpression, ITypeMapConfiguration
    {
        private readonly List<string> ignored = new List<string>();
        private MemberListValidation? validation;
        private int? maxDepth;
        private bool preserveReferences;

        internal OpenTypeMapConfiguration(Type sourceType, Type destinationType)
        {
            Key = new TypeMapKey(sourceType, destinationType);

            if (sourceType.IsGenericTypeDefinition != destinationType.IsGenericTypeDefinition)
            {
                throw new MapperConfigurationException(
                    Key + " mixes an open generic type with a closed one. Either both sides are " +
                    "generic type definitions, making a template, or neither is.");
            }
        }

        public TypeMapKey Key { get; }

        public IOpenMappingExpression IgnoreMember(string destinationMemberName)
        {
            ignored.Add(Guard.NotNull(destinationMemberName, nameof(destinationMemberName)));
            return this;
        }

        public IOpenMappingExpression ValidateMemberList(MemberListValidation validation)
        {
            this.validation = validation;
            return this;
        }

        public IOpenMappingExpression MaxDepth(int depth)
        {
            if (depth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "The maximum depth must be positive.");
            }

            maxDepth = depth;
            return this;
        }

        public IOpenMappingExpression PreserveReferences()
        {
            preserveReferences = true;
            return this;
        }

        [RequiresUnreferencedCode("Resolving a member by name inspects types by reflection.")]
        public TypeMapDefinition Build(MapperOptions options)
        {
            return new TypeMapDefinition(Key)
            {
                Members = BuildIgnored(),
                MemberListValidation = validation ?? options.MemberListValidation,
                MaxDepth = maxDepth,
                PreserveReferences = preserveReferences,
            };
        }

        [RequiresUnreferencedCode("Resolving a member by name inspects types by reflection.")]
        private MemberDefinition[] BuildIgnored()
        {
            if (ignored.Count == 0)
            {
                return Array.Empty<MemberDefinition>();
            }

            var members = new List<MemberDefinition>(ignored.Count);

            foreach (string name in ignored)
            {
                MemberDescriptor? descriptor = Find(name);

                if (descriptor is null)
                {
                    throw new MapperConfigurationException(
                        Key + " has no destination member named '" + name + "' to ignore.");
                }

                members.Add(new MemberDefinition(descriptor) { IsIgnored = true, IsExplicit = true });
            }

            return members.ToArray();
        }

        [RequiresUnreferencedCode("Resolving a member by name inspects types by reflection.")]
        private MemberDescriptor? Find(string name)
        {
            System.Reflection.PropertyInfo? property = Key.DestinationType.GetProperty(name);

            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return MemberDescriptor.ForProperty(property);
            }

            System.Reflection.FieldInfo? field = Key.DestinationType.GetField(name);
            return field is null ? null : MemberDescriptor.ForField(field);
        }
    }
}
