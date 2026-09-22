using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Mapperion.Internal
{
    /// <summary>
    /// Answers the questions the rest of the library asks about a type: whether it is a value the
    /// engine can convert on its own, whether it is a sequence, and what it holds underneath.
    /// </summary>
    [RequiresUnreferencedCode("Inspecting the interfaces of a type is not compatible with trimming.")]
    internal static class TypeClassifier
    {
        internal static Type Underlying(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        internal static bool IsSimple(Type type)
        {
            Type actual = Underlying(type);

            if (actual.IsPrimitive || actual.IsEnum)
            {
                return true;
            }

            return actual == typeof(string)
                || actual == typeof(decimal)
                || actual == typeof(DateTime)
                || actual == typeof(DateTimeOffset)
                || actual == typeof(TimeSpan)
                || actual == typeof(Guid)
                || actual == typeof(Uri)
#if NET8_0_OR_GREATER
                || actual == typeof(DateOnly)
                || actual == typeof(TimeOnly)
#endif
                ;
        }

        internal static bool TryGetDictionaryTypes(
            Type type,
            [NotNullWhen(true)] out Type? keyType,
            [NotNullWhen(true)] out Type? valueType)
        {
            if (IsDictionaryOf(type, out keyType, out valueType))
            {
                return true;
            }

            foreach (Type contract in type.GetInterfaces())
            {
                if (IsDictionaryOf(contract, out keyType, out valueType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDictionaryOf(
            Type type,
            [NotNullWhen(true)] out Type? keyType,
            [NotNullWhen(true)] out Type? valueType)
        {
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();

                if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                {
                    Type[] arguments = type.GetGenericArguments();
                    keyType = arguments[0];
                    valueType = arguments[1];
                    return true;
                }
            }

            keyType = null;
            valueType = null;
            return false;
        }

        internal static bool TryGetElementType(Type type, [NotNullWhen(true)] out Type? elementType)
        {
            elementType = null;

            if (type == typeof(string))
            {
                return false;
            }

            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType is not null;
            }

            if (IsEnumerableOf(type, out elementType))
            {
                return true;
            }

            foreach (Type contract in type.GetInterfaces())
            {
                if (IsEnumerableOf(contract, out elementType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEnumerableOf(Type type, [NotNullWhen(true)] out Type? elementType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }
    }
}
