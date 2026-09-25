using Microsoft.CodeAnalysis;

namespace Mapperion.Analysis
{
    /// <summary>
    /// What the analyser can tell someone about their configuration before they run it.
    /// </summary>
    /// <remarks>
    /// All warnings, none errors. Two of these describe something that throws when the
    /// configuration is built, so an error would be defensible, but the same two can be written
    /// deliberately across branches of an <c>if</c> where only one of them runs. A warning is seen,
    /// and a project that wants it to stop the build already turns warnings into errors.
    /// </remarks>
    internal static class ConfigurationDiagnostics
    {
        private const string Category = "Mapperion";

        internal static readonly DiagnosticDescriptor DuplicateMap = new DiagnosticDescriptor(
            "MPR1001",
            "The pair is already declared",
            "The map '{0}' to '{1}' is declared more than once here; building the configuration will throw",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A pair may be declared once per configuration. The second CreateMap for it " +
                "raises MapperConfigurationException when the configuration is built.");

        internal static readonly DiagnosticDescriptor SourcedTwice = new DiagnosticDescriptor(
            "MPR1002",
            "The member is given a source more than once",
            "'{0}' is given a source more than once on this map; only the last MapFrom has any effect",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MapFrom replaces whatever source the member had, so an earlier one is " +
                "simply gone while still reading as though it applies. Note that this is about the " +
                "source alone: settings accumulate, and splitting a member's configuration across two " +
                "ForMember calls is fine as long as they do not both name where the value comes from.");

        internal static readonly DiagnosticDescriptor IgnoredAndSourced = new DiagnosticDescriptor(
            "MPR1003",
            "The member is both ignored and given a source",
            "'{0}' is both ignored and given a source; whichever came last is the one that holds",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Ignore drops the source and MapFrom clears the ignore, so one of the two is " +
                "doing nothing and which one depends on the order they are written in. That is rarely " +
                "what someone means to rely on.");

        internal static readonly DiagnosticDescriptor ConstructorConfiguredTwoWays = new DiagnosticDescriptor(
            "MPR1004",
            "The destination is built two ways",
            "This map has both ConstructUsing and ForCtorParam; building the configuration will reject it",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ConstructUsing hands the whole construction to a factory, so there are no " +
                "constructor arguments left for ForCtorParam to configure. The configuration refuses " +
                "the pair rather than silently dropping one of them.");
    }
}
