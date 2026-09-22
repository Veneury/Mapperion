using System;
using System.Diagnostics.CodeAnalysis;
using Mapperion.Configuration;
using Mapperion.Internal;
using Mapperion.Model;

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
        }

        /// <summary>Gets the frozen configuration model the compiler turns into executable plans.</summary>
        public MapperModel Model { get; }

        /// <summary>Gets the global options in force.</summary>
        public MapperOptions Options => Model.Options;
    }
}
