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

That looks like a wall, and for a single call it is one: on the flat scenario Mapster's entire
cost above hand-written code is about 9 ns, and our interface dispatch alone is about 7.4 ns of
it. There is no room to win in, short of changing `IMapper` — the thing that makes a migration
from AutoMapper a change of namespace, and not for sale.

But none of that work depends on the object being mapped. **`MapperFor` asks for it once**, and
past the wall the picture turns over:

| | Run 1 | Run 2 |
|---|---|---|
| `mapper.MapperFor<Flat, FlatDto>()`, then called | **1.62x** | **1.20x** |
| Mapster | 2.12x | 1.51x |
| `MapFast` | 2.02x | 1.55x |

Two runs, both clear, and the margin — 4 to 5 ns — is several times the spread. It is explained by
what it removes rather than found by luck: the dispatch and the plan lookup are per pair, not per
object, so a loop was paying for them once per item for no reason.

In absolute terms the single-call gap is under ten nanoseconds per object. For a request mapping
fifty objects that is half a microsecond, against a request measured in milliseconds — which is
why `Map` remains the one to write until a profiler says otherwise.

## MapperFor, for a loop

```csharp
Func<Order, OrderDto> toDto = mapper.MapperFor<Order, OrderDto>();

foreach (Order order in orders)
{
    results.Add(toDto(order));
}
```

Everything that does not depend on the object — working out the type arguments, finding the plan —
happens once, and what comes back is an ordinary `Func`, so it drops straight into a `Select` too.

Hold it for as long as the loop, or as a field beside the mapper. **Asking for one per call costs
more than it saves**, since the work it avoids is the work it does.

## MapFast, for a single call

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

Same result as `Map`, reached without the interface dispatch, for the places where there is nowhere
to keep a function. It still looks the plan up on every call, so it is the slower of the two; where
there is a loop, `MapperFor` is the better answer.

Both recognise the mapper the library builds and fall back to the interface for any other
implementation, so a decorator still works.

## Startup

For a configuration of six maps: building it costs 8.6 µs, building and validating it 14.1 µs, and
compiling the first plan 472 µs. Plans are compiled on first use and kept.
