using System;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Compilation;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Execution
{
    /// <summary>
    /// The mapper handed to callers. Immutable and safe to share: all it holds is the engine that
    /// owns the compiled plans.
    /// </summary>
    internal sealed class Mapper : IMapper
    {
        private const string EntryPointJustification =
            "Obtaining a mapper goes through MapperConfiguration.CreateMapper, which is annotated " +
            "with RequiresUnreferencedCode and RequiresDynamicCode.";

        private readonly MapperEngine engine;

        [RequiresUnreferencedCode("Mapping resolves plans that inspect types by reflection.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        internal Mapper(MapperModel model)
        {
            engine = new MapperEngine(model);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TDestination>(object? source)
        {
            if (source is null)
            {
                return default!;
            }

            var key = new TypeMapKey(source.GetType(), typeof(TDestination));
            object? mapped = engine.GetPlan(key).Boxed(source, null, new MappingContext(engine));

            return (TDestination)mapped!;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(TSource source)
        {
            return Invoke<TSource, TDestination>(source, default!);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return Invoke(source, destination);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Only reached for a value type, which always has a default constructor.")]
        public object? Map(object? source, Type sourceType, Type destinationType)
        {
            Guard.NotNull(sourceType, nameof(sourceType));
            Guard.NotNull(destinationType, nameof(destinationType));

            if (source is null)
            {
                return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
            }

            var key = new TypeMapKey(sourceType, destinationType);
            return engine.GetPlan(key).Boxed(source, null, new MappingContext(engine));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        private TDestination Invoke<TSource, TDestination>(TSource source, TDestination destination)
        {
            MapPlan plan = engine.GetPlan(new TypeMapKey(typeof(TSource), typeof(TDestination)));
            var typed = (MapDelegate<TSource, TDestination>)plan.Typed;

            return typed(source, destination, new MappingContext(engine));
        }
    }
}
