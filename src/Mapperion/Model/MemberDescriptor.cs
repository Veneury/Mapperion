using System;
using System.Reflection;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// Describes a single type member taking part in a map, normalising the differences between
    /// properties, fields and parameterless methods.
    /// </summary>
    public sealed class MemberDescriptor : IEquatable<MemberDescriptor>
    {
        private MemberDescriptor(MemberInfo member, Type memberType, MemberKind kind, bool canRead, bool canWrite)
        {
            Member = member;
            MemberType = memberType;
            Kind = kind;
            CanRead = canRead;
            CanWrite = canWrite;
        }

        /// <summary>Gets the underlying reflection member.</summary>
        public MemberInfo Member { get; }

        /// <summary>Gets the member name.</summary>
        public string Name => Member.Name;

        /// <summary>Gets the type of the value the member holds or returns.</summary>
        public Type MemberType { get; }

        /// <summary>Gets the kind of member.</summary>
        public MemberKind Kind { get; }

        /// <summary>Gets the type that declares the member.</summary>
        public Type DeclaringType => Member.DeclaringType!;

        /// <summary>Gets a value indicating whether the member can be read.</summary>
        public bool CanRead { get; }

        /// <summary>Gets a value indicating whether the member can be written.</summary>
        public bool CanWrite { get; }

        /// <summary>Creates a descriptor for a property.</summary>
        /// <param name="property">The property to describe.</param>
        /// <returns>The descriptor.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The property is indexed.</exception>
        public static MemberDescriptor ForProperty(PropertyInfo property)
        {
            Guard.NotNull(property, nameof(property));

            if (property.GetIndexParameters().Length != 0)
            {
                throw new ArgumentException("Indexed properties cannot take part in a map.", nameof(property));
            }

            return new MemberDescriptor(property, property.PropertyType, MemberKind.Property, property.CanRead, property.CanWrite);
        }

        /// <summary>Creates a descriptor for a field.</summary>
        /// <param name="field">The field to describe.</param>
        /// <returns>The descriptor.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
        public static MemberDescriptor ForField(FieldInfo field)
        {
            Guard.NotNull(field, nameof(field));

            bool writable = !field.IsInitOnly && !field.IsLiteral;
            return new MemberDescriptor(field, field.FieldType, MemberKind.Field, true, writable);
        }

        /// <summary>Creates a descriptor for a parameterless, value-returning method.</summary>
        /// <param name="method">The method to describe.</param>
        /// <returns>The descriptor.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The method takes parameters or returns <see langword="void"/>.</exception>
        public static MemberDescriptor ForMethod(MethodInfo method)
        {
            Guard.NotNull(method, nameof(method));

            if (method.GetParameters().Length != 0)
            {
                throw new ArgumentException("Only parameterless methods can be used as source members.", nameof(method));
            }

            if (method.ReturnType == typeof(void))
            {
                throw new ArgumentException("A source member method must return a value.", nameof(method));
            }

            return new MemberDescriptor(method, method.ReturnType, MemberKind.Method, true, false);
        }

        /// <inheritdoc />
        public bool Equals(MemberDescriptor? other)
        {
            return other is not null && Member.Equals(other.Member);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as MemberDescriptor);

        /// <inheritdoc />
        public override int GetHashCode() => Member.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => DeclaringType.Name + "." + Name;
    }
}
