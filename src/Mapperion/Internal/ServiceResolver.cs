using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Mapperion.Internal
{
    /// <summary>
    /// Where the converters, resolvers and mapping actions of a map come from. Separate from the
    /// engine so the compiled plans stay shared while the instances behind them can come from a
    /// container scope.
    /// </summary>
    internal interface IServiceResolver
    {
        [RequiresUnreferencedCode("Creating a converter or resolver by type is not compatible with trimming.")]
        object Resolve(Type type);
    }

    /// <summary>
    /// Creates each type once through its parameterless constructor and keeps it.
    /// </summary>
    internal sealed class ActivatorServiceResolver : IServiceResolver
    {
        private readonly ConcurrentDictionary<Type, object> instances = new ConcurrentDictionary<Type, object>();

        [RequiresUnreferencedCode("Creating a converter or resolver by type is not compatible with trimming.")]
        public object Resolve(Type type)
        {
            return instances.GetOrAdd(type, Create);
        }

        [RequiresUnreferencedCode("Creating a converter or resolver by type is not compatible with trimming.")]
        private static object Create(Type type)
        {
            object? instance = Activator.CreateInstance(type);

            if (instance is null)
            {
                throw new MapperConfigurationException(
                    type.Name + " could not be created. A converter, resolver or mapping action " +
                    "needs a public parameterless constructor, or must be registered in the container.");
            }

            return instance;
        }
    }

    /// <summary>
    /// Asks the container first and falls back to construction, so a resolver with dependencies
    /// works while one without stays free of registration ceremony.
    /// </summary>
    internal sealed class ServiceProviderResolver : IServiceResolver
    {
        private readonly IServiceProvider services;
        private readonly ActivatorServiceResolver fallback;

        internal ServiceProviderResolver(IServiceProvider services, ActivatorServiceResolver fallback)
        {
            this.services = services;
            this.fallback = fallback;
        }

        [RequiresUnreferencedCode("Creating a converter or resolver by type is not compatible with trimming.")]
        public object Resolve(Type type)
        {
            return services.GetService(type) ?? fallback.Resolve(type);
        }
    }
}
