using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Mapperion.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Mapperion.Analyzers.Tests
{
    /// <summary>
    /// Runs the analyser over a snippet in memory, so a test can look at what it reported and,
    /// just as importantly, at what it did not.
    /// </summary>
    internal static class AnalyzerHarness
    {
        private static readonly MetadataReference[] References = LoadReferences();

        internal static Reported Run(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "AnalyzerTest",
                new[] { CSharpSyntaxTree.ParseText(source) },
                References,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));

            Diagnostic[] failures = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (failures.Length > 0)
            {
                throw new InvalidOperationException(
                    "The snippet does not compile, so anything the analyser said about it would be " +
                    "meaningless:" + Environment.NewLine +
                    string.Join(Environment.NewLine, failures.Select(d => d.ToString())));
            }

            ImmutableArray<Diagnostic> reported = compilation
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ConfigurationAnalyzer()))
                .GetAnalyzerDiagnosticsAsync()
                .GetAwaiter()
                .GetResult();

            return new Reported(reported);
        }

        private static MetadataReference[] LoadReferences()
        {
            var assemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

            return assemblies
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToArray();
        }
    }

    internal sealed class Reported
    {
        private readonly ImmutableArray<Diagnostic> diagnostics;

        internal Reported(ImmutableArray<Diagnostic> diagnostics) => this.diagnostics = diagnostics;

        internal int Count(string id) => diagnostics.Count(d => d.Id == id);

        internal bool Has(string id) => Count(id) > 0;

        internal string All() => diagnostics.Length == 0
            ? "(nothing reported)"
            : string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
    }
}
