using System;
using System.Collections.Generic;
using Mapperion.Internal;

namespace Mapperion.Model
{
    /// <summary>
    /// The complete, frozen configuration: global options plus every type map. This is what the
    /// compiler turns into executable plans, and what diagnostics report on.
    /// </summary>
    public sealed class MapperModel
    {
        private readonly Dictionary<TypeMapKey, TypeMapDefinition> typeMaps;
        private readonly TypeMapDefinition[] ordered;

        /// <summary>Creates a model from global options and a set of type maps.</summary>
        /// <param name="options">The global options.</param>
        /// <param name="typeMaps">The type maps. Order is preserved for diagnostics.</param>
        /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Two definitions describe the same type pair.</exception>
        public MapperModel(MapperOptions options, IEnumerable<TypeMapDefinition> typeMaps)
        {
            Guard.NotNull(typeMaps, nameof(typeMaps));
            Options = Guard.NotNull(options, nameof(options));

            var list = new List<TypeMapDefinition>(typeMaps);
            this.typeMaps = new Dictionary<TypeMapKey, TypeMapDefinition>(list.Count);

            foreach (TypeMapDefinition definition in list)
            {
                if (definition is null)
                {
                    throw new ArgumentException("A type map definition cannot be null.", nameof(typeMaps));
                }

                if (this.typeMaps.ContainsKey(definition.Key))
                {
                    throw new ArgumentException("Duplicate type map for " + definition.Key + ".", nameof(typeMaps));
                }

                this.typeMaps.Add(definition.Key, definition);
            }

            ordered = list.ToArray();
        }

        /// <summary>Gets the global options.</summary>
        public MapperOptions Options { get; }

        /// <summary>Gets every type map, in declaration order.</summary>
        public IReadOnlyList<TypeMapDefinition> TypeMaps => ordered;

        /// <summary>Gets the number of configured type maps.</summary>
        public int Count => ordered.Length;

        /// <summary>Looks up the map for a type pair.</summary>
        /// <param name="key">The type pair.</param>
        /// <param name="definition">The definition when one exists.</param>
        /// <returns><see langword="true"/> when a map is configured for the pair.</returns>
        public bool TryGetTypeMap(TypeMapKey key, out TypeMapDefinition? definition)
        {
            return typeMaps.TryGetValue(key, out definition);
        }

        /// <summary>Looks up the map for a type pair.</summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <param name="definition">The definition when one exists.</param>
        /// <returns><see langword="true"/> when a map is configured for the pair.</returns>
        public bool TryGetTypeMap(Type sourceType, Type destinationType, out TypeMapDefinition? definition)
        {
            return TryGetTypeMap(new TypeMapKey(sourceType, destinationType), out definition);
        }

        /// <summary>Determines whether a map is configured for a type pair.</summary>
        /// <param name="key">The type pair.</param>
        /// <returns><see langword="true"/> when a map exists.</returns>
        public bool Contains(TypeMapKey key) => typeMaps.ContainsKey(key);
    }
}
