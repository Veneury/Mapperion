using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Invoicing
{
    /// <summary>
    /// One invoicing layer, configured twice: once on AutoMapper 14 and once on Mapperion, after
    /// migrating it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The readme claims a migration is mostly a change of namespace. This is that claim written
    /// as something a machine can fail. The domain and the contracts are shared, both mappers are
    /// handed the same invoices with the same per-operation state, and the two results are
    /// compared field by field. If Mapperion ever stops agreeing with AutoMapper about what these
    /// configurations mean, this stops passing.
    /// </para>
    /// <para>
    /// Comparing the serialised form rather than writing an equality member is deliberate: it
    /// covers every field, including ones added later that nobody remembers to compare.
    /// </para>
    /// </remarks>
    public static class Program
    {
        private static readonly JsonSerializerOptions Readable = new JsonSerializerOptions { WriteIndented = true };

        public static int Main()
        {
            AutoMapper.IMapper before = Before.Setup.Build();
            Mapperion.IMapper after = After.Setup.Build();

            var differences = new List<string>();

            foreach ((string name, Invoice invoice) in new[]
            {
                ("an ordinary invoice", Sample.One()),
                ("a cancelled one, with no notes and no discount", Sample.Two()),
            })
            {
                string mapped = Json(Before.Setup.Map(before, invoice, "ada"));
                string migrated = Json(After.Setup.Map(after, invoice, "ada"));

                Console.WriteLine(name);
                Console.WriteLine(new string('-', name.Length));
                Console.WriteLine(Indent(migrated));
                Console.WriteLine();

                if (mapped != migrated)
                {
                    differences.Add(name + ":" + Environment.NewLine +
                        "  AutoMapper:" + Environment.NewLine + Indent(mapped, "    ") + Environment.NewLine +
                        "  Mapperion:" + Environment.NewLine + Indent(migrated, "    "));
                }
            }

            if (differences.Count == 0)
            {
                Console.WriteLine("Both configurations agree on every field of every invoice.");
                return 0;
            }

            Console.Error.WriteLine(differences.Count + " invoice(s) came out differently:");

            foreach (string difference in differences)
            {
                Console.Error.WriteLine(difference);
            }

            return 1;
        }

        private static string Json(InvoiceDto dto) => JsonSerializer.Serialize(dto, Readable);

        private static string Indent(string text, string prefix = "  ") =>
            prefix + text.Replace("\n", "\n" + prefix, StringComparison.Ordinal);
    }
}
