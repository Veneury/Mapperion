using System;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Compilation;
using Mapperion.Configuration;
using Mapperion.Execution;
using System.Collections.Generic;
using System.Threading;
using Mapperion.Internal;
using Mapperion.Diagnostics;
using Mapperion.Model;
using Mapperion.Validation;

namespace Mapperion
{
    /// <summary>
    /// The entry point: runs the configuration callback once and freezes the result into a model.
    /// Building is a startup cost; the resulting instance is immutable and safe to share.
    /// </summary>
    public sealed class MapperConfiguration
    {
        /// <summary>Builds a configuration from the given callback.</summary>
        /// <param name="configure">The callback that declares maps and sets options.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <exception cref="MapperConfigurationException">The configuration is not valid.</exception>
        [RequiresUnreferencedCode(
            "Members not configured explicitly are resolved by reflection, which trimming cannot see. "
            + "Use the source generator in a trimmed or AOT application.")]
        public MapperConfiguration(Action<IMapperConfigurationExpression> configure)
        {
            Guard.NotNull(configure, nameof(configure));

            var expression = new MapperConfigurationExpression();
            configure(expression);
            Model = expression.BuildModel();

            if (Model.Options.ValidateOnBuild)
            {
                AssertIsValid();
            }
        }

        private readonly ActivatorServiceResolver activator = new ActivatorServiceResolver();
        private MapperEngine? engine;

        /// <summary>Gets the frozen configuration model the compiler turns into executable plans.</summary>
        public MapperModel Model { get; }

        /// <summary>Gets the global options in force.</summary>
        public MapperOptions Options => Model.Options;

        /// <summary>
        /// Builds the mapper. Plans are compiled the first time each type pair is mapped and kept
        /// afterwards, so the first call for a pair is slower than the rest.
        /// </summary>
        /// <returns>An immutable mapper safe to share between threads.</returns>
        [RequiresUnreferencedCode("Mapping resolves members by reflection.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        public IMapper CreateMapper() => new Mapper(Engine(), activator);

        /// <summary>
        /// Builds a mapper that takes its converters, resolvers and mapping actions from the given
        /// container, falling back to construction for the ones it does not know. Mappers built
        /// from the same configuration share their compiled plans, so one per scope is cheap.
        /// </summary>
        /// <param name="services">The container to ask.</param>
        /// <returns>An immutable mapper bound to that container.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Mapping resolves members by reflection.")]
        [RequiresDynamicCode("Mapping compiles plans at run time.")]
        public IMapper CreateMapper(IServiceProvider services)
        {
            Guard.NotNull(services, nameof(services));
            return new Mapper(Engine(), new ServiceProviderResolver(services, activator));
        }

        [RequiresUnreferencedCode("Compiling a map inspects types by reflection.")]
        [RequiresDynamicCode("Compiling a map emits code at run time.")]
        private MapperEngine Engine()
        {
            MapperEngine? existing = Volatile.Read(ref engine);

            if (existing is not null)
            {
                return existing;
            }

            var created = new MapperEngine(Model);
            return Interlocked.CompareExchange(ref engine, created, null) ?? created;
        }

        /// <summary>
        /// Throws when the configuration has problems, listing every one of them rather than
        /// stopping at the first.
        /// </summary>
        /// <exception cref="MapperConfigurationException">The configuration has problems.</exception>
        [RequiresUnreferencedCode("Validation inspects types by reflection.")]
        public void AssertIsValid()
        {
            IReadOnlyList<string> errors = ConfigurationValidator.Validate(Model);

            if (errors.Count != 0)
            {
                throw new MapperConfigurationException(ConfigurationValidator.BuildMessage(errors), errors);
            }
        }

        /// <summary>
        /// Describes, member by member, what a map resolved to and where each value comes from.
        /// </summary>
        /// <remarks>
        /// For reading, not for parsing: the wording is meant to answer "why did this member get
        /// that" and will change whenever a clearer wording turns up. It separates what was
        /// configured by hand from what a convention decided, which is most of what makes a
        /// surprising result surprising. Nothing here runs a mapping; it reads the built model.
        /// </remarks>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <returns>The description, including the case where no map is declared for the pair.</returns>
        [RequiresUnreferencedCode("Explaining a map inspects types by reflection.")]
        public string Explain<TSource, TDestination>() =>
            Explain(typeof(TSource), typeof(TDestination));

        /// <summary>Describes a map without generics.</summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The description, including the case where no map is declared for the pair.</returns>
        /// <exception cref="ArgumentNullException">Either type is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Explaining a map inspects types by reflection.")]
        public string Explain(Type sourceType, Type destinationType) =>
            Explanation.Write(Model, new TypeMapKey(sourceType, destinationType));

        /// <summary>Describes every declared map, in the order they were declared.</summary>
        /// <returns>The descriptions, separated by a blank line.</returns>
        [RequiresUnreferencedCode("Explaining a map inspects types by reflection.")]
        public string Explain()
        {
            var text = new System.Text.StringBuilder();

            foreach (TypeMapDefinition definition in Model.TypeMaps)
            {
                if (text.Length != 0)
                {
                    text.Append(Environment.NewLine);
                }

                text.Append(Explanation.Write(Model, definition.Key));
            }

            return text.ToString();
        }

        /// <summary>
        /// The name AutoMapper uses for <see cref="AssertIsValid"/>, so migrated startup code keeps
        /// compiling unchanged.
        /// </summary>
        /// <exception cref="MapperConfigurationException">The configuration has problems.</exception>
        [RequiresUnreferencedCode("Validation inspects types by reflection.")]
        public void AssertConfigurationIsValid() => AssertIsValid();
    }
}
