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

            var body = new List<Expression>
            {
                Expression.Assign(result, CreateDestination(definition, destination, source, context, engine)),
            };

            foreach (MemberDefinition member in Ordered(definition.Members))
            {
                Expression? assignment = BuildAssignment(member, result, source, context, engine);

                if (assignment is not null)
                {
                    body.Add(assignment);
                }
            }

            body.Add(result);

            Expression block = Expression.Block(new[] { result }, body);

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
            ParameterExpression context,
            MapperEngine engine)
        {
            Type destinationType = definition.DestinationType;

            if (definition.Constructor is not null)
            {
                Expression created = Expression.New(
                    definition.Constructor,
                    BuildArguments(definition, source, context, engine));

                return destinationType.IsValueType ? created : Expression.Coalesce(destination, created);
            }

            if (destinationType.IsValueType)
            {
                return destination;
            }

            ConstructorInfo? parameterless = destinationType.GetConstructor(Type.EmptyTypes);

            if (parameterless is null)
            {
                throw new MapperConfigurationException(
                    destinationType.Name + " cannot be created: it has no parameterless constructor " +
                    "and no constructor whose arguments could be resolved from " +
                    definition.SourceType.Name + ".");
            }

            return Expression.Coalesce(destination, Expression.New(parameterless));
        }

        private static Expression[] BuildArguments(
            TypeMapDefinition definition,
            ParameterExpression source,
            ParameterExpression context,
            MapperEngine engine)
        {
            var ordered = new List<ConstructorParameterDefinition>(definition.ConstructorParameters);
            ordered.Sort(static (left, right) => left.Position.CompareTo(right.Position));

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
                    ReadSource(parameter.Source, source),
                    parameter.ParameterType,
                    engine,
                    context);
            }

            return arguments;
        }

        private static Expression? BuildAssignment(
            MemberDefinition member,
            ParameterExpression result,
            ParameterExpression source,
            ParameterExpression context,
            MapperEngine engine)
        {
            if (member.IsIgnored || member.Source is null)
            {
                return null;
            }

            Expression value = ReadSource(member.Source, source);
            value = ApplyNullSubstitute(member, value);

            Type destinationType = member.DestinationMember.MemberType;
            Expression target = Access(result, member.DestinationMember);

            Expression converted = member.UseDestinationValue &&
                engine.Model.Contains(new TypeMapKey(value.Type, destinationType))
                    ? MapIntoExisting(value, target, destinationType, context)
                    : ConversionBuilder.Build(value, destinationType, engine, context);

            Expression assignment = Expression.Assign(target, converted);

            if (member.Condition is LambdaExpression condition)
            {
                assignment = Expression.IfThen(ParameterReplacer.Inline(condition, source), assignment);
            }

            return assignment;
        }

        private static Expression MapIntoExisting(
            Expression value,
            Expression target,
            Type destinationType,
            ParameterExpression context)
        {
            MethodInfo method = typeof(MappingRuntime)
                .GetMethod(nameof(MappingRuntime.MapInto), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(value.Type, destinationType);

            Expression call = Expression.Call(method, value, target, context);

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

        private static Expression ReadSource(MemberSource source, ParameterExpression sourceParameter)
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
                    throw new MapperConfigurationException(
                        "Value resolvers are not implemented yet: " + resolver.ResolverType.Name + ".");

                default:
                    throw new MapperConfigurationException("Unsupported member source: " + source.Kind + ".");
            }
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
