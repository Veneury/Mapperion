using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mapperion
{
    /// <summary>
    /// Base type for every exception thrown by Mapperion.
    /// </summary>
    public abstract class MapperionException : Exception
    {
        /// <summary>Initializes a new instance with a default message.</summary>
        protected MapperionException()
            : base("A Mapperion error occurred.")
        {
        }

        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        protected MapperionException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        protected MapperionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when the mapper configuration is invalid. Reports every problem found, not just the
    /// first one, so a single startup run surfaces all of them.
    /// </summary>
    public sealed class MapperConfigurationException : MapperionException
    {
        private static readonly IReadOnlyList<string> NoErrors = new ReadOnlyCollection<string>(Array.Empty<string>());

        /// <summary>Initializes a new instance with a default message.</summary>
        public MapperConfigurationException()
            : base("The mapper configuration is invalid.")
        {
            Errors = NoErrors;
        }

        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public MapperConfigurationException(string message)
            : base(message)
        {
            Errors = NoErrors;
        }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        public MapperConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
            Errors = NoErrors;
        }

        /// <summary>Initializes a new instance carrying every configuration error found.</summary>
        /// <param name="message">The summary message.</param>
        /// <param name="errors">The individual configuration errors.</param>
        public MapperConfigurationException(string message, IReadOnlyList<string> errors)
            : base(message)
        {
            Errors = errors ?? NoErrors;
        }

        /// <summary>Gets every configuration error detected, in a stable order.</summary>
        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Thrown when a mapping fails at run time. Carries the full member path that failed.
    /// </summary>
    public class MappingException : MapperionException
    {
        /// <summary>Initializes a new instance with a default message.</summary>
        public MappingException()
            : base("The mapping operation failed.")
        {
        }

        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public MappingException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        public MappingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance describing the exact member that failed.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="memberPath">The destination member path, for example <c>OrderDto.Lines[3].Price</c>.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        public MappingException(string message, string memberPath, Exception innerException)
            : base(message, innerException)
        {
            MemberPath = memberPath;
        }

        /// <summary>Gets the destination member path that failed, when known.</summary>
        public string? MemberPath { get; }
    }

    /// <summary>
    /// Thrown when a map that can reach itself has recursed past the configured limit, which means
    /// the object graph loops and nothing in the configuration stops it.
    /// </summary>
    /// <remarks>
    /// Without the limit the recursion would end in a <see cref="StackOverflowException"/>, which
    /// cannot be caught and takes the process down with it. That is the shape of the denial of
    /// service a mapper is exposed to whenever it maps a graph it did not build itself. Here it is
    /// an ordinary exception, so a request that carries a looping graph can be rejected instead.
    /// </remarks>
    public class RecursionLimitException : MappingException
    {
        /// <summary>Initializes a new instance with a default message.</summary>
        public RecursionLimitException()
            : base("A map recursed past the configured limit.")
        {
        }

        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The error message.</param>
        public RecursionLimitException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        public RecursionLimitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal RecursionLimitException(string map, int limit)
            : base("Mapping " + map + " recursed more than " + limit + " levels deep. The object " +
                  "graph loops and nothing in the configuration stops it. Add MaxDepth or " +
                  "PreserveReferences to one of the maps on the loop, ignore the member that " +
                  "closes it, or raise RecursionLimit if the graph really is this deep.")
        {
            Map = map;
            Limit = limit;
        }

        /// <summary>Gets the map that hit the limit.</summary>
        public string? Map { get; }

        /// <summary>Gets the limit that was reached.</summary>
        public int Limit { get; }
    }
}
