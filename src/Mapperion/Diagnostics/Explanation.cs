using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Mapperion.Model;

namespace Mapperion.Diagnostics
{
    /// <summary>
    /// Writes out what a map actually resolved to, member by member.
    /// </summary>
    /// <remarks>
    /// Conventions are convenient until a member takes a value from somewhere unexpected, and then
    /// the question is always the same: where did this come from, and why is that one empty. The
    /// answer is in the model once it is built; this puts it in a form a person can read, and
    /// separates what was configured by hand from what a convention decided, because mistaking one
    /// for the other is most of the confusion.
    /// </remarks>
    [RequiresUnreferencedCode("Explaining a map inspects types by reflection.")]
    internal static class Explanation
    {
        internal static string Write(MapperModel model, TypeMapKey key)
        {
            if (!model.TryGetTypeMap(key, out TypeMapDefinition? definition) || definition is null)
            {
                return key + ": no map is declared. Declare it with CreateMap<" +
                    key.SourceType.Name + ", " + key.DestinationType.Name + ">().";
            }

            var text = new StringBuilder();
            text.Append(definition.Key).Append('\n');

            Preamble(text, definition);

            if (definition.HasTypeConverter)
            {
                text.Append("  every member comes from ")
                    .Append(definition.TypeConverterType!.Name)
                    .Append(", which replaces member mapping entirely\n");

                return text.ToString();
            }

            Members(text, definition);
            Epilogue(text, definition);

            return text.ToString();
        }

        private static void Preamble(StringBuilder text, TypeMapDefinition definition)
        {
            if (definition.ConstructUsing is not null)
            {
                text.Append("  built by a factory given to ConstructUsing\n");
            }
            else if (definition.Constructor is not null)
            {
                text.Append("  built through a constructor of ")
                    .Append(definition.ConstructorParameters.Count)
                    .Append(" argument(s)\n");
            }

            foreach (MemberPath included in definition.IncludedMembers)
            {
                text.Append("  fills what it cannot resolve from ").Append(included).Append('\n');
            }

            foreach (TypeMapKey derived in definition.DerivedMaps)
            {
                text.Append("  hands a ").Append(derived.SourceType.Name).Append(" to ").Append(derived).Append('\n');
            }
        }

        private static void Members(StringBuilder text, TypeMapDefinition definition)
        {
            var rows = new List<(string Name, string Source, string Notes)>();

            foreach (ConstructorParameterDefinition parameter in definition.ConstructorParameters)
            {
                rows.Add((
                    "(" + parameter.Name + ")",
                    parameter.Source is null
                        ? (parameter.HasDefaultValue ? "its default value" : "nothing")
                        : Describe(parameter.Source),
                    "constructor argument"));
            }

            foreach (MemberDefinition member in definition.Members)
            {
                if (member.IsIgnored)
                {
                    rows.Add((Name(member), "(ignored)", string.Empty));
                    continue;
                }

                if (member.Source is null)
                {
                    rows.Add((Name(member), "nothing", "no source was found for it"));
                    continue;
                }

                rows.Add((Name(member), Describe(member.Source), Qualifiers(member)));
            }

            int name = 0;
            int source = 0;

            foreach ((string Name, string Source, string Notes) row in rows)
            {
                name = Math.Max(name, row.Name.Length);
                source = Math.Max(source, row.Source.Length);
            }

            foreach ((string Name, string Source, string Notes) row in rows)
            {
                text.Append("  ").Append(row.Name.PadRight(name)).Append(" <- ");

                if (row.Notes.Length == 0)
                {
                    text.Append(row.Source).Append('\n');
                    continue;
                }

                text.Append(row.Source.PadRight(source)).Append("   [").Append(row.Notes).Append("]\n");
            }
        }

        private static string Qualifiers(MemberDefinition member)
        {
            var notes = new List<string> { member.IsExplicit ? "configured" : "by convention" };

            if (member.ValueConverterType is not null)
            {
                notes.Add("through " + member.ValueConverterType.Name);
            }

            if (member.Condition is not null)
            {
                notes.Add("only when a condition holds");
            }

            if (member.PreCondition is not null)
            {
                notes.Add("only when a precondition holds");
            }

            if (member.HasNullSubstitute)
            {
                notes.Add("null becomes " + (member.NullSubstitute?.ToString() ?? "null"));
            }

            if (member.UseDestinationValue)
            {
                notes.Add("into the instance the destination already holds");
            }

            if (member.MappingOrder != 0)
            {
                notes.Add("order " + member.MappingOrder.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join(", ", notes);
        }

        private static void Epilogue(StringBuilder text, TypeMapDefinition definition)
        {
            foreach (object action in definition.BeforeMapActions)
            {
                text.Append("  before the members: ").Append(Action(action)).Append('\n');
            }

            foreach (object action in definition.AfterMapActions)
            {
                text.Append("  after the members: ").Append(Action(action)).Append('\n');
            }

            if (definition.MaxDepth is int depth)
            {
                text.Append("  stops recursing past depth ").Append(depth).Append('\n');
            }

            if (definition.PreserveReferences)
            {
                text.Append("  reuses the destination already built for a source it has seen\n");
            }
        }

        private static string Action(object action) =>
            action is Type type ? type.Name : "a delegate";

        private static string Name(MemberDefinition member) =>
            member.DestinationPath?.ToString() ?? member.DestinationMember.Name;

        private static string Describe(MemberSource source)
        {
            switch (source)
            {
                case MemberPathSource path:
                    return path.Path.ToString();

                case ValueResolverSource resolver:
                    return resolver.ResolverType.Name;

                case ConstantSource constant:
                    return "the constant " + (constant.Value?.ToString() ?? "null");

                case IncludedMemberSource included:
                    return included.Prefix + " then " + Describe(included.Inner);

                case CustomSource custom:
                    return custom.Description ?? "an expression";

                default:
                    return source.ToString() ?? "?";
            }
        }
    }
}
