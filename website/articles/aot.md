# Ahead-of-time and trimming

## The short version

The run-time engine builds expression trees and compiles them while the program runs. That cannot
work in an application published ahead of time, and the library says so: the types involved carry
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, so the compiler warns rather than letting
you find out at run time.

The source generator is the answer. It writes the same mapping as ordinary C# at compile time.

```
dotnet add package Mapperion.SourceGenerator
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

Flat and nested objects, collections and dictionaries, records and constructors, enums and
nullables, and values read out of text. Members are matched the way the run-time engine matches
them: by name, then ignoring case, then by spelling out a path — a destination called
`CustomerAddressCity` finds `Customer.Address.City` on its own, three members deep.

Four attributes, and the first two are the ones you will use:

| | |
|---|---|
| `[MapProperty("Customer.Address.City", "CustomerCity")]` | a source the conventions would not find |
| `[MapperIgnore("Note")]` | leave a destination member alone |
| `[MapperResolve(nameof(Total), "Total")]` | fill a member from a method on the class, given the whole source |
| `[MapperInclude(typeof(CardPayment), typeof(CardPaymentDto))]` | hand a derived source to the method that maps it, the way `Include` does |

The last two are what a value resolver and `Include` become when there is no container to take a
resolver out of and no configuration to read at run time. Everything else — before and after
steps, `ResolutionContext`, reading a configuration out of a profile — belongs to the run-time
engine, because none of it exists while the project compiles.

It reports its own problems as compiler diagnostics, `MPR0001` to `MPR0009`, so a mapper that
cannot be generated fails the build with a message rather than producing something surprising.

The affixes and the naming conventions are not read either. Those are set on a
`MapperConfiguration`, so a source that writes its members `first_name` needs `[MapProperty]`
here even though the run-time engine can be told about the spelling once and left to it.

## It is checked, not asserted

`samples/Mapperion.Aot` in the repository is an application published with `PublishAot=true` that
maps with generated code and checks its own output, exiting non-zero if a value is wrong. It builds
with the trimming and AOT analysers on, and every run of CI publishes it natively and runs it.

The two engines are also compared directly: a test suite runs the same cases through both and
requires the same answers.
