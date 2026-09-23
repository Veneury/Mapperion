using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Mapperion.Conventions;
using Mapperion.Model;
using Mapperion.Projection;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Owns the compiled plans. A plan is built the first time its type pair is mapped and reused
    /// from then on; nested maps are resolved here at run time rather than inlined, which is what
    /// lets two maps reference each other without the compiler recursing forever.
    /// </summary>
    [RequiresUnreferencedCode("Compiling a map inspects types by reflection.")]
    [RequiresDynamicCode("Compiling a map emits code at run time.")]
    internal sealed class MapperEngine
    {
        private readonly ConcurrentDictionary<TypeMapKey, MapPlan> plans =
            new ConcurrentDictionary<TypeMapKey, MapPlan>();

        private readonly ConcurrentDictionary<TypeMapKey, LambdaExpression> projections =
            new ConcurrentDictionary<TypeMapKey, LambdaExpression>();

        private readonly Dictionary<TypeMapKey, TypeMapDefinition> closedTemplates =
            new Dictionary<TypeMapKey, TypeMapDefinition>();

        private readonly object closing = new object();
        private readonly ConventionResolver resolver;
        private readonly Func<TypeMapKey, MapPlan> compile;
        private readonly Func<TypeMapKey, LambdaExpression> project;

        internal MapperEngine(MapperModel model)
        {
            Model = model;
            RequiresState = NeedsState(model);
            resolver = new ConventionResolver(model.Options);
            compile = CompilePlan;
            project = CompileProjection;
        }

        internal MapperModel Model { get; }

        internal bool RequiresState { get; }

        private static bool NeedsState(MapperModel model)
        {
            foreach (TypeMapDefinition definition in model.TypeMaps)
            {
                if (definition.MaxDepth is not null || definition.PreserveReferences)
                {
                    return true;
                }
            }

            return false;
        }

        internal LambdaExpression GetProjection(TypeMapKey key)
        {
            return projections.GetOrAdd(key, project);
        }

        private LambdaExpression CompileProjection(TypeMapKey key)
        {
            if (!TryGetDefinition(key, out TypeMapDefinition? definition))
            {
                throw new MappingException(
                    "No map is configured for " + key + ". Declare it with CreateMap<" +
                    key.SourceType.Name + ", " + key.DestinationType.Name + ">().");
            }

            return ProjectionCompiler.Compile(definition!, this);
        }

        /// <summary>
        /// Finds the definition for a pair, closing an open generic template the first time a pair
        /// that matches one comes through. A closed template is kept, so the work happens once.
        /// </summary>
        internal bool TryGetDefinition(TypeMapKey key, out TypeMapDefinition? definition)
        {
            if (Model.TryGetTypeMap(key, out definition))
            {
                return !ConventionResolver.IsTemplate(definition!);
            }

            if (closedTemplates.TryGetValue(key, out definition))
            {
                return true;
            }

            lock (closing)
            {
                if (closedTemplates.TryGetValue(key, out definition))
                {
                    return true;
                }

                if (!TryClose(key, out definition))
                {
                    return false;
                }

                closedTemplates[key] = definition!;
                return true;
            }
        }

        internal bool CanMap(TypeMapKey key)
        {
            return TryGetDefinition(key, out _);
        }

        private bool TryClose(TypeMapKey key, out TypeMapDefinition? definition)
        {
            definition = null;

            if (!key.SourceType.IsGenericType || !key.DestinationType.IsGenericType)
            {
                return false;
            }

            Type source = key.SourceType.GetGenericTypeDefinition();
            Type destination = key.DestinationType.GetGenericTypeDefinition();

            foreach (TypeMapDefinition template in Model.TypeMaps)
            {
                if (!ConventionResolver.IsTemplate(template) ||
                    template.SourceType != source ||
                    template.DestinationType != destination)
                {
                    continue;
                }

                definition = resolver.Complete(new TypeMapDefinition(key)
                {
                    Members = CloseIgnored(template, key.DestinationType),
                    MemberListValidation = template.MemberListValidation,
                    MaxDepth = template.MaxDepth,
                    PreserveReferences = template.PreserveReferences,
                });

                return true;
            }

            return false;
        }

        private static MemberDefinition[] CloseIgnored(TypeMapDefinition template, Type destinationType)
        {
            if (template.Members.Count == 0)
            {
                return Array.Empty<MemberDefinition>();
            }

            var members = new List<MemberDefinition>(template.Members.Count);

            foreach (MemberDefinition member in template.Members)
            {
                PropertyInfo? property = destinationType.GetProperty(member.DestinationMember.Name);

                if (property is not null)
                {
                    members.Add(new MemberDefinition(MemberDescriptor.ForProperty(property))
                    {
                        IsIgnored = member.IsIgnored,
                        IsExplicit = true,
                    });
                }
            }

            return members.ToArray();
        }

        internal MapPlan GetPlan(TypeMapKey key)
        {
            return plans.GetOrAdd(key, compile);
        }

        private MapPlan CompilePlan(TypeMapKey key)
        {
            if (!TryGetDefinition(key, out TypeMapDefinition? definition))
            {
                throw new MappingException(
                    "No map is configured for " + key + ". Declare it with CreateMap<" +
                    key.SourceType.Name + ", " + key.DestinationType.Name + ">().");
            }

            return PlanCompiler.Compile(definition!, this);
        }
    }
}
