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

        internal static Expression Build(
            Expression value,
            Type destinationType,
            MapperEngine engine,
            ParameterExpression context)
        {
            Type sourceType = value.Type;

            if (TypeClassifier.IsSequence(sourceType))
            {
                Expression? copied =
                    TryDictionary(value, sourceType, destinationType, engine, context)
                    ?? TryCollection(value, sourceType, destinationType, engine, context);

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
                return FromNullableSource(value, sourceUnderlying, destinationType, destinationUnderlying, engine, context);
            }

            if (destinationUnderlying is not null)
            {
                return ToNullableDestination(value, destinationType, destinationUnderlying, engine, context);
            }

            if (destinationType.IsAssignableFrom(sourceType))
            {
                return Expression.Convert(value, destinationType);
            }

            Expression? converted =
                TryEnum(value, sourceType, destinationType, engine)
                ?? TryNumeric(value, sourceType, destinationType)
                ?? TryToString(value, sourceType, destinationType)
                ?? TryNestedMap(value, sourceType, destinationType, engine, context)
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
            MapperEngine engine,
            ParameterExpression context)
        {
            Expression inner = Build(
                Expression.Property(value, "Value"),
                destinationUnderlying ?? destinationType,
                engine,
                context);

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
            MapperEngine engine,
            ParameterExpression context)
        {
            Expression inner = Expression.Convert(
                Build(value, destinationUnderlying, engine, context),
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
                return Expression.Call(
                    Method(nameof(MappingRuntime.ToEnum)).MakeGenericMethod(sourceType, destinationType),
                    value,
                    Expression.Constant(policy));
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

        private static Expression? TryDictionary(
            Expression value,
            Type sourceType,
            Type destinationType,
            MapperEngine engine,
            ParameterExpression context)
        {
            if (!TypeClassifier.TryGetDictionaryTypes(sourceType, out Type? sourceKey, out Type? sourceValue))
            {
                return null;
            }

            if (!TryGetDestinationDictionary(destinationType, out Type? destinationKey, out Type? destinationValue))
            {
                return null;
            }

            Delegate keyConverter = ElementConverter(sourceKey, destinationKey!, engine, out Type keyConverterType);
            Delegate valueConverter = ElementConverter(sourceValue, destinationValue!, engine, out Type valueConverterType);

            Type entryType = typeof(KeyValuePair<,>).MakeGenericType(sourceKey, sourceValue);
            Expression entries = Expression.Convert(value, typeof(IEnumerable<>).MakeGenericType(entryType));

            Expression built = Expression.Call(
                Method(nameof(MappingRuntime.ToDictionary))
                    .MakeGenericMethod(sourceKey, sourceValue, destinationKey!, destinationValue!),
                entries,
                context,
                Expression.Constant(keyConverter, keyConverterType),
                Expression.Constant(valueConverter, valueConverterType),
                Expression.Constant(engine.Model.Options.AllowNullCollections));

            return built.Type == destinationType ? built : Expression.Convert(built, destinationType);
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
            MapperEngine engine,
            out Type converterType)
        {
            ParameterExpression element = Expression.Parameter(sourceType, "element");
            ParameterExpression elementContext = Expression.Parameter(typeof(MappingContext), "context");

            converterType = typeof(Func<,,>).MakeGenericType(sourceType, typeof(MappingContext), destinationType);

            return Expression
                .Lambda(converterType, Build(element, destinationType, engine, elementContext), element, elementContext)
                .Compile();
        }

        private static Expression? TryCollection(
            Expression value,
            Type sourceType,
            Type destinationType,
            MapperEngine engine,
            ParameterExpression context)
        {
            if (!TypeClassifier.TryGetElementType(sourceType, out Type? sourceElement))
            {
                return null;
            }

            if (!TryGetDestinationCollection(destinationType, out Type? destinationElement, out string? helper))
            {
                return null;
            }

            Delegate converter = ElementConverter(sourceElement, destinationElement!, engine, out Type converterType);

            Expression sequence = Expression.Convert(value, typeof(IEnumerable<>).MakeGenericType(sourceElement));

            Expression built = Expression.Call(
                Method(helper!).MakeGenericMethod(sourceElement, destinationElement!),
                sequence,
                context,
                Expression.Constant(converter, converterType),
                Expression.Constant(engine.Model.Options.AllowNullCollections));

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
            MapperEngine engine,
            ParameterExpression context)
        {
            if (!engine.CanMap(new TypeMapKey(sourceType, destinationType)))
            {
                return null;
            }

            Expression call = Expression.Call(
                Method(nameof(MappingRuntime.MapValue)).MakeGenericMethod(sourceType, destinationType),
                value,
                context);

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
