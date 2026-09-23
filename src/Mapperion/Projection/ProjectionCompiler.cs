using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using Mapperion.Compilation;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Projection
{
    /// <summary>
    /// Builds the <c>Expression&lt;Func&lt;TSource, TDestination&gt;&gt;</c> a LINQ provider can
    /// translate. It shares the configuration model with the runtime compiler but not a line of its
    /// output: a query provider understands member initialisation, member access, conditionals and
    /// <c>Select</c>, and nothing else. No blocks, no locals, no calls into this library.
    /// </summary>
    /// <remarks>
    /// Because the projection never runs our code, anything that depends on it cannot be projected:
    /// type converters, value converters, value resolvers and the before and after steps. Each is
    /// reported rather than skipped, so a projection never quietly differs from the same map run
    /// through <c>Map</c>.
    /// </remarks>
    [RequiresUnreferencedCode("Building a projection inspects types by reflection.")]
    [RequiresDynamicCode("Building a projection emits code at run time.")]
    internal static class ProjectionCompiler
    {
        private static readonly HashSet<Type> Numeric = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(char), typeof(float), typeof(double), typeof(decimal),
        };

        internal static LambdaExpression Compile(TypeMapDefinition definition, MapperEngine engine)
        {
            ParameterExpression source = Expression.Parameter(definition.SourceType, "source");
            Expression body = BuildInstance(definition, source, engine, new List<TypeMapKey>());

            Type delegateType = typeof(Func<,>).MakeGenericType(definition.SourceType, definition.DestinationType);
            return Expression.Lambda(delegateType, body, source);
        }

        private static Expression BuildInstance(
            TypeMapDefinition definition,
            Expression source,
            MapperEngine engine,
            List<TypeMapKey> open)
        {
            Reject(definition);

            NewExpression instance = definition.Constructor is not null
                ? Expression.New(definition.Constructor, BuildArguments(definition, source, engine, open))
                : NewParameterless(definition);

            var bindings = new List<MemberBinding>();

            foreach (MemberDefinition member in definition.Members)
            {
                if (member.IsIgnored || member.Source is null || member.DestinationMember.Kind == MemberKind.Method)
                {
                    continue;
                }

                Expression value = BuildMember(definition, member, source, engine, open);
                bindings.Add(Expression.Bind(member.DestinationMember.Member, value));
            }

            return bindings.Count == 0 ? instance : Expression.MemberInit(instance, bindings);
        }

        private static void Reject(TypeMapDefinition definition)
        {
            if (definition.TypeConverterType is not null)
            {
                throw new MapperConfigurationException(
                    definition.Key + " uses a type converter, which a query provider cannot run. " +
                    "Project to a type without one, or query the entities and map them in memory.");
            }

            if (definition.ConstructUsing is not null)
            {
                throw new MapperConfigurationException(
                    definition.Key + " builds its destination with a factory, which a query " +
                    "provider cannot call. Project to a type without one, or query the entities " +
                    "and map them in memory.");
            }

            foreach (MemberDefinition member in definition.Members)
            {
                if (member.IsPath)
                {
                    throw new MapperConfigurationException(
                        definition.Key + " writes into '" + member.DestinationPath +
                        "' with ForPath, which a projection cannot express: it builds each object " +
                        "in a single expression, with nothing to walk into afterwards. Project to a " +
                        "flatter type, or query the entities and map them in memory.");
                }
            }

            if (definition.DerivedMaps.Count != 0)
            {
                throw new MapperConfigurationException(
                    definition.Key + " dispatches to derived maps, which a query provider cannot do: " +
                    "the shape of a projection is fixed before any row is read. Project the derived " +
                    "types separately, or query the entities and map them in memory.");
            }

            if (definition.BeforeMapActions.Count != 0 || definition.AfterMapActions.Count != 0)
            {
                throw new MapperConfigurationException(
                    definition.Key + " has a before or after step, which a query provider cannot run. " +
                    "AutoMapper skips them silently in ProjectTo; Mapperion reports them so a " +
                    "projection never differs from the same map run through Map.");
            }
        }

        private static NewExpression NewParameterless(TypeMapDefinition definition)
        {
            if (definition.DestinationType.GetConstructor(Type.EmptyTypes) is null &&
                !definition.DestinationType.IsValueType)
            {
                throw new MapperConfigurationException(
                    definition.DestinationType.Name + " cannot be created in a projection: it has no " +
                    "parameterless constructor and no constructor whose arguments could be resolved.");
            }

            return Expression.New(definition.DestinationType);
        }

        private static Expression[] BuildArguments(
            TypeMapDefinition definition,
            Expression source,
            MapperEngine engine,
            List<TypeMapKey> open)
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

                arguments[i] = Convert(
                    Read(definition, parameter.Name, parameter.Source, source),
                    parameter.ParameterType,
                    engine,
                    open);
            }

            return arguments;
        }

        private static Expression BuildMember(
            TypeMapDefinition definition,
            MemberDefinition member,
            Expression source,
            MapperEngine engine,
            List<TypeMapKey> open)
        {
            if (member.ValueConverterType is not null)
            {
                throw new MapperConfigurationException(
                    definition.Key + ": member '" + member.DestinationMember.Name + "' uses a value " +
                    "converter, which a query provider cannot run. Express it with MapFrom instead.");
            }

            Expression value = Read(definition, member.DestinationMember.Name, member.Source!, source);
            Type destinationType = member.DestinationMember.MemberType;

            if (member.HasNullSubstitute && member.NullSubstitute is not null)
            {
                Type underlying = Nullable.GetUnderlyingType(value.Type) ?? value.Type;

                if (underlying.IsInstanceOfType(member.NullSubstitute) && !value.Type.IsValueType)
                {
                    value = Expression.Coalesce(value, Expression.Constant(member.NullSubstitute, underlying));
                }
            }

            Expression converted = Convert(value, destinationType, engine, open);

            if (!engine.Model.Options.AllowNullDestinationValues && destinationType == typeof(string))
            {
                converted = Expression.Coalesce(converted, Expression.Constant(string.Empty));
            }

            if (member.Condition is LambdaExpression condition)
            {
                converted = Expression.Condition(
                    ParameterReplacer.Inline(condition, source),
                    converted,
                    Absent(destinationType));
            }

            if (member.PreCondition is LambdaExpression preCondition)
            {
                converted = Expression.Condition(
                    ParameterReplacer.Inline(preCondition, source),
                    converted,
                    Absent(destinationType));
            }

            return converted;
        }

        private static Expression Read(
            TypeMapDefinition definition,
            string what,
            MemberSource memberSource,
            Expression source)
        {
            switch (memberSource)
            {
                case MemberPathSource path:
                    return ReadPath(source, path);

                case CustomSource custom when custom.Payload is LambdaExpression lambda:
                    return ParameterReplacer.Inline(lambda, source);

                case ConstantSource constant:
                    return Expression.Constant(constant.Value, constant.ValueType);

                case IncludedMemberSource included when included.Inner is not ValueResolverSource:
                    return Read(
                        definition,
                        what,
                        included.Inner,
                        ReadPath(source, new MemberPathSource(included.Prefix)));

                case ValueResolverSource resolver:
                    throw new MapperConfigurationException(
                        definition.Key + ": '" + what + "' uses the resolver " + resolver.ResolverType.Name +
                        ", which a query provider cannot run. Express it with MapFrom instead.");

                default:
                    throw new MapperConfigurationException(
                        definition.Key + ": '" + what + "' cannot be projected.");
            }
        }

        /// <summary>
        /// The value a member takes when there is nothing to read, written as a constant.
        /// </summary>
        /// <remarks>
        /// <c>Expression.Default</c> would say the same thing more directly, and Entity Framework
        /// Core translates it, but Entity Framework 6 does not: its provider stops at
        /// "Unknown LINQ expression of type 'Default'". A constant is understood by both, and by
        /// every other provider, so the projection emits one.
        /// </remarks>
        private static ConstantExpression Absent(Type type)
        {
            return Expression.Constant(
                type.IsValueType ? Activator.CreateInstance(type) : null,
                type);
        }

        private static Expression ReadPath(Expression source, MemberPathSource path)
        {
            Expression access = source;

            foreach (MemberDescriptor step in path.Path.Steps)
            {
                access = Access(access, step);
            }

            return access;
        }

        private static Expression Access(Expression instance, MemberDescriptor member)
        {
            switch (member.Kind)
            {
                case MemberKind.Property:
                    return Expression.Property(instance, (System.Reflection.PropertyInfo)member.Member);

                case MemberKind.Field:
                    return Expression.Field(instance, (System.Reflection.FieldInfo)member.Member);

                default:
                    return Expression.Call(instance, (System.Reflection.MethodInfo)member.Member);
            }
        }

        private static Expression Convert(
            Expression value,
            Type destinationType,
            MapperEngine engine,
            List<TypeMapKey> open)
        {
            Type sourceType = value.Type;

            if (sourceType == destinationType)
            {
                return value;
            }

            if (destinationType.IsAssignableFrom(sourceType))
            {
                return Expression.Convert(value, destinationType);
            }

            Type underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
            Type underlyingDestination = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

            if (IsDirectlyConvertible(underlyingSource, underlyingDestination))
            {
                return Expression.Convert(value, destinationType);
            }

            if (destinationType == typeof(string))
            {
                return Expression.Call(value, typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!);
            }

            if (TypeClassifier.TryGetDictionaryTypes(sourceType, out _, out _))
            {
                throw new MapperConfigurationException(
                    "A projection cannot build a dictionary from '" + sourceType.Name +
                    "'. A query provider has no way to materialise one; query the entities and map " +
                    "them in memory instead.");
            }

            Expression? projected =
                TryCollection(value, sourceType, destinationType, engine, open)
                ?? TryNested(value, sourceType, destinationType, engine, open);

            if (projected is not null)
            {
                return projected;
            }

            throw new MapperConfigurationException(
                "A projection cannot convert '" + sourceType.Name + "' to '" + destinationType.Name +
                "'. Declare a map for the pair, or express the member with MapFrom.");
        }

        private static bool IsDirectlyConvertible(Type source, Type destination)
        {
            bool sourceIsNumberLike = Numeric.Contains(source) || source.IsEnum;
            bool destinationIsNumberLike = Numeric.Contains(destination) || destination.IsEnum;

            return sourceIsNumberLike && destinationIsNumberLike;
        }

        private static Expression? TryCollection(
            Expression value,
            Type sourceType,
            Type destinationType,
            MapperEngine engine,
            List<TypeMapKey> open)
        {
            if (!TypeClassifier.TryGetElementType(sourceType, out Type? sourceElement))
            {
                return null;
            }

            Type? destinationElement;
            bool toArray = destinationType.IsArray;

            if (toArray)
            {
                destinationElement = destinationType.GetElementType();
            }
            else if (!TypeClassifier.TryGetElementType(destinationType, out destinationElement))
            {
                return null;
            }

            ParameterExpression element = Expression.Parameter(sourceElement, "element");
            Expression converted = Convert(element, destinationElement!, engine, open);
            LambdaExpression selector = Expression.Lambda(converted, element);

            Expression selected = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Select),
                new[] { sourceElement, destinationElement! },
                value,
                selector);

            Expression materialised = Expression.Call(
                typeof(Enumerable),
                toArray ? nameof(Enumerable.ToArray) : nameof(Enumerable.ToList),
                new[] { destinationElement! },
                selected);

            return destinationType.IsAssignableFrom(materialised.Type)
                ? materialised
                : Expression.Convert(materialised, destinationType);
        }

        private static Expression? TryNested(
            Expression value,
            Type sourceType,
            Type destinationType,
            MapperEngine engine,
            List<TypeMapKey> open)
        {
            var key = new TypeMapKey(sourceType, destinationType);

            if (!engine.TryGetDefinition(key, out TypeMapDefinition? nested))
            {
                return null;
            }

            if (open.Contains(key))
            {
                throw new MapperConfigurationException(
                    key + " refers to itself. A projection has to be expanded up front, so a cycle " +
                    "has no end: ignore the member that closes it, or query the entities and map " +
                    "them in memory.");
            }

            open.Add(key);
            Expression instance = BuildInstance(nested!, value, engine, open);
            open.RemoveAt(open.Count - 1);

            return sourceType.IsValueType
                ? instance
                : Expression.Condition(
                    Expression.Equal(value, Expression.Constant(null, sourceType)),
                    Absent(destinationType),
                    instance);
        }
    }
}
