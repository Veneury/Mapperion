using System;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// How one constructor parameter of the destination type is supplied. Used for records,
    /// primary constructors and any destination built through a parameterised constructor.
    /// </summary>
    public sealed class ConstructorParameterDefinition
    {
        /// <summary>Creates a definition for a constructor parameter.</summary>
        /// <param name="name">The parameter name, as declared.</param>
        /// <param name="parameterType">The parameter type.</param>
        /// <param name="position">The zero-based position in the constructor signature.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="parameterType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
        public ConstructorParameterDefinition(string name, Type parameterType, int position)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "A parameter position cannot be negative.");
            }

            Name = Guard.NotNull(name, nameof(name));
            ParameterType = Guard.NotNull(parameterType, nameof(parameterType));
            Position = position;
        }

        /// <summary>Gets the parameter name.</summary>
        public string Name { get; }

        /// <summary>Gets the parameter type.</summary>
        public Type ParameterType { get; }

        /// <summary>Gets the zero-based position in the constructor signature.</summary>
        public int Position { get; }

        /// <summary>Gets where the argument value comes from, or <see langword="null"/> when unresolved.</summary>
        public MemberSource? Source { get; init; }

        /// <summary>Gets a value indicating whether the user configured this parameter explicitly.</summary>
        public bool IsExplicit { get; init; }

        /// <summary>Gets a value indicating whether the parameter has a compiler-supplied default.</summary>
        public bool HasDefaultValue { get; init; }

        /// <summary>Gets the compiler-supplied default, used when no source resolves.</summary>
        public object? DefaultValue { get; init; }

        /// <inheritdoc />
        public override string ToString() => Name + " <- " + (Source?.ToString() ?? "(unresolved)");
    }
}
