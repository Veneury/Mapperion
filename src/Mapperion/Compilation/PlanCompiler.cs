using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Mapperion.Execution;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Turns one definition into a compiled delegate. The body is a plain block of assignments:
    /// no reflection survives into the compiled code, and nested maps become a typed call resolved
    /// through the engine so two maps can reference each other.
    /// </summary>
    [RequiresUnreferencedCode("Compiling a map inspects types by reflection.")]
    [RequiresDynamicCode("Compiling a map emits code at run time.")]
    internal static class PlanCompiler
    {
        internal static MapPlan Compile(TypeMapDefinition definition, MapperEngine engine)
        {
            Type sourceType = definition.SourceType;
            Type destinationType = definition.DestinationType;

            ParameterExpression source = Expression.Parameter(sourceType, "source");
            ParameterExpression destination = Expression.Parameter(destinationType, "destination");
            ParameterExpression context = Expression.Parameter(typeof(MappingContext), "context");
            ParameterExpression result = Expression.Variable(destinationType, "result");

            Expression block;

            if (definition.TypeConverterType is not null)
            {
                block = BuildTypeConverterCall(definition, source, destination, context);
            }
            else
            {
                var step = new StepSlot(Expression.Variable(typeof(string), "step"));
                CompileScope scope = CompileScope.Root(engine, context, step, definition.Key);

                var body = new List<Expression>
                {
                    Expression.Assign(step.Variable, Expression.Constant("(constructing)")),
                    Expression.Assign(result, CreateDestination(definition, destination, source, scope)),
                };

                if (definition.PreserveReferences)
                {
                    body.Add(Expression.Call(
                        Method(nameof(MappingRuntime.Preserve)),
                        Expression.Convert(source, typeof(object)),
                        Expression.Constant(destinationType, typeof(Type)),
                        Expression.Convert(result, typeof(object)),
                        context));
                }

                foreach (object action in definition.BeforeMapActions)
                {
                    body.Add(Expression.Assign(step.Variable, Expression.Constant("(before step)")));
                    body.Add(BuildAction(action, definition, source, result, context));
                }

                foreach (MemberDefinition member in Ordered(definition.Members))
                {
                    if (member.IsPath)
                    {
                        continue;
                    }

                    Expression? assignment = BuildAssignment(member, result, result, source, scope);

                    if (assignment is not null)
                    {
                        body.Add(Expression.Assign(
                            step.Variable,
                            Expression.Constant(member.DestinationMember.Name)));

                        body.Add(assignment);
                    }
                }

                foreach (MemberDefinition member in Ordered(definition.Members))
                {
                    if (!member.IsPath)
                    {
                        continue;
                    }

                    Expression? assignment = BuildPathAssignment(member, result, source, scope);

                    if (assignment is not null)
                    {
                        body.Add(Expression.Assign(
                            step.Variable,
                            Expression.Constant(member.DestinationPath!.ToString())));

                        body.Add(assignment);
                    }
                }

                foreach (object action in definition.AfterMapActions)
                {
                    body.Add(Expression.Assign(step.Variable, Expression.Constant("(after step)")));
                    body.Add(BuildAction(action, definition, source, result, context));
                }

                body.Add(result);

                Expression core = Expression.Block(new[] { result }, body);
                core = WithPreservedShortCircuit(core, definition, source, context);
                core = WithDepthLimit(core, definition, context);
                core = WithRecursionCeiling(core, definition, context, engine);
                core = WithDerivedDispatch(core, definition, source, scope);

                block = Expression.Block(
                    new[] { step.Variable },
                    Expression.Assign(step.Variable, Expression.Constant("(constructing)")),
                    Reporting(core, step.Variable, definition));
            }

            if (!sourceType.IsValueType)
            {
                block = Expression.Condition(
                    Expression.Equal(source, Expression.Constant(null, sourceType)),
                    Expression.Default(destinationType),
                    block);
            }

            Type delegateType = typeof(MapDelegate<,>).MakeGenericType(sourceType, destinationType);
            Delegate typed = Expression.Lambda(delegateType, block, source, destination, context).Compile();

            return new MapPlan(typed, BuildBoxed(typed, sourceType, destinationType));
        }

        /// <summary>
        /// Hands the work to a derived map when the instance turns out to be of a derived type, so
        /// mapping through a base reference still produces the right destination.
        /// </summary>
        /// <remarks>
        /// The checks run most-derived first, so a hierarchy several levels deep picks the closest
        /// match rather than the first one that happens to fit.
        /// </remarks>
        private static Expression WithDerivedDispatch(
            Expression core,
            TypeMapDefinition definition,
            ParameterExpression source,
            CompileScope scope)
        {
            if (definition.DerivedMaps.Count == 0)
            {
                return core;
            }

            var ordered = new List<TypeMapKey>(definition.DerivedMaps);

            ordered.Sort(static (left, right) =>
            {
                if (left.SourceType == right.SourceType)
                {
                    return 0;
                }

                if (left.SourceType.IsAssignableFrom(right.SourceType))
                {
                    return 1;
                }

                return right.SourceType.IsAssignableFrom(left.SourceType) ? -1 : 0;
            });

            Expression dispatch = core;

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                TypeMapKey derived = ordered[i];

                MethodCallExpression mapped = ConversionBuilder.NestedMapCall(
                    derived.SourceType,
                    derived.DestinationType,
                    Expression.Convert(source, derived.SourceType),
                    scope);

                dispatch = Expression.Condition(
                    Expression.TypeIs(source, derived.SourceType),
                    Expression.Convert(mapped, definition.DestinationType),
                    dispatch);
            }

            return dispatch;
        }

        /// <summary>
        /// Returns the destination already built for this source instance, when the map asks for
        /// references to be preserved. Registering happens right after the destination is created
        /// and before any member is mapped, which is what lets a cycle find its way back.
        /// </summary>
        private static Expression WithPreservedShortCircuit(
            Expression core,
            TypeMapDefinition definition,
            ParameterExpression source,
            ParameterExpression context)
        {
            if (!definition.PreserveReferences)
            {
                return core;
            }

            ParameterExpression existing = Expression.Variable(typeof(object), "preserved");

            return Expression.Block(
                new[] { existing },
                Expression.Assign(
                    existing,
                    Expression.Call(
                        Method(nameof(MappingRuntime.Preserved)),
                        Expression.Convert(source, typeof(object)),
                        Expression.Constant(definition.DestinationType, typeof(Type)),
                        context)),
                Expression.Condition(
                    Expression.Equal(existing, Expression.Constant(null, typeof(object))),
                    core,
                    Expression.Convert(existing, definition.DestinationType)));
        }

        /// <summary>
        /// Stops recursing once this map is nested deeper than it allows, yielding the default
        /// instead. The counter is released in a finally so an exception does not leave it raised.
        /// </summary>
        private static Expression WithDepthLimit(
            Expression core,
            TypeMapDefinition definition,
            ParameterExpression context)
        {
            if (definition.MaxDepth is not int maximum)
            {
                return core;
            }

            ParameterExpression depth = Expression.Variable(typeof(int), "depth");
            ConstantExpression key = Expression.Constant(definition.Key, typeof(TypeMapKey));

            return Expression.Block(
                new[] { depth },
                Expression.Assign(
                    depth,
                    Expression.Call(Method(nameof(MappingRuntime.Enter)), key, context)),
                Expression.TryFinally(
                    Expression.Condition(
                        Expression.GreaterThan(depth, Expression.Constant(maximum)),
                        Expression.Default(definition.DestinationType),
                        core),
                    Expression.Call(Method(nameof(MappingRuntime.Exit)), key, context)));
        }

        /// <summary>
        /// Bounds a map that can reach itself, so a graph that loops fails with an exception the
        /// caller can catch rather than recursing until the stack runs out, which cannot be caught
        /// and takes the process with it.
        /// </summary>
        /// <remarks>
        /// Only the maps that close an unguarded loop get this, so a map whose types cannot recurse
        /// pays nothing, and one that already asked for <c>MaxDepth</c> or <c>PreserveReferences</c>
        /// keeps what it asked for. The counter is released in a finally so a failure does not
        /// leave it raised.
        /// </remarks>
        private static Expression WithRecursionCeiling(
            Expression core,
            TypeMapDefinition definition,
            ParameterExpression context,
            MapperEngine engine)
        {
            if (!engine.NeedsCeiling(definition.Key))
            {
                return core;
            }

            int limit = engine.Model.Options.RecursionLimit;
            ParameterExpression depth = Expression.Variable(typeof(int), "recursion");
            ConstantExpression key = Expression.Constant(definition.Key, typeof(TypeMapKey));

            return Expression.Block(
                new[] { depth },
                Expression.Assign(
                    depth,
                    Expression.Call(Method(nameof(MappingRuntime.Enter)), key, context)),
                Expression.TryFinally(
                    Expression.Condition(
                        Expression.GreaterThan(depth, Expression.Constant(limit)),
                        Expression.Call(
                            Method(nameof(MappingRuntime.TooDeep))
                                .MakeGenericMethod(definition.DestinationType),
                            Expression.Constant(definition.Key.ToString()),
                            Expression.Constant(limit)),
                        core),
                    Expression.Call(Method(nameof(MappingRuntime.Exit)), key, context)));
        }

        /// <remarks>
        /// The catch carries no exception filter. A filter compiles to an IL filter block, and
        /// <c>DynamicMethod</c> on .NET Framework refuses those, so the decision of what to rethrow
        /// untouched lives in <see cref="MappingRuntime.Fail{TDestination}"/> instead.
        /// </remarks>
        private static TryExpression Reporting(
            Expression body,
            ParameterExpression step,
            TypeMapDefinition definition)
        {
            ParameterExpression error = Expression.Parameter(typeof(Exception), "error");

            MethodCallExpression fail = Expression.Call(
                Method(nameof(MappingRuntime.Fail)).MakeGenericMethod(definition.DestinationType),
                step,
                Expression.Constant(definition.Key.ToString()),
                error);

            return Expression.TryCatch(body, Expression.Catch(error, fail));
        }

        private static Expression BuildTypeConverterCall(
            TypeMapDefinition definition,
            ParameterExpression source,
            ParameterExpression destination,
            ParameterExpression context)
        {
            Type[] arguments = InterfaceArguments(
                definition.TypeConverterType!,
                typeof(ITypeConverter<,>),
                "ITypeConverter<TSource, TDestination>");

            Expression call = Expression.Call(
                Method(nameof(MappingRuntime.ConvertType)).MakeGenericMethod(arguments[0], arguments[1]),
                Expression.Constant(definition.TypeConverterType, typeof(Type)),
                Expression.Convert(source, arguments[0]),
                Expression.Convert(destination, arguments[1]),
                context);

            return arguments[1] == definition.DestinationType
                ? call
                : Expression.Convert(call, definition.DestinationType);
        }

        private static Expression BuildAction(
            object action,
            TypeMapDefinition definition,
            ParameterExpression source,
            ParameterExpression result,
            ParameterExpression context)
        {
            if (action is Delegate handler)
            {
                int parameters = handler.GetType().GetMethod("Invoke")!.GetParameters().Length;

                return parameters == 3
                    ? Expression.Invoke(Expression.Constant(handler), source, result, NewResolutionContext(context))
                    : Expression.Invoke(Expression.Constant(handler), source, result);
            }

            if (action is Type actionType)
            {
                return Expression.Call(
                    Method(nameof(MappingRuntime.RunAction))
                        .MakeGenericMethod(definition.SourceType, definition.DestinationType),
                    Expression.Constant(actionType, typeof(Type)),
                    source,
                    result,
                    context);
            }

            throw new MapperConfigurationException(
                definition.Key + ": a before or after step must be a delegate or an IMappingAction type.");
        }

        private static NewExpression NewResolutionContext(ParameterExpression context)
        {
            ConstructorInfo constructor = typeof(ResolutionContext).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(MappingContext) },
                null)!;

            return Expression.New(constructor, context);
        }

        private static Type[] InterfaceArguments(Type implementation, Type contract, string expected)
        {
            foreach (Type implemented in implementation.GetInterfaces())
            {
                if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == contract)
                {
                    return implemented.GetGenericArguments();
                }
            }

            throw new MapperConfigurationException(
                implementation.Name + " does not implement " + expected + ".");
        }

        private static MethodInfo Method(string name)
        {
            return typeof(MappingRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        }

        /// <summary>
        /// Compiles a pair that needs no member configuration at all, such as one sequence into
        /// another. The whole body is the conversion the builder already knows how to write.
        /// </summary>
        internal static MapPlan CompileConversion(TypeMapKey key, MapperEngine engine)
        {
            ParameterExpression source = Expression.Parameter(key.SourceType, "source");
            ParameterExpression destination = Expression.Parameter(key.DestinationType, "destination");
            ParameterExpression context = Expression.Parameter(typeof(MappingContext), "context");

            Expression body = ConversionBuilder.Build(
                source,
                key.DestinationType,
                CompileScope.Root(engine, context, null, key));

            Type delegateType = typeof(MapDelegate<,>).MakeGenericType(key.SourceType, key.DestinationType);
            Delegate typed = Expression.Lambda(delegateType, body, source, destination, context).Compile();

            return new MapPlan(typed, BuildBoxed(typed, key.SourceType, key.DestinationType));
        }

        private static IEnumerable<MemberDefinition> Ordered(IReadOnlyList<MemberDefinition> members)
        {
            var ordered = new List<KeyValuePair<int, MemberDefinition>>(members.Count);

            for (int i = 0; i < members.Count; i++)
            {
                ordered.Add(new KeyValuePair<int, MemberDefinition>(i, members[i]));
            }

            ordered.Sort(static (left, right) =>
            {
                int byOrder = left.Value.MappingOrder.CompareTo(right.Value.MappingOrder);
                return byOrder != 0 ? byOrder : left.Key.CompareTo(right.Key);
            });

            foreach (KeyValuePair<int, MemberDefinition> entry in ordered)
            {
                yield return entry.Value;
            }
        }

        private static Expression CreateDestination(
            TypeMapDefinition definition,
            ParameterExpression destination,
            ParameterExpression source,
            CompileScope scope)
        {
            Type destinationType = definition.DestinationType;

            if (definition.ConstructUsing is Delegate factory)
            {
                Expression built = InvokeFactory(factory, definition, source, scope);

                return destinationType.IsValueType ? built : Expression.Coalesce(destination, built);
            }

            if (definition.Constructor is not null)
            {
                Expression created = Expression.New(
                    definition.Constructor,
                    BuildArguments(definition, source, scope));

                return destinationType.IsValueType ? created : Expression.Coalesce(destination, created);
            }

            if (destinationType.IsValueType)
            {
                return destination;
            }

            ConstructorInfo? parameterless = destinationType.GetConstructor(Type.EmptyTypes);

            if (parameterless is not null)
            {
                return Expression.Coalesce(destination, Expression.New(parameterless));
            }

            if (definition.DerivedMaps.Count == 0)
            {
                throw new MapperConfigurationException(
                    destinationType.Name + " cannot be created: it has no parameterless constructor " +
                    "and no constructor whose arguments could be resolved from " +
                    definition.SourceType.Name + ".");
            }

            return Expression.Coalesce(destination, Unbuildable(definition, source));
        }

        /// <summary>
        /// Stands in for the construction of a destination that has derived maps and cannot be
        /// built on its own, such as an abstract base.
        /// </summary>
        /// <remarks>
        /// Reached only when none of the derived maps matched, because the dispatch that chooses
        /// between them is wrapped around this. So a polymorphic map with an abstract base
        /// destination compiles and works, and the one arrangement that has no answer — an instance
        /// no derived map covers — says so when it turns up, naming the type that turned up.
        /// </remarks>
        private static UnaryExpression Unbuildable(TypeMapDefinition definition, ParameterExpression source)
        {
            return Expression.Throw(
                Expression.Call(
                    Method(nameof(MappingRuntime.CannotCreateBase)),
                    Expression.Constant(definition.DestinationType, typeof(Type)),
                    Expression.Constant(definition.SourceType, typeof(Type)),
                    Expression.Convert(source, typeof(object))),
                definition.DestinationType);
        }

        /// <summary>
        /// Emits the call into a destination factory, with or without the operation depending on
        /// which overload the user reached for.
        /// </summary>
        private static Expression InvokeFactory(
            Delegate factory,
            TypeMapDefinition definition,
            ParameterExpression source,
            CompileScope scope)
        {
            int parameters = factory.GetType().GetMethod("Invoke")!.GetParameters().Length;

            Expression call = parameters == 2
                ? Expression.Invoke(Expression.Constant(factory), source, NewResolutionContext(scope.Context))
                : Expression.Invoke(Expression.Constant(factory), source);

            return call.Type == definition.DestinationType
                ? call
                : Expression.Convert(call, definition.DestinationType);
        }

        private static Expression[] BuildArguments(
            TypeMapDefinition definition,
            ParameterExpression source,
            CompileScope scope)
        {
            var ordered = new List<ConstructorParameterDefinition>(definition.ConstructorParameters);
            ordered.Sort(static (left, right) => left.Position.CompareTo(right.Position));

            CompileScope argument = scope.WithoutInlining();
            var arguments = new Expression[ordered.Count];

            for (int i = 0; i < ordered.Count; i++)
            {
                ConstructorParameterDefinition parameter = ordered[i];

                if (parameter.Source is null)
                {
                    if (!parameter.HasDefaultValue)
                    {
                        throw new MapperConfigurationException(
                            definition.Key + ": constructor parameter '" + parameter.Name +
                            "' has no source. Map it with ForCtorParam.");
                    }

                    arguments[i] = Expression.Constant(parameter.DefaultValue, parameter.ParameterType);
                    continue;
                }

                arguments[i] = ConversionBuilder.Build(
                    ReadSource(
                        parameter.Source,
                        source,
                        Expression.Default(definition.DestinationType),
                        scope.Context),
                    parameter.ParameterType,
                    argument);
            }

            return arguments;
        }

        /// <summary>
        /// Writes a nested map straight into the body that needs it, instead of calling its plan.
        /// Returns <see langword="null"/> when the scope does not allow it and the call stands.
        /// </summary>
        /// <remarks>
        /// The absorbed assignments write their full path into the enclosing step variable, so a
        /// failure inside one still names the member it happened at. The body then puts the step
        /// back to the member being produced, so a destination setter that throws is not blamed on
        /// the last member read.
        /// </remarks>
        internal static Expression? TryInline(TypeMapKey key, Expression value, CompileScope scope)
        {
            if (!scope.TryReserveInline(key, out TypeMapDefinition? definition))
            {
                return null;
            }

            CompileScope inner = scope.Inside(key);
            ParameterExpression nested = Expression.Variable(key.SourceType, "nested");
            ParameterExpression built = Expression.Variable(key.DestinationType, "built");

            var body = new List<Expression>
            {
                Expression.Assign(built, CreateInlineDestination(definition, nested, inner)),
            };

            bool tracked = false;

            foreach (MemberDefinition member in Ordered(definition.Members))
            {
                Expression? assignment = BuildAssignment(member, built, built, nested, inner);

                if (assignment is null)
                {
                    continue;
                }

                body.Add(Expression.Assign(
                    scope.Step!.Variable,
                    Expression.Constant(scope.Prefix + member.DestinationMember.Name)));

                body.Add(assignment);
                tracked = true;
            }

            if (tracked)
            {
                body.Add(Expression.Assign(scope.Step!.Variable, Expression.Constant(scope.StepValue)));
                scope.Step!.Used = true;
            }

            body.Add(built);

            Expression block = Expression.Block(new[] { built }, body);

            Expression produced = key.SourceType.IsValueType
                ? block
                : Expression.Condition(
                    Expression.Equal(nested, Expression.Constant(null, key.SourceType)),
                    Expression.Default(key.DestinationType),
                    block);

            return Expression.Block(
                new[] { nested },
                Expression.Assign(nested, value),
                produced);
        }

        /// <summary>
        /// Builds the destination of an absorbed map. Unlike the plan's own version there is no
        /// instance the caller may have passed in: an inlined map always produces a new one.
        /// </summary>
        private static Expression CreateInlineDestination(
            TypeMapDefinition definition,
            ParameterExpression source,
            CompileScope scope)
        {
            Type destinationType = definition.DestinationType;

            if (definition.Constructor is not null)
            {
                return Expression.New(definition.Constructor, BuildArguments(definition, source, scope));
            }

            if (destinationType.IsValueType)
            {
                return Expression.Default(destinationType);
            }

            ConstructorInfo? parameterless = destinationType.GetConstructor(Type.EmptyTypes);

            if (parameterless is null)
            {
                throw new MapperConfigurationException(
                    destinationType.Name + " cannot be created: it has no parameterless constructor " +
                    "and no constructor whose arguments could be resolved from " +
                    definition.SourceType.Name + ".");
            }

            return Expression.New(parameterless);
        }

        /// <param name="member">What is being assigned.</param>
        /// <param name="owner">The instance the member lives on, which is the destination itself
        /// except when writing into a path.</param>
        /// <param name="result">The destination, which is what a resolver is given.</param>
        /// <param name="source">The object being read from.</param>
        /// <param name="scope">The surrounding compilation.</param>
        private static Expression? BuildAssignment(
            MemberDefinition member,
            ParameterExpression owner,
            ParameterExpression result,
            ParameterExpression source,
            CompileScope scope)
        {
            if (member.IsIgnored || member.Source is null)
            {
                return null;
            }

            Expression value = ReadSource(member.Source, source, result, scope.Context);
            value = ApplyNullSubstitute(member, value);

            Type destinationType = member.DestinationMember.MemberType;
            Expression target = Access(owner, member.DestinationMember);
            CompileScope inside = scope.ForMember(member.DestinationMember.Name);

            Expression converted;

            if (member.ValueConverterType is not null)
            {
                converted = BuildValueConverterCall(member.ValueConverterType, value, destinationType, inside);
            }
            else if (member.UseDestinationValue &&
                scope.Engine.CanMap(new TypeMapKey(value.Type, destinationType)))
            {
                converted = MapIntoExisting(value, target, destinationType, scope);
            }
            else
            {
                converted = ConversionBuilder.Build(value, destinationType, inside);
            }

            converted = WithoutNullDestination(converted, scope.Engine.Model.Options);

            Expression assignment;

            if (member.Condition is LambdaExpression condition)
            {
                ParameterExpression resolved = Expression.Variable(destinationType, "resolved");

                assignment = Expression.Block(
                    new[] { resolved },
                    Expression.Assign(resolved, converted),
                    Expression.IfThen(
                        ParameterReplacer.Inline(condition, source),
                        Expression.Assign(target, resolved)));
            }
            else
            {
                assignment = Expression.Assign(target, converted);
            }

            if (member.PreCondition is LambdaExpression preCondition)
            {
                assignment = Expression.IfThen(ParameterReplacer.Inline(preCondition, source), assignment);
            }

            return assignment;
        }

        /// <summary>
        /// Walks into the destination and assigns a member that sits inside it, creating the
        /// objects along the way.
        /// </summary>
        /// <remarks>
        /// A step that can be written is created when it is missing, which is what lets a path be
        /// configured against a destination that arrives empty. A step that cannot be written has
        /// to be there already; nothing can put it there, so the map says which one was missing
        /// rather than throwing a bare null reference.
        /// </remarks>
        private static BlockExpression? BuildPathAssignment(
            MemberDefinition member,
            ParameterExpression result,
            ParameterExpression source,
            CompileScope scope)
        {
            if (member.IsIgnored || member.Source is null)
            {
                return null;
            }

            MemberPath path = member.DestinationPath!;
            var locals = new List<ParameterExpression>();
            var body = new List<Expression>();
            Expression current = result;

            for (int i = 0; i < path.Length - 1; i++)
            {
                MemberDescriptor step = path.Steps[i];
                ParameterExpression local = Expression.Variable(step.MemberType, "path" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Expression access = Access(current, step);

                locals.Add(local);
                body.Add(Expression.Assign(local, access));
                body.Add(Expression.IfThen(
                    Expression.Equal(local, Expression.Constant(null, step.MemberType)),
                    Fill(path, step, local, access)));

                current = local;
            }

            Expression? assignment = BuildAssignment(
                member,
                (ParameterExpression)current,
                result,
                source,
                scope);

            if (assignment is null)
            {
                return null;
            }

            body.Add(assignment);

            return Expression.Block(locals, body);
        }

        private static Expression Fill(
            MemberPath path,
            MemberDescriptor step,
            ParameterExpression local,
            Expression access)
        {
            ConstructorInfo? parameterless = step.MemberType.GetConstructor(Type.EmptyTypes);

            if (parameterless is null || !step.CanWrite)
            {
                return Expression.Throw(Expression.Call(
                    Method(nameof(MappingRuntime.PathStepMissing)),
                    Expression.Constant(path.ToString()),
                    Expression.Constant(step.Name),
                    Expression.Constant(parameterless is null)));
            }

            return Expression.Block(
                Expression.Assign(local, Expression.New(parameterless)),
                Expression.Assign(access, local));
        }

        /// <summary>
        /// Replaces a null with the destination type's empty content when the configuration says
        /// destinations should not hold nulls. Collections are left alone: AllowNullCollections is
        /// the setting that speaks for them.
        /// </summary>
        private static Expression WithoutNullDestination(Expression converted, MapperOptions options)
        {
            if (options.AllowNullDestinationValues || converted.Type.IsValueType)
            {
                return converted;
            }

            Expression? empty = EmptyContent(converted.Type);
            return empty is null ? converted : Expression.Coalesce(converted, empty);
        }

        private static Expression? EmptyContent(Type type)
        {
            if (type == typeof(string))
            {
                return Expression.Constant(string.Empty);
            }

            if (TypeClassifier.IsSequence(type))
            {
                return null;
            }

            ConstructorInfo? parameterless = type.GetConstructor(Type.EmptyTypes);
            return parameterless is null ? null : Expression.New(parameterless);
        }

        private static Expression BuildValueConverterCall(
            Type converterType,
            Expression value,
            Type destinationType,
            CompileScope scope)
        {
            Type[] arguments = InterfaceArguments(
                converterType,
                typeof(IValueConverter<,>),
                "IValueConverter<TSourceMember, TDestinationMember>");

            Expression input = ConversionBuilder.Build(value, arguments[0], scope);

            Expression produced = Expression.Call(
                Method(nameof(MappingRuntime.ConvertValue)).MakeGenericMethod(arguments[0], arguments[1]),
                Expression.Constant(converterType, typeof(Type)),
                input,
                scope.Context);

            return ConversionBuilder.Build(produced, destinationType, scope);
        }

        private static Expression MapIntoExisting(
            Expression value,
            Expression target,
            Type destinationType,
            CompileScope scope)
        {
            Type referenceType = typeof(PlanReference<,>).MakeGenericType(value.Type, destinationType);
            object reference = Activator.CreateInstance(referenceType, scope.Engine)!;

            Expression call = Expression.Call(
                Expression.Constant(reference, referenceType),
                referenceType.GetMethod(nameof(PlanReference<object, object>.MapInto))!,
                value,
                target,
                scope.Context);

            if (value.Type.IsValueType)
            {
                return call;
            }

            return Expression.Condition(
                Expression.Equal(value, Expression.Constant(null, value.Type)),
                Expression.Default(destinationType),
                call);
        }

        private static Expression ApplyNullSubstitute(MemberDefinition member, Expression value)
        {
            if (!member.HasNullSubstitute || member.NullSubstitute is null)
            {
                return value;
            }

            Type underlying = Nullable.GetUnderlyingType(value.Type) ?? value.Type;

            if (value.Type.IsValueType && underlying == value.Type)
            {
                return value;
            }

            return underlying.IsInstanceOfType(member.NullSubstitute)
                ? Expression.Coalesce(value, Expression.Constant(member.NullSubstitute, underlying))
                : value;
        }

        private static Expression ReadSource(
            MemberSource source,
            ParameterExpression sourceParameter,
            Expression destinationInstance,
            ParameterExpression context)
        {
            switch (source)
            {
                case MemberPathSource path:
                    return ReadPath(sourceParameter, path.Path.Steps, 0);

                case CustomSource custom when custom.Payload is LambdaExpression lambda:
                    return ParameterReplacer.Inline(lambda, sourceParameter);

                case ConstantSource constant:
                    return Expression.Constant(constant.Value, constant.ValueType);

                case ValueResolverSource resolver:
                    return BuildResolverCall(resolver, sourceParameter, destinationInstance, context);

                case IncludedMemberSource included:
                    return ReadIncluded(included, sourceParameter, destinationInstance, context);

                default:
                    throw new MapperConfigurationException("Unsupported member source: " + source.Kind + ".");
            }
        }

        /// <summary>
        /// Reads a source belonging to an included map against the member that owns it. The owner
        /// is read once into a local and guarded, so an included member that is null leaves what it
        /// would have filled at its default instead of throwing.
        /// </summary>
        private static BlockExpression ReadIncluded(
            IncludedMemberSource included,
            ParameterExpression sourceParameter,
            Expression destinationInstance,
            ParameterExpression context)
        {
            Expression access = ReadPath(sourceParameter, included.Prefix.Steps, 0);
            ParameterExpression owner = Expression.Variable(access.Type, "included");
            Expression inner = ReadSource(included.Inner, owner, destinationInstance, context);

            Expression guarded = CanBeNull(access.Type)
                ? Expression.Condition(
                    Expression.Equal(owner, Expression.Constant(null, access.Type)),
                    Expression.Default(inner.Type),
                    inner)
                : inner;

            return Expression.Block(new[] { owner }, Expression.Assign(owner, access), guarded);
        }

        private static MethodCallExpression BuildResolverCall(
            ValueResolverSource resolver,
            ParameterExpression sourceParameter,
            Expression destinationInstance,
            ParameterExpression context)
        {
            Type[] arguments = InterfaceArguments(
                resolver.ResolverType,
                typeof(IValueResolver<,,>),
                "IValueResolver<TSource, TDestination, TDestinationMember>");

            return Expression.Call(
                Method(nameof(MappingRuntime.Resolve)).MakeGenericMethod(arguments[0], arguments[1], arguments[2]),
                Expression.Constant(resolver.ResolverType, typeof(Type)),
                Expression.Convert(sourceParameter, arguments[0]),
                Expression.Convert(destinationInstance, arguments[1]),
                Expression.Default(arguments[2]),
                context);
        }

        private static Expression ReadPath(Expression instance, IReadOnlyList<MemberDescriptor> steps, int index)
        {
            Expression access = Access(instance, steps[index]);

            if (index == steps.Count - 1)
            {
                return access;
            }

            Type stepType = steps[index].MemberType;
            ParameterExpression local = Expression.Variable(stepType, "step" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Expression inner = ReadPath(local, steps, index + 1);

            Expression guarded = CanBeNull(stepType)
                ? Expression.Condition(
                    Expression.Equal(local, Expression.Constant(null, stepType)),
                    Expression.Default(inner.Type),
                    inner)
                : inner;

            return Expression.Block(new[] { local }, Expression.Assign(local, access), guarded);
        }

        private static bool CanBeNull(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        }

        private static Expression Access(Expression instance, MemberDescriptor member)
        {
            switch (member.Kind)
            {
                case MemberKind.Property:
                    return Expression.Property(instance, (PropertyInfo)member.Member);

                case MemberKind.Field:
                    return Expression.Field(instance, (FieldInfo)member.Member);

                default:
                    return Expression.Call(instance, (MethodInfo)member.Member);
            }
        }

        private static Func<object?, object?, MappingContext, object?> BuildBoxed(
            Delegate typed,
            Type sourceType,
            Type destinationType)
        {
            ParameterExpression source = Expression.Parameter(typeof(object), "source");
            ParameterExpression destination = Expression.Parameter(typeof(object), "destination");
            ParameterExpression context = Expression.Parameter(typeof(MappingContext), "context");

            Expression call = Expression.Invoke(
                Expression.Constant(typed),
                Unbox(source, sourceType),
                Unbox(destination, destinationType),
                context);

            return Expression
                .Lambda<Func<object?, object?, MappingContext, object?>>(
                    Expression.Convert(call, typeof(object)),
                    source,
                    destination,
                    context)
                .Compile();
        }

        private static Expression Unbox(ParameterExpression value, Type type)
        {
            Expression cast = Expression.Convert(value, type);

            return type.IsValueType
                ? Expression.Condition(
                    Expression.Equal(value, Expression.Constant(null, typeof(object))),
                    Expression.Default(type),
                    cast)
                : cast;
        }
    }
}
