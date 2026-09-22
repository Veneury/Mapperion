using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Model;

namespace Mapperion.Conventions
{
    /// <summary>
    /// The members of a type that conventions are allowed to read from or write to, cached per
    /// type because the same type is inspected once per map that touches it.
    /// </summary>
    [RequiresUnreferencedCode(
        "Reading the members of a type by reflection is not compatible with trimming. "
        + "Configure members explicitly, or use the source generator, in a trimmed application.")]
    internal sealed class TypeMembers
    {
        private const BindingFlags Visible = BindingFlags.Public | BindingFlags.Instance;

        private static readonly MemberDescriptor[] None = Array.Empty<MemberDescriptor>();

        private readonly MapperOptions options;
        private readonly Dictionary<Type, MemberDescriptor[]> readable = new Dictionary<Type, MemberDescriptor[]>();
        private readonly Dictionary<Type, MemberDescriptor[]> writable = new Dictionary<Type, MemberDescriptor[]>();

        internal TypeMembers(MapperOptions options)
        {
            this.options = options;
        }

        internal MemberDescriptor[] Readable(Type type)
        {
            if (readable.TryGetValue(type, out MemberDescriptor[]? cached))
            {
                return cached!;
            }

            MemberDescriptor[] found = CollectReadable(type);
            readable.Add(type, found);
            return found;
        }

        internal MemberDescriptor[] Writable(Type type)
        {
            if (writable.TryGetValue(type, out MemberDescriptor[]? cached))
            {
                return cached!;
            }

            MemberDescriptor[] found = CollectWritable(type);
            writable.Add(type, found);
            return found;
        }

        private MemberDescriptor[] CollectReadable(Type type)
        {
            if (type.IsPrimitive || type == typeof(string))
            {
                return CollectProperties(type, readableOnly: true);
            }

            var found = new List<MemberDescriptor>();

            foreach (PropertyInfo property in type.GetProperties(Visible))
            {
                if (property.GetIndexParameters().Length == 0 && IsPublicGetter(property))
                {
                    found.Add(MemberDescriptor.ForProperty(property));
                }
            }

            if (options.IncludeFields)
            {
                foreach (FieldInfo field in type.GetFields(Visible))
                {
                    found.Add(MemberDescriptor.ForField(field));
                }
            }

            if (options.IncludeSourceMethods)
            {
                foreach (MethodInfo method in type.GetMethods(Visible))
                {
                    if (IsUsableSourceMethod(method))
                    {
                        found.Add(MemberDescriptor.ForMethod(method));
                    }
                }
            }

            return Sort(found);
        }

        private MemberDescriptor[] CollectWritable(Type type)
        {
            var found = new List<MemberDescriptor>();

            foreach (PropertyInfo property in type.GetProperties(Visible))
            {
                if (property.GetIndexParameters().Length == 0 && IsPublicSetter(property))
                {
                    found.Add(MemberDescriptor.ForProperty(property));
                }
            }

            if (options.IncludeFields)
            {
                foreach (FieldInfo field in type.GetFields(Visible))
                {
                    if (!field.IsInitOnly && !field.IsLiteral)
                    {
                        found.Add(MemberDescriptor.ForField(field));
                    }
                }
            }

            return Sort(found);
        }

        private static MemberDescriptor[] CollectProperties(Type type, bool readableOnly)
        {
            var found = new List<MemberDescriptor>();

            foreach (PropertyInfo property in type.GetProperties(Visible))
            {
                if (property.GetIndexParameters().Length == 0 && (!readableOnly || IsPublicGetter(property)))
                {
                    found.Add(MemberDescriptor.ForProperty(property));
                }
            }

            return Sort(found);
        }

        private static bool IsPublicGetter(PropertyInfo property)
        {
            MethodInfo? getter = property.GetGetMethod(nonPublic: false);
            return getter is not null && getter.IsPublic;
        }

        private static bool IsPublicSetter(PropertyInfo property)
        {
            MethodInfo? setter = property.GetSetMethod(nonPublic: false);
            return setter is not null && setter.IsPublic;
        }

        private static bool IsUsableSourceMethod(MethodInfo method)
        {
            return !method.IsSpecialName
                && !method.IsGenericMethodDefinition
                && method.DeclaringType != typeof(object)
                && method.GetParameters().Length == 0
                && method.ReturnType != typeof(void);
        }

        private static MemberDescriptor[] Sort(List<MemberDescriptor> found)
        {
            if (found.Count == 0)
            {
                return None;
            }

            found.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            return found.ToArray();
        }
    }
}
