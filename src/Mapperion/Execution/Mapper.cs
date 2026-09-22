using System;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Compilation;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Execution
{
    /// <summary>
    /// The mapper handed to callers. Immutable and safe to share. Several mappers can sit on the
    /// same engine, which is what makes one per container scope cheap: the compiled plans are
    /// shared, and only the place converters and resolvers come from differs.
    /// </summary>
    internal sealed class Mapper : IMapper
    {
        private const string EntryPointJustification =
            "Obtaining a mapper goes through MapperConfiguration.CreateMapper, which is annotated " +
            "with RequiresUnreferencedCode and RequiresDynamicCode.";

        private readonly MapperEngine engine;
        private readonly IServiceResolver services;

        [RequiresUnreferencedCode("Mapping resolves plans that inspect types by reflection.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        internal Mapper(MapperModel model)
            : this(new MapperEngine(model), new ActivatorServiceResolver())
        {
        }

        internal Mapper(MapperEngine engine, IServiceResolver services)
        {
            this.engine = engine;
            this.services = services;
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
            object? mapped = engine.GetPlan(key).Boxed(source, null, Context());

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
            return engine.GetPlan(key).Boxed(source, null, Context());
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        private TDestination Invoke<TSource, TDestination>(TSource source, TDestination destination)
        {
            MapPlan plan = engine.GetPlan(new TypeMapKey(typeof(TSource), typeof(TDestination)));
            var typed = (MapDelegate<TSource, TDestination>)plan.Typed;

            return typed(source, destination, Context());
        }

        private MappingContext Context()
        {
            return new MappingContext(engine, services, this);
        }
    }
}
