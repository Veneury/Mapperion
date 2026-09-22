#if !NET5_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill of the framework attribute for target frameworks that predate it. PolySharp covers
    /// the nullability and language polyfills but not the trimming ones.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method,
        Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        internal RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public string? Url { get; set; }
    }
}

#endif
