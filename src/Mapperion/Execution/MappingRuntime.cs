using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Mapperion.Compilation;
using Mapperion.Model;

namespace Mapperion.Execution
{
    /// <summary>
    /// The helpers a compiled plan calls into. Everything here is generic so the emitted code stays
    /// typed and nothing is boxed on the way through.
    /// </summary>
    [RequiresUnreferencedCode("Mapping resolves plans that inspect types by reflection.")]
    [RequiresDynamicCode("Mapping compiles plans at run time.")]
    internal static class MappingRuntime
    {
        internal static TDestination Fail<TDestination>(string member, string map, Exception error)
        {
            if (error is MapperConfigurationException)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            string path = Combine(member, error);

            throw new MappingException(
                "Mapping " + map + " failed at '" + path + "'. See the inner exception.",
                path,
                error);
        }

        private static string Combine(string outer, Exception error)
        {
            if (error is MappingException inner && !string.IsNullOrEmpty(inner.MemberPath))
            {
                return inner.MemberPath![0] == '['
                    ? outer + inner.MemberPath
                    : outer + "." + inner.MemberPath;
            }

            return outer;
        }

        private static MappingException AtIndex(int index, Exception error)
        {
            string path = Combine("[" + index.ToString(CultureInfo.InvariantCulture) + "]", error);

            return new MappingException(
                "Mapping the element at " + path + " failed. See the inner exception.",
                path,
                error);
        }

        internal static TDestination MapValue<TSource, TDestination>(TSource source, MappingContext context)
        {
            MapPlan plan = context.Engine.GetPlan(new TypeMapKey(typeof(TSource), typeof(TDestination)));
            return ((MapDelegate<TSource, TDestination>)plan.Typed)(source, default!, context);
        }

        internal static TDestination MapInto<TSource, TDestination>(
            TSource source,
            TDestination destination,
            MappingContext context)
        {
            MapPlan plan = context.Engine.GetPlan(new TypeMapKey(typeof(TSource), typeof(TDestination)));
            return ((MapDelegate<TSource, TDestination>)plan.Typed)(source, destination, context);
        }

        internal static TDestination ConvertType<TSource, TDestination>(
            Type converterType,
            TSource source,
            TDestination destination,
            MappingContext context)
        {
            var converter = (ITypeConverter<TSource, TDestination>)context.Services.Resolve(converterType);
            return converter.Convert(source, destination, new ResolutionContext(context));
        }

        internal static void RunAction<TSource, TDestination>(
            Type actionType,
            TSource source,
            TDestination destination,
            MappingContext context)
        {
            var action = (IMappingAction<TSource, TDestination>)context.Services.Resolve(actionType);
            action.Process(source, destination, new ResolutionContext(context));
        }

        internal static TDestinationMember ConvertValue<TSourceMember, TDestinationMember>(
            Type converterType,
            TSourceMember value,
            MappingContext context)
        {
            var converter = (IValueConverter<TSourceMember, TDestinationMember>)context.Services.Resolve(converterType);
            return converter.Convert(value, new ResolutionContext(context));
        }

        internal static TDestinationMember Resolve<TSource, TDestination, TDestinationMember>(
            Type resolverType,
            TSource source,
            TDestination destination,
            TDestinationMember current,
            MappingContext context)
        {
            var resolver = (IValueResolver<TSource, TDestination, TDestinationMember>)context.Services.Resolve(resolverType);
            return resolver.Resolve(source, destination, current, new ResolutionContext(context));
        }

        internal static List<TDestination> ToList<TSource, TDestination>(
            IEnumerable<TSource>? source,
            MappingContext context,
            Func<TSource, MappingContext, TDestination> convert,
            bool allowNull)
        {
            if (source is null)
            {
                return allowNull ? null! : new List<TDestination>();
            }

            var result = source is ICollection<TSource> known
                ? new List<TDestination>(known.Count)
                : new List<TDestination>();

            int index = 0;

            try
            {
                foreach (TSource item in source)
                {
                    result.Add(convert(item, context));
                    index++;
                }
            }
            catch (Exception error) when (!(error is MapperConfigurationException))
            {
                throw AtIndex(index, error);
            }

            return result;
        }

        internal static TDestination[] ToArray<TSource, TDestination>(
            IEnumerable<TSource>? source,
            MappingContext context,
            Func<TSource, MappingContext, TDestination> convert,
            bool allowNull)
        {
            if (source is null)
            {
                return allowNull ? null! : Array.Empty<TDestination>();
            }

            return ToList(source, context, convert, false).ToArray();
        }

        internal static Dictionary<TDestinationKey, TDestinationValue> ToDictionary<
            TSourceKey,
            TSourceValue,
            TDestinationKey,
            TDestinationValue>(
            IEnumerable<KeyValuePair<TSourceKey, TSourceValue>>? source,
            MappingContext context,
            Func<TSourceKey, MappingContext, TDestinationKey> key,
            Func<TSourceValue, MappingContext, TDestinationValue> value,
            bool allowNull)
            where TDestinationKey : notnull
        {
            if (source is null)
            {
                return allowNull ? null! : new Dictionary<TDestinationKey, TDestinationValue>();
            }

            var result = new Dictionary<TDestinationKey, TDestinationValue>();
            int index = 0;

            try
            {
                foreach (KeyValuePair<TSourceKey, TSourceValue> entry in source)
                {
                    result[key(entry.Key, context)] = value(entry.Value, context);
                    index++;
                }
            }
            catch (Exception error) when (!(error is MapperConfigurationException))
            {
                throw AtIndex(index, error);
            }

            return result;
        }

        internal static HashSet<TDestination> ToHashSet<TSource, TDestination>(
            IEnumerable<TSource>? source,
            MappingContext context,
            Func<TSource, MappingContext, TDestination> convert,
            bool allowNull)
        {
            if (source is null)
            {
                return allowNull ? null! : new HashSet<TDestination>();
            }

            var result = new HashSet<TDestination>();
            int index = 0;

            try
            {
                foreach (TSource item in source)
                {
                    result.Add(convert(item, context));
                    index++;
                }
            }
            catch (Exception error) when (!(error is MapperConfigurationException))
            {
                throw AtIndex(index, error);
            }

            return result;
        }

        internal static TDestination ToEnum<TSource, TDestination>(TSource value, EnumMappingPolicy policy)
            where TSource : struct, Enum
            where TDestination : struct, Enum
        {
            if (policy != EnumMappingPolicy.ByValue)
            {
                string name = value.ToString();

                if (LooksLikeName(name) &&
                    Enum.TryParse(name, out TDestination parsed) &&
                    string.Equals(parsed.ToString(), name, StringComparison.Ordinal))
                {
                    return parsed;
                }

                if (policy == EnumMappingPolicy.ByName)
                {
                    throw new MappingException(
                        "Enum value '" + name + "' has no member with the same name on " +
                        typeof(TDestination).Name + ".");
                }
            }

            return (TDestination)Enum.ToObject(typeof(TDestination), Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        internal static TDestination ParseEnum<TDestination>(string? value, EnumMappingPolicy policy)
            where TDestination : struct, Enum
        {
            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            if (Enum.TryParse(value, ignoreCase: true, out TDestination parsed))
            {
                return parsed;
            }

            if (policy == EnumMappingPolicy.ByName)
            {
                throw new MappingException(
                    "'" + value + "' is not a member of " + typeof(TDestination).Name + ".");
            }

            return default;
        }

        private static bool LooksLikeName(string text)
        {
            return text.Length > 0 && !char.IsDigit(text[0]) && text[0] != '-';
        }

        internal static TDestination ChangeType<TDestination>(object? value)
        {
            if (value is null)
            {
                return default!;
            }

            return (TDestination)Convert.ChangeType(value, typeof(TDestination), CultureInfo.InvariantCulture);
        }
    }
}
