using System.Diagnostics.CodeAnalysis;
using Mapperion.Model;

namespace Mapperion.Compilation
{
    /// <summary>
    /// A nested map, resolved once and then held. The compiled plan embeds one of these per pair it
    /// reaches instead of looking the plan up on every value, which is what made a collection pay a
    /// dictionary lookup per element.
    /// </summary>
    /// <remarks>
    /// The plan cannot be resolved while the outer plan is being compiled, because the nested one is
    /// compiled on first use and two maps are allowed to reference each other. So it is resolved on
    /// the first value that needs it and cached in a field. Two threads arriving at once may both
    /// resolve it; they arrive at the same delegate, so the race is harmless and cheaper than a lock.
    /// </remarks>
    [RequiresUnreferencedCode("Resolving a plan inspects types by reflection.")]
    [RequiresDynamicCode("Resolving a plan compiles code at run time.")]
    internal sealed class PlanReference<TSource, TDestination>
    {
        private readonly MapperEngine engine;
        private MapDelegate<TSource, TDestination>? resolved;

        public PlanReference(MapperEngine engine)
        {
            this.engine = engine;
        }

        public TDestination Map(TSource source, MappingContext context)
        {
            MapDelegate<TSource, TDestination> map = resolved ??= Resolve();
            return map(source, default!, context);
        }

        public TDestination MapInto(TSource source, TDestination destination, MappingContext context)
        {
            MapDelegate<TSource, TDestination> map = resolved ??= Resolve();
            return map(source, destination, context);
        }

        private MapDelegate<TSource, TDestination> Resolve()
        {
            MapPlan plan = engine.GetPlan(new TypeMapKey(typeof(TSource), typeof(TDestination)));
            return (MapDelegate<TSource, TDestination>)plan.Typed;
        }
    }
}
