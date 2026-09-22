using System;

namespace Mapperion.Internal
{
    /// <summary>
    /// Argument checks written once here so the rest of the codebase stays free of the conditional
    /// compilation needed to support target frameworks without <c>ArgumentNullException.ThrowIfNull</c>.
    /// </summary>
    internal static class Guard
    {
        /// <summary>Throws when <paramref name="value"/> is null, otherwise returns it.</summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="value">The argument value.</param>
        /// <param name="paramName">The argument name reported in the exception.</param>
        /// <returns>The non-null value.</returns>
        internal static T NotNull<T>(T? value, string paramName)
            where T : class
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(value, paramName);
#else
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
#endif
            return value;
        }
    }
}
