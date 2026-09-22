using System;
using System.Collections.Generic;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// An ordered chain of members read to reach a value, such as <c>Customer.Address.City</c>.
    /// A path of length one is a direct member access.
    /// </summary>
    public sealed class MemberPath : IEquatable<MemberPath>
    {
        private readonly MemberDescriptor[] steps;

        /// <summary>Creates a path from an ordered sequence of members.</summary>
        /// <param name="steps">The members to traverse, outermost first.</param>
        /// <exception cref="ArgumentNullException"><paramref name="steps"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="steps"/> is empty or contains a null entry.</exception>
        public MemberPath(IEnumerable<MemberDescriptor> steps)
        {
            Guard.NotNull(steps, nameof(steps));

            this.steps = new List<MemberDescriptor>(steps).ToArray();

            if (this.steps.Length == 0)
            {
                throw new ArgumentException("A member path needs at least one step.", nameof(steps));
            }

            foreach (MemberDescriptor step in this.steps)
            {
                if (step is null)
                {
                    throw new ArgumentException("A member path cannot contain null steps.", nameof(steps));
                }
            }
        }

        /// <summary>Creates a path with a single step.</summary>
        /// <param name="member">The member to read.</param>
        /// <returns>The path.</returns>
        public static MemberPath Of(MemberDescriptor member) => new MemberPath(new[] { member });

        /// <summary>Gets the members to traverse, outermost first.</summary>
        public IReadOnlyList<MemberDescriptor> Steps => steps;

        /// <summary>Gets the last member in the path, the one holding the value.</summary>
        public MemberDescriptor Leaf => steps[steps.Length - 1];

        /// <summary>Gets the type of the value the path resolves to.</summary>
        public Type MemberType => Leaf.MemberType;

        /// <summary>Gets the number of steps in the path.</summary>
        public int Length => steps.Length;

        /// <summary>Gets a value indicating whether the path traverses more than one member.</summary>
        public bool IsFlattened => steps.Length > 1;

        /// <summary>Returns a new path with one more step appended.</summary>
        /// <param name="member">The member to append.</param>
        /// <returns>The extended path.</returns>
        public MemberPath Append(MemberDescriptor member)
        {
            var extended = new MemberDescriptor[steps.Length + 1];
            Array.Copy(steps, extended, steps.Length);
            extended[steps.Length] = member;
            return new MemberPath(extended);
        }

        /// <inheritdoc />
        public bool Equals(MemberPath? other)
        {
            if (other is null || other.steps.Length != steps.Length)
            {
                return false;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                if (!steps[i].Equals(other.steps[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as MemberPath);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (MemberDescriptor step in steps)
                {
                    hash = (hash * 31) ^ step.GetHashCode();
                }

                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var names = new string[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                names[i] = steps[i].Name;
            }

            return string.Join(".", names);
        }
    }
}
