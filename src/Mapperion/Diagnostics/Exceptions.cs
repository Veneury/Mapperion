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
    public sealed class MappingException : MapperionException
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
}
