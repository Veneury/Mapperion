using System;
using System.Threading;
using Mapperion.Internal;

namespace Mapperion
{
    /// <summary>
    /// Holds one mapper for code that has no way to receive it, such as a .NET Framework
    /// application without a container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a last resort and is documented as one. A mapper reached through a static property
    /// is a dependency the signature does not declare: it cannot be substituted in a test without
    /// touching global state, and two parts of a process cannot use different configurations. Take
    /// an <see cref="IMapper"/> as a constructor parameter wherever that is possible, and register
    /// it with <c>AddMapperion</c> when there is a container.
    /// </para>
    /// <para>
    /// It exists because AutoMapper had a static <c>Mapper.Map</c> until v4, and a codebase still
    /// carrying those call sites should be able to migrate without rewriting them all at once.
    /// Treat it as a step on the way, not a destination.
    /// </para>
    /// </remarks>
    public static class MapperHost
    {
        private static IMapper? instance;

        /// <summary>Gets a value indicating whether a mapper has been installed.</summary>
        public static bool IsInitialized => Volatile.Read(ref instance) is not null;

        /// <summary>Gets the installed mapper.</summary>
        /// <exception cref="InvalidOperationException">No mapper has been installed yet.</exception>
        public static IMapper Instance =>
            Volatile.Read(ref instance) ?? throw new InvalidOperationException(
                "No mapper has been installed. Call MapperHost.Initialize(mapper) once at startup, " +
                "or take an IMapper as a constructor parameter instead of reaching for this.");

        /// <summary>Installs the mapper every call to <see cref="Instance"/> will return.</summary>
        /// <param name="mapper">The mapper to install.</param>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// A mapper is already installed. Installing a second one halfway through a process would
        /// change what earlier call sites resolve to, so it has to be deliberate: call
        /// <see cref="Reset"/> first.
        /// </exception>
        public static void Initialize(IMapper mapper)
        {
            Guard.NotNull(mapper, nameof(mapper));

            if (Interlocked.CompareExchange(ref instance, mapper, null) is not null)
            {
                throw new InvalidOperationException(
                    "A mapper is already installed. Call MapperHost.Reset() first if replacing it " +
                    "is really what you mean, which outside a test it rarely is.");
            }
        }

        /// <summary>Removes the installed mapper, so another one can be installed.</summary>
        /// <remarks>
        /// Meant for tests, which need each case to start from a known state. Calling it while a
        /// mapping is in flight is not made safe by anything here.
        /// </remarks>
        public static void Reset()
        {
            Volatile.Write(ref instance, null);
        }
    }
}
