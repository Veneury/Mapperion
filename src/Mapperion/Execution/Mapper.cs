using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
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
        private readonly MappingContext stateless;

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
            stateless = new MappingContext(engine, services, this, null);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TDestination>(object? source)
        {
            return Map<TDestination>(source, (MappingState?)null);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions> options)
        {
            return Map<TDestination>(source, Requested(options));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        private TDestination Map<TDestination>(object? source, MappingState? state)
        {
            if (source is null)
            {
                return default!;
            }

            var key = new TypeMapKey(source.GetType(), typeof(TDestination));
            object? mapped = engine.GetPlan(key).Boxed(source, null, Context(state));

            return (TDestination)mapped!;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(TSource source)
        {
            return Invoke<TSource, TDestination>(source, default!, null);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(
            TSource source,
            Action<IMappingOperationOptions> options)
        {
            return Invoke<TSource, TDestination>(source, default!, Requested(options));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return Invoke(source, destination, null);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public TDestination Map<TSource, TDestination>(
            TSource source,
            TDestination destination,
            Action<IMappingOperationOptions> options)
        {
            return Invoke(source, destination, Requested(options));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Only reached for a value type, which always has a default constructor.")]
        public object? Map(object? source, Type sourceType, Type destinationType)
        {
            return Map(source, sourceType, destinationType, (MappingState?)null);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Only reached for a value type, which always has a default constructor.")]
        public object? Map(
            object? source,
            Type sourceType,
            Type destinationType,
            Action<IMappingOperationOptions> options)
        {
            return Map(source, sourceType, destinationType, Requested(options));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Only reached for a value type, which always has a default constructor.")]
        private object? Map(object? source, Type sourceType, Type destinationType, MappingState? state)
        {
            Guard.NotNull(sourceType, nameof(sourceType));
            Guard.NotNull(destinationType, nameof(destinationType));

            if (source is null)
            {
                return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
            }

            var key = new TypeMapKey(sourceType, destinationType);
            return engine.GetPlan(key).Boxed(source, null, Context(state));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        private TDestination Invoke<TSource, TDestination>(
            TSource source,
            TDestination destination,
            MappingState? state)
        {
            return engine.GetTyped<TSource, TDestination>()(source, destination, Context(state));
        }

        /// <summary>
        /// Builds the state for an operation the caller set up. It is created whatever the
        /// configuration would have needed, because the caller asking for it is reason enough.
        /// </summary>
        private static MappingState Requested(Action<IMappingOperationOptions> options)
        {
            Guard.NotNull(options, nameof(options));

            var state = new MappingState();
            options(state);

            return state;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = EntryPointJustification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = EntryPointJustification)]
        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source)
        {
            Guard.NotNull(source, nameof(source));

            LambdaExpression selector = engine.GetProjection(
                new TypeMapKey(source.ElementType, typeof(TDestination)));

            MethodCallExpression select = Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Select),
                new[] { source.ElementType, typeof(TDestination) },
                source.Expression,
                Expression.Quote(selector));

            return source.Provider.CreateQuery<TDestination>(select);
        }

        /// <remarks>
        /// An operation that needs no state carries the same four references every time, so the
        /// context is built once in the constructor and handed out as it stands.
        /// </remarks>
        private MappingContext Context(MappingState? state)
        {
            if (state is null)
            {
                return engine.RequiresState
                    ? new MappingContext(engine, services, this, new MappingState())
                    : stateless;
            }

            return new MappingContext(engine, services, this, state);
        }
    }
}
