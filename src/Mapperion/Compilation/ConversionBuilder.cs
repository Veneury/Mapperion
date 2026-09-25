using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Mapperion.Execution;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Produces the expression that turns a source value into the destination member's type.
    /// Everything it emits is typed: nothing here boxes a value or falls back to <c>object</c>
    /// unless the conversion itself has no other way through.
    /// </summary>
    /// <remarks>
    /// Sequences are looked at before anything else, including before the source and destination
    /// types being the same or assignable. A destination that shared the source's list would let a
    /// change to one show up in the other, and it would also slip past
    /// <c>AllowNullCollections</c>; a collection is always rebuilt.
    /// </remarks>
    [RequiresUnreferencedCode("Building a conversion inspects types by reflection.")]
    [RequiresDynamicCode("Building a conversion emits code at run time.")]
    internal static class ConversionBuilder
    {
        private static readonly HashSet<Type> Numeric = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(char), typeof(float), typeof(double), typeof(decimal),
        };

        internal static Expression Build(Expression value, Type destinationType, CompileScope scope)
        {
            Type sourceType = value.Type;

            if (TypeClassifier.IsSequence(sourceType))
            {
                Expression? copied =
                    TryDictionary(value, sourceType, destinationType, scope)
                    ?? TryCollection(value, sourceType, destinationType, scope);

                if (copied is not null)
                {
                    return copied;
                }
            }

            if (sourceType == destinationType)
            {
                return value;
            }

            Type? sourceUnderlying = Nullable.GetUnderlyingType(sourceType);
            Type? destinationUnderlying = Nullable.GetUnderlyingType(destinationType);

            if (sourceUnderlying is not null)
            {
                return FromNullableSource(value, sourceUnderlying, destinationType, destinationUnderlying, scope);
            }

            if (destinationUnderlying is not null)
            {
                return ToNullableDestination(value, destinationType, destinationUnderlying, scope);
            }

            if (destinationType.IsAssignableFrom(sourceType))
            {
                return Expression.Convert(value, destinationType);
            }

            Expression? converted =
                TryEnum(value, sourceType, destinationType, scope.Engine)
                ?? TryNumeric(value, sourceType, destinationType)
                ?? TryToString(value, sourceType, destinationType)
                ?? TryNestedMap(value, sourceType, destinationType, scope)
                ?? TryChangeType(value, sourceType, destinationType);

            if (converted is not null)
            {
                return converted;
            }

            throw new MapperConfigurationException(
                "No conversion from '" + sourceType.Name + "' to '" + destinationType.Name +
                "' is available. Declare a map for the pair, or configure the member with MapFrom.");
        }

        private static ConditionalExpression FromNullableSource(
            Expression value,
            Type sourceUnderlying,
            Type destinationType,
            Type? destinationUnderlying,
            CompileScope scope)
        {
            Expression inner = Build(
                Expression.Property(value, "Value"),
                destinationUnderlying ?? destinationType,
                scope);

            if (inner.Type != destinationType)
            {
                inner = Expression.Convert(inner, destinationType);
            }

            return Expression.Condition(
                Expression.Property(value, "HasValue"),
                inner,
                Expression.Default(destinationType));
        }

        private static Expression ToNullableDestination(
            Expression value,
            Type destinationType,
            Type destinationUnderlying,
            CompileScope scope)
        {
            Expression inner = Expression.Convert(
                Build(value, destinationUnderlying, scope),
                destinationType);

            if (value.Type.IsValueType)
            {
                return inner;
            }

            return Expression.Condition(
                Expression.Equal(value, Expression.Constant(null, value.Type)),
                Expression.Default(destinationType),
                inner);
        }

        private static Expression? TryEnum(Expression value, Type sourceType, Type destinationType, MapperEngine engine)
        {
            EnumMappingPolicy policy = engine.Model.Options.EnumMapping;

            if (sourceType.IsEnum && destinationType.IsEnum)
            {
                MethodCallExpression atRunTime = Expression.Call(
                    Method(nameof(MappingRuntime.ToEnum)).MakeGenericMethod(sourceType, destinationType),
                    value,
                    Expression.Constant(policy));

                return policy == EnumMappingPolicy.ByValue
                    ? atRunTime
                    : ByName(value, sourceType, destinationType, policy, atRunTime);
            }

            if (sourceType == typeof(string) && destinationType.IsEnum)
            {
                return Expression.Call(
                    Method(nameof(MappingRuntime.ParseEnum)).MakeGenericMethod(destinationType),
                    value,
                    Expression.Constant(policy));
            }

            if (sourceType.IsEnum && Numeric.Contains(destinationType))
            {
                return Expression.Convert(value, destinationType);
            }

            if (Numeric.Contains(sourceType) && destinationType.IsEnum)
            {
                return Expression.Convert(value, destinationType);
            }

            return null;
        }

        /// <summary>
        /// Settles which destination member each source member becomes while the plan is compiled,
        /// and emits the answer as a switch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both types are known here, so the correspondence between their names is knowable here
        /// too. Working it out per call instead cost a <c>ToString</c> for the source name, an
        /// <c>Enum.TryParse</c> against the destination, and a second <c>ToString</c> to confirm the
        /// name really came back — per member, per map. On five enums that was twenty-five times a
        /// hand-written switch and seven times its allocations, which is what B06 was written to
        /// find out.
        /// </para>
        /// <para>
        /// Only the members that can be settled here become cases. Anything else — a value outside
        /// the ones declared, a combination of flags, a name the destination does not have under
        /// <see cref="EnumMappingPolicy.ByName"/> — falls to the default, which is the same
        /// run-time call as before. So the answers do not change, including the exception that
        /// names the value that had nowhere to go.
        /// </para>
        /// </remarks>
        private static Expression ByName(
            Expression value,
            Type sourceType,
            Type destinationType,
            EnumMappingPolicy policy,
            Expression atRunTime)
        {
            Type underlyingType = Enum.GetUnderlyingType(sourceType);
            var cases = new List<SwitchCase>();
            var seen = new HashSet<object>();

            foreach (object member in Enum.GetValues(sourceType))
            {
                object underlying = Convert.ChangeType(member, underlyingType, CultureInfo.InvariantCulture);

                if (!seen.Add(underlying))
                {
                    continue;
                }

                object? resolved = Counterpart(member, sourceType, destinationType, policy, underlying);

                if (resolved is not null)
                {
                    cases.Add(Expression.SwitchCase(
                        Expression.Constant(resolved, destinationType),
                        Expression.Constant(member, sourceType)));
                }
            }

            return cases.Count == 0
                ? atRunTime
                : Expression.Switch(destinationType, value, atRunTime, null, cases);
        }

        /// <summary>
        /// The destination member one source member becomes, or <see langword="null"/> when that
        /// cannot be decided now and the run-time call has to answer it.
        /// </summary>
        private static object? Counterpart(
            object member,
            Type sourceType,
            Type destinationType,
            EnumMappingPolicy policy,
            object underlying)
        {
            string? name = Enum.GetName(sourceType, member);

            if (name is not null && Enum.IsDefined(destinationType, name))
            {
                object parsed = Enum.Parse(destinationType, name);

                // The name has to come back out as it went in. Where two destination members share
                // a value, only one of them is what that value prints as, and the run-time path
                // rejects the other for the same reason.
                if (string.Equals(Enum.GetName(destinationType, parsed), name, StringComparison.Ordinal))
                {
                    return parsed;
                }
            }

            return policy == EnumMappingPolicy.ByNameThenValue
                ? Enum.ToObject(destinationType, underlying)
                : null;
        }

        private static UnaryExpression? TryNumeric(Expression value, Type sourceType, Type destinationType)
        {
            return Numeric.Contains(sourceType) && Numeric.Contains(destinationType)
                ? Expression.Convert(value, destinationType)
                : null;
        }

        private static Expression? TryToString(Expression value, Type sourceType, Type destinationType)
        {
            if (destinationType != typeof(string))
            {
                return null;
            }

            MethodInfo toString = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
            Expression call = Expression.Call(value, toString);

            if (sourceType.IsValueType)
            {
                return call;
            }

            return Expression.Condition(
                Expression.Equal(value, Expression.Constant(null, sourceType)),
                Expression.Constant(null, typeof(string)),
                call);
        }

        /// <summary>
        /// Emits the call into a nested map through a reference that resolves the plan once, rather
        /// than a helper that looks it up on every value.
        /// </summary>
        internal static MethodCallExpression NestedMapCall(
            Type sourceType,
            Type destinationType,
            Expression value,
            CompileScope scope)
        {
            Type referenceType = typeof(PlanReference<,>).MakeGenericType(sourceType, destinationType);
            object reference = Activator.CreateInstance(referenceType, scope.Engine)!;

            return Expression.Call(
                Expression.Constant(reference, referenceType),
                referenceType.GetMethod(nameof(PlanReference<object, object>.Map))!,
                value,
                scope.Context);
        }

        private static Expression? TryDictionary(
            Expression value,
            Type sourceType,
            Type destinationType,
            CompileScope scope)
        {
            if (!TypeClassifier.TryGetDictionaryTypes(sourceType, out Type? sourceKey, out Type? sourceValue))
            {
                return null;
            }

            if (!TryGetDestinationDictionary(destinationType, out Type? destinationKey, out Type? destinationValue))
            {
                return null;
            }

            Delegate keyConverter = ElementConverter(sourceKey, destinationKey!, scope, out Type keyConverterType);
            Delegate valueConverter = ElementConverter(sourceValue, destinationValue!, scope, out Type valueConverterType);

            Type entryType = typeof(KeyValuePair<,>).MakeGenericType(sourceKey, sourceValue);
            Expression entries = Expression.Convert(value, typeof(IEnumerable<>).MakeGenericType(entryType));

            Expression built = Expression.Call(
                Method(nameof(MappingRuntime.ToDictionary))
                    .MakeGenericMethod(sourceKey, sourceValue, destinationKey!, destinationValue!),
                entries,
                scope.Context,
                Expression.Constant(keyConverter, keyConverterType),
                Expression.Constant(valueConverter, valueConverterType),
                Expression.Constant(scope.Engine.Model.Options.AllowNullCollections));

            return built.Type == destinationType ? built : Expression.Convert(built, destinationType);
        }

        /// <summary>
        /// Emits the loop directly when the source can be indexed and the destination is a list or
        /// an array, which covers nearly every collection member in practice.
        /// </summary>
        /// <remarks>
        /// The general path compiles the element conversion into a delegate and calls it once per
        /// element. That costs an indirect call per element and stops the JIT from seeing through
        /// the conversion. Here the conversion is emitted inside the loop body instead, and the
        /// destination is allocated at the right size up front. Anything that is only an
        /// <c>IEnumerable</c> still takes the general path: walking it needs an enumerator and a
        /// try/finally, which is a good deal more emitted code for a case that is far rarer.
        /// </remarks>
        private static Expression? TryInlineLoop(
            Expression value,
            Type sourceType,
            Type destinationType,
            Type sourceElement,
            Type destinationElement,
            CompileScope scope)
        {
            Type indexable = typeof(IList<>).MakeGenericType(sourceElement);

            if (!indexable.IsAssignableFrom(sourceType))
            {
                return null;
            }

            bool toArray = destinationType.IsArray;
            Type listType = typeof(List<>).MakeGenericType(destinationElement);
            Type builtType = toArray ? destinationElement.MakeArrayType() : listType;

            if (!toArray && !destinationType.IsAssignableFrom(listType))
            {
                return null;
            }

            ParameterExpression source = Expression.Variable(indexable, "sequence");
            ParameterExpression result = Expression.Variable(builtType, "mapped");
            ParameterExpression count = Expression.Variable(typeof(int), "count");
            ParameterExpression index = Expression.Variable(typeof(int), "index");
            LabelTarget done = Expression.Label("done");

            Expression item = Expression.MakeIndex(
                source,
                indexable.GetProperty("Item"),
                new[] { (Expression)index });

            var elementStep = new StepSlot(Expression.Variable(typeof(string), "elementStep"));
            Expression converted = Build(item, destinationElement, scope.ForElement(elementStep));

            Expression add = toArray
                ? Expression.Assign(Expression.ArrayAccess(result, index), converted)
                : Expression.Call(result, listType.GetMethod("Add")!, converted);

            Expression step = elementStep.Used
                ? Expression.Assign(elementStep.Variable, Expression.Constant(string.Empty))
                : (Expression)Expression.Empty();

            Expression allocate = toArray
                ? Expression.NewArrayBounds(destinationElement, count)
                : Expression.New(listType.GetConstructor(new[] { typeof(int) })!, count);

            Expression empty = toArray
                ? Expression.NewArrayBounds(destinationElement, Expression.Constant(0))
                : (Expression)Expression.New(listType.GetConstructor(Type.EmptyTypes)!);

            Expression whenNull = scope.Engine.Model.Options.AllowNullCollections
                ? Expression.Default(builtType)
                : empty;

            Expression fill = Expression.Block(
                Expression.Assign(
                    count,
                    Expression.Property(
                        source,
                        typeof(ICollection<>).MakeGenericType(sourceElement).GetProperty("Count")!)),
                Expression.Assign(result, allocate),
                Expression.Assign(index, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.LessThan(index, count),
                        Expression.Block(step, add, Expression.PostIncrementAssign(index)),
                        Expression.Break(done)),
                    done));

            ParameterExpression error = Expression.Parameter(typeof(Exception), "error");

            Expression reported = Expression.TryCatch(
                Expression.Block(typeof(void), fill),
                Expression.Catch(
                    error,
                    Expression.Block(
                        typeof(void),
                        Expression.Assign(
                            result,
                            Expression.Call(
                                Method(nameof(MappingRuntime.FailAtIndex)).MakeGenericMethod(builtType),
                                index,
                                elementStep.Used
                                    ? (Expression)elementStep.Variable
                                    : Expression.Constant(string.Empty),
                                error)))));

            var locals = new List<ParameterExpression> { source, result, count, index };

            if (elementStep.Used)
            {
                locals.Add(elementStep.Variable);
            }

            Expression body = Expression.Block(
                locals,
                Expression.Assign(source, Expression.Convert(value, indexable)),
                Expression.IfThenElse(
                    Expression.Equal(source, Expression.Constant(null, indexable)),
                    Expression.Assign(result, whenNull),
                    reported),
                result);

            return destinationType.IsAssignableFrom(builtType)
                ? body
                : Expression.Convert(body, destinationType);
        }

        private static bool TryGetDestinationDictionary(
            Type destinationType,
            out Type? keyType,
            out Type? valueType)
        {
            if (destinationType.IsGenericType)
            {
                Type definition = destinationType.GetGenericTypeDefinition();

                if (definition == typeof(Dictionary<,>) ||
                    definition == typeof(IDictionary<,>) ||
                    definition == typeof(IReadOnlyDictionary<,>))
                {
                    Type[] arguments = destinationType.GetGenericArguments();
                    keyType = arguments[0];
                    valueType = arguments[1];
                    return true;
                }
            }

            keyType = null;
            valueType = null;
            return false;
        }

        private static Delegate ElementConverter(
            Type sourceType,
            Type destinationType,
            CompileScope scope,
            out Type converterType)
        {
            ParameterExpression element = Expression.Parameter(sourceType, "element");
            ParameterExpression elementContext = Expression.Parameter(typeof(MappingContext), "context");

            converterType = typeof(Func<,,>).MakeGenericType(sourceType, typeof(MappingContext), destinationType);

            return Expression
                .Lambda(
                    converterType,
                    Build(element, destinationType, scope.ForLambda(elementContext)),
                    element,
                    elementContext)
                .Compile();
        }

        private static Expression? TryCollection(
            Expression value,
            Type sourceType,
            Type destinationType,
            CompileScope scope)
        {
            if (!TypeClassifier.TryGetElementType(sourceType, out Type? sourceElement))
            {
                return null;
            }

            if (!TryGetDestinationCollection(destinationType, out Type? destinationElement, out string? helper))
            {
                return null;
            }

            Expression? inlined = TryInlineLoop(
                value,
                sourceType,
                destinationType,
                sourceElement,
                destinationElement!,
                scope);

            if (inlined is not null)
            {
                return inlined;
            }

            Delegate converter = ElementConverter(sourceElement, destinationElement!, scope, out Type converterType);

            Expression sequence = Expression.Convert(value, typeof(IEnumerable<>).MakeGenericType(sourceElement));

            Expression built = Expression.Call(
                Method(helper!).MakeGenericMethod(sourceElement, destinationElement!),
                sequence,
                scope.Context,
                Expression.Constant(converter, converterType),
                Expression.Constant(scope.Engine.Model.Options.AllowNullCollections));

            return built.Type == destinationType ? built : Expression.Convert(built, destinationType);
        }

        private static bool TryGetDestinationCollection(
            Type destinationType,
            out Type? elementType,
            out string? helper)
        {
            if (destinationType.IsArray)
            {
                elementType = destinationType.GetElementType();
                helper = nameof(MappingRuntime.ToArray);
                return elementType is not null;
            }

            if (destinationType.IsGenericType)
            {
                Type definition = destinationType.GetGenericTypeDefinition();
                elementType = destinationType.GetGenericArguments()[0];

                if (definition == typeof(HashSet<>) || definition == typeof(ISet<>))
                {
                    helper = nameof(MappingRuntime.ToHashSet);
                    return true;
                }

                if (definition == typeof(List<>) ||
                    definition == typeof(IList<>) ||
                    definition == typeof(ICollection<>) ||
                    definition == typeof(IEnumerable<>) ||
                    definition == typeof(IReadOnlyList<>) ||
                    definition == typeof(IReadOnlyCollection<>))
                {
                    helper = nameof(MappingRuntime.ToList);
                    return true;
                }
            }

            elementType = null;
            helper = null;
            return false;
        }

        private static Expression? TryNestedMap(
            Expression value,
            Type sourceType,
            Type destinationType,
            CompileScope scope)
        {
            var key = new TypeMapKey(sourceType, destinationType);

            if (!scope.Engine.CanMap(key))
            {
                return null;
            }

            Expression? written = PlanCompiler.TryInline(key, value, scope);

            if (written is not null)
            {
                return written;
            }

            Expression call = NestedMapCall(sourceType, destinationType, value, scope);

            if (sourceType.IsValueType)
            {
                return call;
            }

            return Expression.Condition(
                Expression.Equal(value, Expression.Constant(null, sourceType)),
                Expression.Default(destinationType),
                call);
        }

        private static MethodCallExpression? TryChangeType(Expression value, Type sourceType, Type destinationType)
        {
            if (!typeof(IConvertible).IsAssignableFrom(sourceType) ||
                !typeof(IConvertible).IsAssignableFrom(destinationType))
            {
                return null;
            }

            return Expression.Call(
                Method(nameof(MappingRuntime.ChangeType)).MakeGenericMethod(destinationType),
                Expression.Convert(value, typeof(object)));
        }

        private static MethodInfo Method(string name)
        {
            return typeof(MappingRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        }
    }
}
