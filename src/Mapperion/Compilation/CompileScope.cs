using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Mapperion.Model;

namespace Mapperion.Compilation
{
    /// <summary>
    /// The variable a compiled body writes the current member into, so a failure can be reported
    /// against it. <see cref="Used"/> records whether anything was actually written, which lets the
    /// emitter leave the variable out when nothing tracks it.
    /// </summary>
    internal sealed class StepSlot
    {
        internal StepSlot(ParameterExpression variable)
        {
            Variable = variable;
        }

        internal ParameterExpression Variable { get; }

        internal bool Used { get; set; }
    }

    /// <summary>
    /// What compiling one expression needs beyond the expression itself: the engine that owns the
    /// plans, the parameters of the lambda being written, and how much of a nested map may be
    /// written into the body instead of called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writing a small map inline removes the call into its plan and lets the JIT see the whole
    /// thing as one method. The cost is that the inlined body no longer has a reporting region of
    /// its own, so the scope carries the member path it sits under and the inlined assignments
    /// write the composed path into the enclosing <see cref="Step"/>. A failure therefore reports
    /// the same <c>MemberPath</c> it would have reported through a call; only the sentence naming
    /// which map failed changes, from the nested pair to the one that absorbed it.
    /// </para>
    /// <para>
    /// Three limits keep the emitted code finite: a map already being written inline is never
    /// written into itself, the nesting stops after a few levels, and a budget caps how many
    /// members one lambda may absorb in total.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Compiling a map inspects types by reflection.")]
    [RequiresDynamicCode("Compiling a map emits code at run time.")]
    internal sealed class CompileScope
    {
        private const int MembersPerLambda = 64;
        private const int MembersPerMap = 16;
        private const int Levels = 3;

        private readonly int[] budget;
        private readonly TypeMapKey[] active;
        private readonly bool inlining;

        private CompileScope(
            MapperEngine engine,
            ParameterExpression context,
            StepSlot? step,
            string prefix,
            string stepValue,
            int[] budget,
            TypeMapKey[] active,
            bool inlining)
        {
            Engine = engine;
            Context = context;
            Step = step;
            Prefix = prefix;
            StepValue = stepValue;
            this.budget = budget;
            this.active = active;
            this.inlining = inlining;
        }

        internal MapperEngine Engine { get; }

        internal ParameterExpression Context { get; }

        /// <summary>Gets the step variable of the reporting region this scope sits inside.</summary>
        internal StepSlot? Step { get; }

        /// <summary>Gets what to put in front of a member name to form its full path.</summary>
        internal string Prefix { get; }

        /// <summary>Gets the path of the member this scope is producing a value for.</summary>
        internal string StepValue { get; }

        internal static CompileScope Root(
            MapperEngine engine,
            ParameterExpression context,
            StepSlot? step,
            TypeMapKey key)
        {
            return new CompileScope(
                engine,
                context,
                step,
                string.Empty,
                string.Empty,
                new[] { MembersPerLambda },
                new[] { key },
                true);
        }

        /// <summary>Narrows the scope to one destination member, extending the path with its name.</summary>
        internal CompileScope ForMember(string name)
        {
            return new CompileScope(
                Engine,
                Context,
                Step,
                Prefix + name + ".",
                Prefix + name,
                budget,
                active,
                inlining);
        }

        /// <summary>
        /// Narrows the scope to the body of a collection loop, where the path restarts: the index
        /// is prepended by the loop's own handler, not by the member names inside it.
        /// </summary>
        internal CompileScope ForElement(StepSlot step)
        {
            return new CompileScope(
                Engine,
                Context,
                step,
                string.Empty,
                string.Empty,
                budget,
                active,
                inlining);
        }

        /// <summary>Moves to a separate lambda, which has a reporting region of neither its own nor its caller's.</summary>
        internal CompileScope ForLambda(ParameterExpression context)
        {
            return new CompileScope(
                Engine,
                context,
                null,
                string.Empty,
                string.Empty,
                budget,
                active,
                false);
        }

        /// <summary>
        /// Drops inlining for a value produced before the destination exists, such as a constructor
        /// argument, where there is no member name to report a failure against.
        /// </summary>
        internal CompileScope WithoutInlining()
        {
            return new CompileScope(Engine, Context, Step, Prefix, StepValue, budget, active, false);
        }

        /// <summary>
        /// Decides whether the map for a pair may be written into the current body, and books its
        /// members against the budget when it may.
        /// </summary>
        internal bool TryReserveInline(TypeMapKey key, [NotNullWhen(true)] out TypeMapDefinition? definition)
        {
            definition = null;

            if (!inlining || Step is null || active.Length > Levels)
            {
                return false;
            }

            for (int i = 0; i < active.Length; i++)
            {
                if (active[i].Equals(key))
                {
                    return false;
                }
            }

            if (!Engine.TryGetDefinition(key, out TypeMapDefinition? candidate) || candidate is null)
            {
                return false;
            }

            if (!IsPlain(candidate))
            {
                return false;
            }

            int members = candidate.Members.Count;

            if (members > MembersPerMap || members > budget[0])
            {
                return false;
            }

            budget[0] -= members;
            definition = candidate;
            return true;
        }

        /// <summary>Enters the body of a map being written inline, so it cannot absorb itself.</summary>
        internal CompileScope Inside(TypeMapKey key)
        {
            var extended = new TypeMapKey[active.Length + 1];
            Array.Copy(active, extended, active.Length);
            extended[active.Length] = key;

            return new CompileScope(Engine, Context, Step, Prefix, StepValue, budget, extended, true);
        }

        /// <summary>
        /// Answers whether a map is nothing but member assignments. Anything that runs at a
        /// particular moment in the plan, or that decides at run time which map applies, keeps its
        /// own plan: a converter, a before or after step, a derived dispatch, a depth limit or
        /// reference tracking.
        /// </summary>
        private static bool IsPlain(TypeMapDefinition definition)
        {
            return definition.TypeConverterType is null
                && definition.ConstructUsing is null
                && definition.BeforeMapActions.Count == 0
                && definition.AfterMapActions.Count == 0
                && definition.DerivedMaps.Count == 0
                && !definition.PreserveReferences
                && definition.MaxDepth is null;
        }
    }
}
