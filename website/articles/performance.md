# Performance

## How to read this

Every number is a multiple of the same mapping written by hand, measured in the same run with
BenchmarkDotNet. Absolute times move between machines; the multiple is what travels.

These were taken on a laptop with other things running, not a dedicated machine, so treat gaps of a
few per cent as noise. They are indicative, and they are honest about where Mapperion loses.

## The scenarios

| Scenario | Manual | Mapperion | With `MapFast` | Source generator | Mapperly | Mapster | AutoMapper 14 |
|---|---|---|---|---|---|---|---|
| Flat, 10 primitives | 1.00x | 2.75x | **1.83x** | **0.93x** | 0.97x | 1.82x | 4.75x |
| Nested with a collection | 1.00x | 2.11x | — | — | — | 1.53x | 2.95x |
| Collection of 1,000 | 1.00x | 1.80x | — | 1.18x | 0.94x | 1.24x | 1.50x |
| Flattening, 4 hops | 1.00x | 3.40x | — | — | — | 2.11x | 5.54x |
| Record via constructor | 1.00x | 3.46x | — | 1.04x | 1.07x | 2.13x | 5.00x |
| Existing destination | 1.00x | 3.27x | — | — | — | 2.35x | 6.50x |

Allocations match hand-written code everywhere except the nested case, which is 1.12x.

## What this says

**The source generator is as fast as writing it yourself**, between 0.93x and 1.18x. If mapping is
genuinely on your hot path, that is the answer, and it is a bigger difference than any tuning of
the run-time engine could produce.

**The run-time engine beats AutoMapper everywhere**, usually by around half.

**Mapster is faster than the run-time engine** on every scenario, by roughly 1.4x to 1.6x. Part of
that is its API: an extension method on a static generic type does not pay what a generic method
called through an interface pays, which measured at 7.7 ns of a 28 ns call. `MapFast` is an
extension method that takes the same shortcut without changing `IMapper`, and on the flat scenario
it ties with Mapster exactly.

In absolute terms the remaining gap is around ten nanoseconds per object. For a request that maps
fifty objects that is half a microsecond, against a request measured in milliseconds.

## MapFast

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

Same result as `Map`, reached without the cost of calling a generic method through an interface. It
recognises the mapper the library builds and calls it directly, falling back to the interface for
any other implementation, so a decorator still works.

Worth it in a loop over a great many objects. Not worth the noise anywhere else.

## Startup

For a configuration of six maps: building it costs 8.6 µs, building and validating it 14.1 µs, and
compiling the first plan 472 µs. Plans are compiled on first use and kept.
