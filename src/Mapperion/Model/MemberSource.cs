using System;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// Where a destination member takes its value from. The hierarchy is closed: every source is
    /// one of the nested kinds listed by <see cref="MemberSourceKind"/>.
    /// </summary>
    public abstract class MemberSource
    {
        private protected MemberSource(Type valueType)
        {
            ValueType = Guard.NotNull(valueType, nameof(valueType));
        }

        /// <summary>Gets the discriminator for this source.</summary>
        public abstract MemberSourceKind Kind { get; }

        /// <summary>Gets the type of the value this source produces, before any conversion.</summary>
        public Type ValueType { get; }
    }

    /// <summary>
    /// Reads the value by walking a chain of source members.
    /// </summary>
    public sealed class MemberPathSource : MemberSource
    {
        /// <summary>Creates a source that reads the given path.</summary>
        /// <param name="path">The member path to read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
        public MemberPathSource(MemberPath path)
            : base(Guard.NotNull(path, nameof(path)).MemberType)
        {
            Path = path;
        }

        /// <inheritdoc />
        public override MemberSourceKind Kind => MemberSourceKind.MemberPath;

        /// <summary>Gets the member path to read.</summary>
        public MemberPath Path { get; }

        /// <inheritdoc />
        public override string ToString() => Path.ToString();
    }

    /// <summary>
    /// Delegates the value to a resolver type instantiated when the map runs.
    /// </summary>
    public sealed class ValueResolverSource : MemberSource
    {
        /// <summary>Creates a source backed by a resolver type.</summary>
        /// <param name="resolverType">The resolver implementation.</param>
        /// <param name="valueType">The type the resolver produces.</param>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        public ValueResolverSource(Type resolverType, Type valueType)
            : base(valueType)
        {
            ResolverType = Guard.NotNull(resolverType, nameof(resolverType));
        }

        /// <inheritdoc />
        public override MemberSourceKind Kind => MemberSourceKind.ValueResolver;

        /// <summary>Gets the resolver implementation type.</summary>
        public Type ResolverType { get; }

        /// <inheritdoc />
        public override string ToString() => ResolverType.Name;
    }

    /// <summary>
    /// Supplies a constant value.
    /// </summary>
    public sealed class ConstantSource : MemberSource
    {
        /// <summary>Creates a constant source.</summary>
        /// <param name="value">The value to assign. May be <see langword="null"/>.</param>
        /// <param name="valueType">The declared type of the value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="valueType"/> is <see langword="null"/>.</exception>
        public ConstantSource(object? value, Type valueType)
            : base(valueType)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override MemberSourceKind Kind => MemberSourceKind.Constant;

        /// <summary>Gets the constant value.</summary>
        public object? Value { get; }

        /// <inheritdoc />
        public override string ToString() => Value?.ToString() ?? "null";
    }

    /// <summary>
    /// Carries a payload only one engine understands, typically a user-supplied lambda held as
    /// <see cref="object"/> so the model stays independent of <c>System.Linq.Expressions</c>.
    /// </summary>
    public sealed class CustomSource : MemberSource
    {
        /// <summary>Creates a custom source.</summary>
        /// <param name="payload">The engine-specific payload.</param>
        /// <param name="valueType">The type the payload produces.</param>
        /// <param name="description">A human-readable description used in diagnostics.</param>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> or <paramref name="valueType"/> is <see langword="null"/>.</exception>
        public CustomSource(object payload, Type valueType, string? description = null)
            : base(valueType)
        {
            Payload = Guard.NotNull(payload, nameof(payload));
            Description = description;
        }

        /// <inheritdoc />
        public override MemberSourceKind Kind => MemberSourceKind.Custom;

        /// <summary>Gets the engine-specific payload.</summary>
        public object Payload { get; }

        /// <summary>Gets the description shown in diagnostics, when one was supplied.</summary>
        public string? Description { get; }

        /// <inheritdoc />
        public override string ToString() => Description ?? "custom";
    }
}
