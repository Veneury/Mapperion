using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Mapperion.Conventions;
using Mapperion.Internal;
using Mapperion.Model;
using Mapperion.Projection;
using Mapperion.Validation;

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

        private readonly object growing = new object();
        private MapPlan?[] slots = new MapPlan?[16];

        private readonly ConcurrentDictionary<TypeMapKey, LambdaExpression> projections =
            new ConcurrentDictionary<TypeMapKey, LambdaExpression>();

        private readonly Dictionary<TypeMapKey, TypeMapDefinition> closedTemplates =
            new Dictionary<TypeMapKey, TypeMapDefinition>();

        private static readonly HashSet<TypeMapKey> Empty = new HashSet<TypeMapKey>();

        private readonly HashSet<TypeMapKey> ceilings;
        private readonly object closing = new object();
        private readonly ConventionResolver resolver;
        private readonly Func<TypeMapKey, MapPlan> compile;
        private readonly Func<TypeMapKey, LambdaExpression> project;

        internal MapperEngine(MapperModel model)
        {
            Model = model;
            ceilings = model.Options.RecursionLimit > 0
                ? CycleFinder.Closing(model)
                : Empty;
            RequiresState = NeedsState(model) || ceilings.Count > 0;
            resolver = new ConventionResolver(model.Options);
            compile = CompilePlan;
            project = CompileProjection;
        }

        internal MapperModel Model { get; }

        internal bool RequiresState { get; }

        /// <summary>
        /// Answers whether an operation has to carry state. Depth counting and reference tracking
        /// need it, and so does anything that is handed a <see cref="ResolutionContext"/>, since
        /// that is the way into the per-operation items.
        /// </summary>
        private static bool NeedsState(MapperModel model)
        {
            foreach (TypeMapDefinition definition in model.TypeMaps)
            {
                if (definition.MaxDepth is not null ||
                    definition.PreserveReferences ||
                    HandsOutContext(definition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HandsOutContext(TypeMapDefinition definition)
        {
            if (definition.TypeConverterType is not null)
            {
                return true;
            }

            if (definition.ConstructUsing is Delegate factory && TakesContext(factory, 2))
            {
                return true;
            }

            foreach (object action in definition.BeforeMapActions)
            {
                if (IsContextAction(action))
                {
                    return true;
                }
            }

            foreach (object action in definition.AfterMapActions)
            {
                if (IsContextAction(action))
                {
                    return true;
                }
            }

            foreach (MemberDefinition member in definition.Members)
            {
                if (member.ValueConverterType is not null || member.Source is ValueResolverSource)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A step written as its own type always receives the operation. A step written as a lambda
        /// only receives it when the user asked for the overload that passes it.
        /// </summary>
        private static bool IsContextAction(object action)
        {
            return action is Type || (action is Delegate handler && TakesContext(handler, 3));
        }

        private static bool TakesContext(Delegate handler, int parameters)
        {
            return handler.GetType().GetMethod("Invoke")!.GetParameters().Length == parameters;
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

        /// <summary>
        /// Answers whether a map closes a loop that nothing else stops, and so needs counting to
        /// keep a looping graph from recursing without end.
        /// </summary>
        internal bool NeedsCeiling(TypeMapKey key)
        {
            return ceilings.Contains(key);
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

        /// <summary>
        /// Returns the compiled map for a pair known at compile time, reaching it through the
        /// pair's slot rather than by looking a key up.
        /// </summary>
        /// <remarks>
        /// The dictionary stays the one place a plan is created and stored; this is a cache in
        /// front of it. A slot holds a plan the dictionary already produced, so a race between two
        /// threads filling the same slot writes the same instance twice and is harmless.
        /// </remarks>
        internal MapDelegate<TSource, TDestination> GetTyped<TSource, TDestination>()
        {
            int slot = TypeMapSlot<TSource, TDestination>.Index;
            MapPlan?[] current = Volatile.Read(ref slots);

            if ((uint)slot < (uint)current.Length && current[slot] is MapPlan cached)
            {
                return (MapDelegate<TSource, TDestination>)cached.Typed;
            }

            return (MapDelegate<TSource, TDestination>)Fill(slot, typeof(TSource), typeof(TDestination)).Typed;
        }

        private MapPlan Fill(int slot, Type sourceType, Type destinationType)
        {
            MapPlan plan = GetPlan(new TypeMapKey(sourceType, destinationType));

            lock (growing)
            {
                if (slots.Length <= slot)
                {
                    int length = slots.Length;

                    while (length <= slot)
                    {
                        length *= 2;
                    }

                    var grown = new MapPlan?[length];
                    Array.Copy(slots, grown, slots.Length);
                    grown[slot] = plan;

                    Volatile.Write(ref slots, grown);
                }
                else
                {
                    Volatile.Write(ref slots[slot], plan);
                }
            }

            return plan;
        }

        private MapPlan CompilePlan(TypeMapKey key)
        {
            if (TryGetDefinition(key, out TypeMapDefinition? definition))
            {
                return PlanCompiler.Compile(definition!, this);
            }

            if (TypeClassifier.IsSequence(key.SourceType) && TypeClassifier.IsSequence(key.DestinationType))
            {
                return PlanCompiler.CompileConversion(key, this);
            }

            throw new MappingException(
                "No map is configured for " + key + ". Declare it with CreateMap<" +
                key.SourceType.Name + ", " + key.DestinationType.Name + ">().");
        }
    }
}
