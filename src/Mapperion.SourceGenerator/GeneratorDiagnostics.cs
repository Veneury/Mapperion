using Microsoft.CodeAnalysis;

namespace Mapperion.SourceGeneration
{
    /// <summary>
    /// What the generator can tell the user at compile time. A map it cannot write is reported
    /// here rather than emitted as code that would not compile.
    /// </summary>
    internal static class GeneratorDiagnostics
    {
        private const string Category = "Mapperion";

        internal static readonly DiagnosticDescriptor MapperMustBePartial = new DiagnosticDescriptor(
            "MPR0001",
            "Mapper class must be partial",
            "'{0}' is marked with [Mapper] but is not partial, so the generated methods have nowhere to go",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnmappedMember = new DiagnosticDescriptor(
            "MPR0002",
            "Destination member has no source",
            "Mapping '{0}' to '{1}': member '{2}' has no source. Map it with [MapProperty], or skip it with [MapperIgnore].",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor NoConversion = new DiagnosticDescriptor(
            "MPR0003",
            "No conversion available",
            "Mapping '{0}' to '{1}': member '{2}' cannot be converted from '{3}' to '{4}'. Add a mapping method for the pair.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor CannotConstruct = new DiagnosticDescriptor(
            "MPR0004",
            "Destination cannot be constructed",
            "Mapping '{0}' to '{1}': '{1}' has no parameterless constructor and no single constructor whose arguments could be resolved",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnsupportedSignature = new DiagnosticDescriptor(
            "MPR0005",
            "Unsupported mapping method",
            "'{0}' must take exactly one parameter and return a value for the generator to write it",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnknownMember = new DiagnosticDescriptor(
            "MPR0006",
            "Member named in an attribute was not found",
            "Mapping '{0}' to '{1}': '{2}' does not name a member of '{3}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
