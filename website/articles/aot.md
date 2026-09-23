# Ahead-of-time and trimming

## The short version

The run-time engine builds expression trees and compiles them while the program runs. That cannot
work in an application published ahead of time, and the library says so: the types involved carry
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, so the compiler warns rather than letting
you find out at run time.

The source generator is the answer. It writes the same mapping as ordinary C# at compile time.

```
dotnet add package Mapperion.SourceGenerator --prerelease
```

```csharp
[Mapperion.Mapper]
public partial class OrderMapper
{
    [Mapperion.MapProperty("Customer.Address.City", "CustomerCity")]
    public partial OrderDto ToDto(Order source);

    public partial LineDto ToDto(Line source);

    public partial List<LineDto> ToDtos(IList<Line> source);
}
```

The generator fills in the bodies. What ends up in your assembly is the code you would have written
by hand: no reflection, nothing compiled while running, nothing for the trimmer to be unsure about.

## What it covers

Flat and nested objects, collections, explicit paths with `[MapProperty]`, `[MapperIgnore]`,
records and constructors, enums and nullables. It reports its own problems as compiler
diagnostics, `MPR0001` to `MPR0006`, so a mapper that cannot be generated fails the build with a
message rather than producing something surprising.

It does not do the things that only make sense at run time: value resolvers, before and after
steps, polymorphic dispatch. Those belong to the run-time engine.

## It is checked, not asserted

`samples/Mapperion.Aot` in the repository is an application published with `PublishAot=true` that
maps with generated code and checks its own output, exiting non-zero if a value is wrong. It builds
with the trimming and AOT analysers on, and every run of CI publishes it natively and runs it.

The two engines are also compared directly: a test suite runs the same cases through both and
requires the same answers.
