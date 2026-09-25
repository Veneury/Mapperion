using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// Finds the source path that feeds a destination member, following the same order AutoMapper
    /// uses so that migrated configurations resolve to the same members: exact name, name ignoring
    /// case, name with the configured affixes stripped, then flattening.
    /// </summary>
    /// <remarks>
    /// Each side is read through its naming convention before anything is compared, which is what
    /// lets a source written <c>first_name</c> meet a destination written <c>FirstName</c>. The
    /// convention contributes one thing: the separator between words, which is taken out of the
    /// name. A spelling with no separator leaves the name as it stands, so the default costs
    /// nothing and matches exactly what it matched before conventions were configurable.
    /// </remarks>
    [RequiresUnreferencedCode("Convention matching inspects types by reflection.")]
    internal sealed class MemberMatcher
    {
        private readonly MapperOptions options;
        private readonly TypeMembers members;
        private readonly string? sourceSeparator;
        private readonly string? destinationSeparator;
        private readonly bool flattens;

        internal MemberMatcher(MapperOptions options, TypeMembers members)
        {
            this.options = options;
            this.members = members;

            sourceSeparator = (options.SourceMemberNamingConvention ?? PascalCaseNamingConvention.Instance)
                .SeparatorCharacter;
            destinationSeparator = (options.DestinationMemberNamingConvention ?? PascalCaseNamingConvention.Instance)
                .SeparatorCharacter;

            flattens = sourceSeparator is not null;
        }

        internal MemberPath? Match(Type sourceType, string destinationMemberName)
        {
            string target = Canonical(destinationMemberName, destinationSeparator);

            MemberPath? direct = Match(sourceType, target, 1);
            if (direct is not null)
            {
                return direct;
            }

            string stripped = Strip(target, options.DestinationPrefixes, options.DestinationPostfixes);
            return string.Equals(stripped, target, StringComparison.Ordinal)
                ? null
                : Match(sourceType, stripped, 1);
        }

        private MemberPath? Match(Type type, string remaining, int depth)
        {
            MemberDescriptor[] candidates = members.Readable(type);

            foreach (MemberDescriptor candidate in candidates)
            {
                if (NameMatches(candidate, remaining))
                {
                    return MemberPath.Of(candidate);
                }
            }

            if (!flattens || depth >= options.MaxFlatteningDepth)
            {
                return null;
            }

            foreach (MemberDescriptor candidate in candidates)
            {
                if (!TryConsume(candidate, remaining, out string rest))
                {
                    continue;
                }

                MemberPath? tail = Match(candidate.MemberType, rest, depth + 1);
                if (tail is not null)
                {
                    return Prepend(candidate, tail);
                }
            }

            return null;
        }

        private bool NameMatches(MemberDescriptor candidate, string target)
        {
            string name = Canonical(candidate.Name, sourceSeparator);

            if (string.Equals(name, target, options.NameStringComparison))
            {
                return true;
            }

            string stripped = Strip(name, options.SourcePrefixes, options.SourcePostfixes);
            return !string.Equals(stripped, name, StringComparison.Ordinal)
                && string.Equals(stripped, target, options.NameStringComparison);
        }

        private bool TryConsume(MemberDescriptor candidate, string remaining, out string rest)
        {
            string name = Canonical(candidate.Name, sourceSeparator);

            if (TryConsume(name, remaining, out rest))
            {
                return true;
            }

            string stripped = Strip(name, options.SourcePrefixes, options.SourcePostfixes);
            return !string.Equals(stripped, name, StringComparison.Ordinal)
                && TryConsume(stripped, remaining, out rest);
        }

        private bool TryConsume(string name, string remaining, out string rest)
        {
            if (name.Length > 0 &&
                remaining.Length > name.Length &&
                remaining.StartsWith(name, options.NameStringComparison))
            {
                rest = remaining.Substring(name.Length);
                return true;
            }

            rest = string.Empty;
            return false;
        }

        private string Strip(string name, IReadOnlyList<string> prefixes, IReadOnlyList<string> postfixes)
        {
            string result = name;

            for (int i = 0; i < prefixes.Count; i++)
            {
                string prefix = prefixes[i];
                if (result.Length > prefix.Length && result.StartsWith(prefix, options.NameStringComparison))
                {
                    result = result.Substring(prefix.Length);
                    break;
                }
            }

            for (int i = 0; i < postfixes.Count; i++)
            {
                string postfix = postfixes[i];
                if (result.Length > postfix.Length && result.EndsWith(postfix, options.NameStringComparison))
                {
                    result = result.Substring(0, result.Length - postfix.Length);
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Takes the word separator out of a name, so both sides can be compared as one word.
        /// </summary>
        private static string Canonical(string name, string? separator)
        {
            return string.IsNullOrEmpty(separator) || name.IndexOf(separator!, StringComparison.Ordinal) < 0
                ? name
                : name.Replace(separator, string.Empty);
        }

        private static MemberPath Prepend(MemberDescriptor head, MemberPath tail)
        {
            var steps = new List<MemberDescriptor>(tail.Length + 1) { head };
            for (int i = 0; i < tail.Steps.Count; i++)
            {
                steps.Add(tail.Steps[i]);
            }

            return new MemberPath(steps);
        }
    }
}
