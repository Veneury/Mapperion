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
        public static void Main(string[] args) =>
            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args, DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator));
    }
}
