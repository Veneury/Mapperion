using BenchmarkDotNet.Running;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// Entry point. Run one scenario with <c>--filter *FlatBenchmarks*</c>, or all of them with
    /// <c>--filter *</c>. Numbers meant for publication come from a dedicated machine, never from
    /// a shared runner.
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
