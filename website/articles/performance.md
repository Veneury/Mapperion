# Performance

## How to read this

Every number is a multiple of the same mapping written by hand, measured in the same run with
BenchmarkDotNet. Absolute times move between machines; the multiple is what travels.

These were taken on a laptop, not a dedicated machine, so treat gaps of a few per cent as noise.
They are indicative, and they are honest about where Mapperion loses.

Every row below comes from one run, all entrants together. That matters more than it sounds: two
runs on this machine have put the same unchanged library twenty per cent apart, so a table
assembled from several of them can show a winner that the measurement never found.

## The scenarios

| Scenario | Manual | Mapperion | With `MapFast` | Source generator | Mapperly | Mapster | AutoMapper 14 |
|---|---|---|---|---|---|---|---|
| Flat, 10 primitives | 11.3 ns | 2.60x | 1.95x | **1.06x** | 1.02x | 1.80x | 4.93x |
| Nested with a collection | 50.2 ns | 1.74x | — | — | — | **1.43x** | 2.90x |
| Collection of 1,000 | 6.21 µs | 1.75x | — | 1.30x | **1.14x** | 1.38x | 1.74x |
| Flattening, 4 hops | 9.47 ns | 3.09x | — | — | — | **1.83x** | 5.38x |
| Record via constructor | 6.86 ns | 2.91x | — | **1.01x** | 1.06x | 2.09x | 7.23x |
| Existing destination | 7.42 ns | 2.81x | — | — | — | **2.01x** | 6.15x |

Allocations match hand-written code in every row, for Mapperion and for Mapster. AutoMapper
allocates more in two of them.

## What this says

**The source generator is as fast as writing it yourself**, between 1.01x and 1.30x, and it is
ahead of Mapster in all three rows it appears in. If mapping is genuinely on your hot path, that
is the answer, and it is a bigger difference than any tuning of the run-time engine could produce.

**The run-time engine beats AutoMapper everywhere**, by roughly half to two thirds.

**Mapster is faster than the run-time engine on every scenario**, by 1.2x to 1.7x. Most of that is
not the mapping, it is how the call arrives. `mapper.Map<A, B>(x)` is a generic method reached
through an interface, and the runtime settles its type arguments on each call; splitting a call
apart put that at about 7.4 ns of a 31 ns call. Mapster's `source.Adapt<T>()` is an extension
method on a static type and never pays it.

That is a wall rather than a tuning problem, and the arithmetic says so plainly: on the flat
scenario Mapster's entire cost above hand-written code is about 9 ns, and our interface dispatch
alone is about 7.4 ns of it. There is no room left to win in, short of changing `IMapper` — which
is the thing that makes a migration from AutoMapper a change of namespace, and is not for sale.

`MapFast` (below) removes that dispatch and closes most of the gap without touching `IMapper`. It
does not reliably clear Mapster: it measured 1.95x here against Mapster's 1.80x, and 1.43x against
1.51x in another run of the same code on the same machine. The honest reading of two runs that
disagree about who won is that they are close, not that we are ahead.

In absolute terms the gap is under ten nanoseconds per object. For a request mapping fifty objects
that is half a microsecond, against a request measured in milliseconds.

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
