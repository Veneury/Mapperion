using System;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// Identifies a mapping by its source and destination types.
    /// </summary>
    /// <remarks>
    /// Part of the configuration model, which stays free of <c>System.Linq.Expressions</c> so the
    /// same shapes can be produced by the source generator.
    /// </remarks>
    public readonly struct TypeMapKey : IEquatable<TypeMapKey>
    {
        /// <summary>Initializes a new key for the given type pair.</summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <exception cref="ArgumentNullException">Either type is <see langword="null"/>.</exception>
        public TypeMapKey(Type sourceType, Type destinationType)
        {
            SourceType = Guard.NotNull(sourceType, nameof(sourceType));
            DestinationType = Guard.NotNull(destinationType, nameof(destinationType));
        }

        /// <summary>Gets the source type.</summary>
        public Type SourceType { get; }

        /// <summary>Gets the destination type.</summary>
        public Type DestinationType { get; }

        /// <inheritdoc />
        public bool Equals(TypeMapKey other)
        {
            return SourceType == other.SourceType && DestinationType == other.DestinationType;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TypeMapKey other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceType is null ? 0 : SourceType.GetHashCode();
                return (hash * 397) ^ (DestinationType is null ? 0 : DestinationType.GetHashCode());
            }
        }

        /// <summary>Determines whether two keys are equal.</summary>
        /// <param name="left">The left key.</param>
        /// <param name="right">The right key.</param>
        /// <returns><see langword="true"/> when both keys describe the same type pair.</returns>
        public static bool operator ==(TypeMapKey left, TypeMapKey right) => left.Equals(right);

        /// <summary>Determines whether two keys are different.</summary>
        /// <param name="left">The left key.</param>
        /// <param name="right">The right key.</param>
        /// <returns><see langword="true"/> when the keys describe different type pairs.</returns>
        public static bool operator !=(TypeMapKey left, TypeMapKey right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString()
        {
            return (SourceType?.Name ?? "?") + " -> " + (DestinationType?.Name ?? "?");
        }
    }
}
