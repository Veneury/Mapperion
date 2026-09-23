using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// Entry point. Run one scenario with <c>--filter *FlatBenchmarks*</c>, or all of them with
    /// <c>--filter *</c>. Numbers meant for publication come from a dedicated machine, never from
    /// a shared runner.
    /// </summary>
    /// <remarks>
    /// The optimisation validator is off because AgileObjects.AgileMapper ships a non-optimised
    /// assembly, and BenchmarkDotNet refuses to measure one by default. That refusal is right: a
    /// library built in Debug is being timed with the JIT optimiser disabled, so its numbers say
    /// nothing about how it performs in a real application. AgileMapper is kept in the comparison
    /// because it was asked for, and its row is marked wherever the results are written down.
    /// </remarks>
    public static class Program
    {
        /// <remarks>
        /// Two modes. Without arguments, or with BenchmarkDotNet's own, it runs benchmarks. With
        /// <c>--budget</c> it reads a finished run instead and checks it against
        /// <c>benchmarks/baseline.json</c>, which is what CI does after running a subset.
        /// </remarks>
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--budget")
            {
                string baseline = args.Length > 1 ? args[1] : "benchmarks/baseline.json";
                string results = args.Length > 2 ? args[2] : "BenchmarkDotNet.Artifacts/results";

                return Budget.Check(baseline, results);
            }

            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args, DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator));

            return 0;
        }
    }
}
