using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mapperion.Benchmarks
{
    /// <summary>
    /// Compares a benchmark run against the ratios recorded in <c>benchmarks/baseline.json</c> and
    /// fails when one of them has got worse by more than the allowed margin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test suite says what a mapping returns. Nothing says how long it takes, and the two come
    /// apart easily: a change can leave every result identical and halve the speed, with the whole
    /// suite still green. That happened within a hair of being committed while the inlining was
    /// being written, and nothing but a second read would have caught it.
    /// </para>
    /// <para>
    /// What is compared is the multiple over the same mapping written by hand, measured in the same
    /// run, and never the time itself. A shared runner is slow and erratic in ways that move both
    /// numbers together, so the ratio survives what the nanoseconds do not.
    /// </para>
    /// </remarks>
    internal static class Budget
    {
        private const string Baseline = "Manual";

        internal static int Check(string baselineFile, string resultsDirectory)
        {
            if (!File.Exists(baselineFile))
            {
                Console.Error.WriteLine("No baseline at " + baselineFile + ".");
                return 1;
            }

            if (!Directory.Exists(resultsDirectory))
            {
                Console.Error.WriteLine(
                    "No results at " + resultsDirectory + ". The benchmarks have to run first, " +
                    "with --exporters json.");
                return 1;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(baselineFile));
            JsonElement root = document.RootElement;

            double tolerance = root.GetProperty("tolerance").GetDouble();
            Dictionary<(string Type, string Method), double> measured = Measure(resultsDirectory);

            var failures = new List<string>();
            var missing = new List<string>();

            Console.WriteLine("{0,-26} {1,-22} {2,9} {3,9}   {4}", "Scenario", "Entrant", "allowed", "measured", string.Empty);

            foreach (JsonElement scenario in root.GetProperty("scenarios").EnumerateArray())
            {
                string type = scenario.GetProperty("type").GetString()!;
                string method = scenario.GetProperty("method").GetString()!;
                double expected = scenario.GetProperty("ratio").GetDouble();
                double margin = scenario.TryGetProperty("tolerance", out JsonElement own)
                    ? own.GetDouble()
                    : tolerance;

                double allowed = expected * (1 + margin);

                if (!measured.TryGetValue((type, method), out double actual))
                {
                    missing.Add(type + "." + method);
                    continue;
                }

                bool over = actual > allowed;

                if (over)
                {
                    failures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}: {2:0.00}x of hand-written, over the {3:0.00}x allowed (baseline {4:0.00}x plus {5:0}%).",
                        type, method, actual, allowed, expected, margin * 100));
                }

                Console.WriteLine(
                    "{0,-26} {1,-22} {2,9} {3,9}   {4}",
                    type.Replace("Benchmarks", string.Empty),
                    method,
                    allowed.ToString("0.00", CultureInfo.InvariantCulture) + "x",
                    actual.ToString("0.00", CultureInfo.InvariantCulture) + "x",
                    over ? "WORSE" : "ok");
            }

            foreach (string absent in missing)
            {
                Console.Error.WriteLine("Not measured: " + absent + ". The run did not include it.");
            }

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            if (failures.Count == 0 && missing.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Every scenario is within its budget.");
                return 0;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "If the change is meant to cost this, raise the number in benchmarks/baseline.json " +
                "in the same pull request, so the cost is agreed rather than discovered.");

            return 1;
        }

        /// <summary>
        /// Reads every report in the directory and works out each entrant's multiple over the
        /// hand-written baseline of its own scenario.
        /// </summary>
        private static Dictionary<(string, string), double> Measure(string directory)
        {
            var means = new Dictionary<(string Type, string Method), double>();

            foreach (string file in Directory.EnumerateFiles(directory, "*-report-full-compressed.json"))
            {
                using JsonDocument report = JsonDocument.Parse(File.ReadAllText(file));

                foreach (JsonElement entry in report.RootElement.GetProperty("Benchmarks").EnumerateArray())
                {
                    means[(entry.GetProperty("Type").GetString()!, entry.GetProperty("Method").GetString()!)] =
                        entry.GetProperty("Statistics").GetProperty("Mean").GetDouble();
                }
            }

            var ratios = new Dictionary<(string, string), double>();

            foreach (KeyValuePair<(string Type, string Method), double> entry in means)
            {
                if (entry.Key.Method == Baseline)
                {
                    continue;
                }

                if (means.TryGetValue((entry.Key.Type, Baseline), out double manual) && manual > 0)
                {
                    ratios[entry.Key] = entry.Value / manual;
                }
            }

            return ratios;
        }
    }
}
