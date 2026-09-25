using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Mapperion.Model;

namespace Mapperion.Compilation
{
    /// <summary>
    /// Which destination member each source member becomes, worked out once from the two types.
    /// </summary>
    /// <remarks>
    /// Both the mapping engine and the projection compiler need this answer and have to give the
    /// same one: a list view and a detail view of the same record disagreeing about what a status
    /// is called is the kind of thing that reaches production and stays there. They emit it
    /// differently — one as a switch, the other as something a query provider can translate — but
    /// the correspondence itself is settled here, for both.
    /// </remarks>
    [RequiresUnreferencedCode("Reading an enum's members inspects the type by reflection.")]
    [RequiresDynamicCode("Reading an enum's members builds an array of it at run time.")]
    internal static class EnumCorrespondence
    {
        /// <summary>
        /// The pairs that can be settled at compile time, source value to destination value.
        /// </summary>
        /// <remarks>
        /// A source member left out is one that has no answer: no destination member of that name
        /// under <see cref="EnumMappingPolicy.ByName"/>. What the caller does about it depends on
        /// where it is emitting to.
        /// </remarks>
        internal static List<KeyValuePair<object, object>> Between(
            Type sourceType,
            Type destinationType,
            EnumMappingPolicy policy)
        {
            Type underlyingType = Enum.GetUnderlyingType(sourceType);
            var pairs = new List<KeyValuePair<object, object>>();
            var seen = new HashSet<object>();

            foreach (object member in Enum.GetValues(sourceType))
            {
                object underlying = Convert.ChangeType(member, underlyingType, CultureInfo.InvariantCulture);

                if (!seen.Add(underlying))
                {
                    continue;
                }

                object? counterpart = Counterpart(member, sourceType, destinationType, policy, underlying);

                if (counterpart is not null)
                {
                    pairs.Add(new KeyValuePair<object, object>(member, counterpart));
                }
            }

            return pairs;
        }

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
    }
}
