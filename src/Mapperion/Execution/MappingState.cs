using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mapperion.Model;

namespace Mapperion.Execution
{
    /// <summary>
    /// The state one mapping operation needs to terminate on a graph that loops: how deep each map
    /// currently is, and which destinations have already been built for which source instances.
    /// </summary>
    /// <remarks>
    /// Only created when the configuration actually asks for it. A map that uses neither
    /// <c>MaxDepth</c> nor <c>PreserveReferences</c> never allocates one, and the dictionaries
    /// inside are created on first use so a map that only needs one does not pay for the other.
    /// </remarks>
    internal sealed class MappingState
    {
        private Dictionary<TypeMapKey, int>? depths;
        private Dictionary<ReferenceKey, object>? references;

        internal int Enter(TypeMapKey key)
        {
            depths ??= new Dictionary<TypeMapKey, int>();
            depths.TryGetValue(key, out int depth);

            depth++;
            depths[key] = depth;
            return depth;
        }

        internal void Exit(TypeMapKey key)
        {
            if (depths is null)
            {
                return;
            }

            if (depths.TryGetValue(key, out int depth) && depth > 0)
            {
                depths[key] = depth - 1;
            }
        }

        internal object? Preserved(object source, Type destinationType)
        {
            if (references is null)
            {
                return null;
            }

            return references.TryGetValue(new ReferenceKey(source, destinationType), out object? destination)
                ? destination
                : null;
        }

        internal void Preserve(object source, Type destinationType, object destination)
        {
            references ??= new Dictionary<ReferenceKey, object>();
            references[new ReferenceKey(source, destinationType)] = destination;
        }

        private readonly struct ReferenceKey : IEquatable<ReferenceKey>
        {
            private readonly object source;
            private readonly Type destinationType;

            internal ReferenceKey(object source, Type destinationType)
            {
                this.source = source;
                this.destinationType = destinationType;
            }

            public bool Equals(ReferenceKey other)
            {
                return ReferenceEquals(source, other.source) && destinationType == other.destinationType;
            }

            public override bool Equals(object? obj) => obj is ReferenceKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(source) * 397) ^ destinationType.GetHashCode();
                }
            }
        }
    }
}
