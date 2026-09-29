using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mapperion.Internal;
using Mapperion.Model;

namespace Mapperion.Configuration
{
    /// <summary>
    /// Finds a member from its name, for the overloads that take one as text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A name is what you have when the member is chosen somewhere a lambda cannot reach — a
    /// configuration file, a table of overrides, a loop over names. It costs the compiler's
    /// checking, which is why the expression overloads exist and are the ones to prefer.
    /// </para>
    /// <para>
    /// A dotted name is a path, the same shape the expression overloads accept: <c>Customer.Name</c>
    /// walks two steps. The name is resolved here, while the configuration is being built, so a
    /// misspelling is a configuration error rather than a member that silently maps to nothing.
    /// </para>
    /// </remarks>
    internal static class MemberNameResolver
    {
        private const BindingFlags Lookup =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        internal static MemberPath Path(Type type, string name, string role)
        {
            Guard.NotNull(type, nameof(type));

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new MapperConfigurationException("A " + role + " member name cannot be empty.");
            }

            string[] steps = name!.Split('.');
            var descriptors = new List<MemberDescriptor>(steps.Length);
            Type current = type;

            foreach (string step in steps)
            {
                MemberDescriptor descriptor = Step(current, step.Trim(), name!, role);
                descriptors.Add(descriptor);
                current = descriptor.MemberType;
            }

            return new MemberPath(descriptors);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification =
            "Members are looked up by name here, which the trimmer cannot follow. The fluent configuration is only reachable through MapperConfiguration, which already says so.")]
        private static MemberDescriptor Step(Type type, string step, string whole, string role)
        {
            PropertyInfo? property = type.GetProperty(step, Lookup);

            if (property is not null)
            {
                return MemberDescriptor.ForProperty(property);
            }

            FieldInfo? field = type.GetField(step, Lookup);

            if (field is not null)
            {
                return MemberDescriptor.ForField(field);
            }

            MethodInfo? method = type.GetMethod(step, Lookup, null, Type.EmptyTypes, null);

            if (method is not null && method.ReturnType != typeof(void))
            {
                return MemberDescriptor.ForMethod(method);
            }

            throw new MapperConfigurationException(
                "'" + type.Name + "' has no " + role + " member called '" + step + "'" +
                (whole == step ? "." : ", named as part of '" + whole + "'."));
        }
    }
}
