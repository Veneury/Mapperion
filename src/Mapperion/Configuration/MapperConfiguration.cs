using System;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Configuration;
using Mapperion.Execution;
using System.Collections.Generic;
using Mapperion.Internal;
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
        public IMapper CreateMapper() => new Mapper(Model);

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
        /// The name AutoMapper uses for <see cref="AssertIsValid"/>, so migrated startup code keeps
        /// compiling unchanged.
        /// </summary>
        /// <exception cref="MapperConfigurationException">The configuration has problems.</exception>
        [RequiresUnreferencedCode("Validation inspects types by reflection.")]
        public void AssertConfigurationIsValid() => AssertIsValid();
    }
}
