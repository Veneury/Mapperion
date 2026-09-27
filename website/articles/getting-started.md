# Getting started

```
dotnet add package Mapperion
```

## One map

```csharp
using Mapperion;

var configuration = new MapperConfiguration(cfg =>
    cfg.CreateMap<Order, OrderDto>());

IMapper mapper = configuration.CreateMapper();
OrderDto dto = mapper.Map<Order, OrderDto>(order);
```

Members are matched by name, then by name ignoring case, then by flattening: a destination member
called `CustomerAddressCity` finds `Customer.Address.City` on the source, with a null check at each
step. Configured prefixes and suffixes are stripped before matching.

A pair with no `CreateMap` is an error, always. Mapperion never maps types you did not declare.

## Configuring a member

```csharp
cfg.CreateMap<Order, OrderDto>()
   .ForMember(d => d.Total, o => o.MapFrom(s => s.Lines.Sum(l => l.Price)))
   .ForMember(d => d.Internal, o => o.Ignore())
   .ForMember(d => d.Note, o => o.Condition(s => s.Note.Length > 0));
```

`ForPath` reaches inside the destination, and the objects along the way are created as needed:

```csharp
cfg.CreateMap<Delivery, DeliveryDto>()
   .ForPath(d => d.Address.Street, o => o.MapFrom(s => s.Street));
```

`IncludeMembers` builds one destination out of several nested source objects. The map is consulted
first; only what it leaves unresolved is offered to the included members, in order:

```csharp
cfg.CreateMap<Application, ApplicationDto>()
   .IncludeMembers(s => s.Applicant, s => s.Employment);
```

## When the two sides spell names differently

A row read straight out of a database or a JSON payload often spells its members `first_name`
while the destination spells them `FirstName`. Ignoring case does not help, because they differ by
a character rather than by capitalisation. Tell each side which spelling it uses and the
conventions do the rest:

```csharp
cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
cfg.CreateMap<CustomerRow, CustomerDto>();
```

Flattening crosses the two spellings, so a destination `ShipToCityName` still reaches
`ship_to.city_name`.

A spelling the library does not ship is one property. Worth knowing what the room is: member names
are CLR identifiers, so the only separator that can appear in one is the underscore, and a
convention of your own is there for its variants, such as the doubled separator some code
generators emit.

```csharp
public sealed class DoubleUnderscoreNamingConvention : INamingConvention
{
    public string? SeparatorCharacter => "__";
}
```

`ExactMatchNamingConvention` as the source convention reads names exactly as written and turns
flattening off with them: `CustomerName` then only matches a source member of that name, never
`Customer.Name`.

## Checking the configuration

```csharp
configuration.AssertIsValid();
```

It reports every problem at once rather than the first: destination members nothing maps to, nested
pairs with no map declared, and loops with nothing to stop them. Worth calling in a test, so a
configuration mistake fails the build rather than a request.

## With dependency injection

```
dotnet add package Mapperion.Extensions.DependencyInjection
```

```csharp
services.AddMapperion(typeof(SomeProfile).Assembly);
```

That scans for `Profile` classes, registers `IMapper`, and resolves converters and resolvers from
the container, so a resolver can take its own dependencies.

## When mapping is the hot path

`mapper.Map<Order, OrderDto>(order)` is a generic method reached through an interface, and the
runtime works out its type arguments on every call. On a small map that is most of what the call
costs — more, on the flat benchmark, than everything Mapster spends in total.

Two ways out, in the order worth trying them.

**`MapperFor`**, when the same pair is mapped more than once:

```csharp
Func<Order, OrderDto> toDto = mapper.MapperFor<Order, OrderDto>();

foreach (Order order in orders)
{
    results.Add(toDto(order));
}
```

None of that dispatch depends on the object being mapped, so in a loop it is the same answer found
over and over. This asks for it once, and what comes back is an ordinary `Func` that also drops
into a `Select`. Hold it for as long as the loop, or as a field beside the mapper; asking for one
per call costs more than it saves.

Measured on the flat benchmark, this is the one arrangement that comes in under Mapster.

**`MapFast`**, for a single call with nowhere to keep a function:

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

Same result, same map. It skips the interface but still looks the plan up each time, so where
there is a loop, `MapperFor` is the better answer. Both recognise the mapper the library builds
and fall back to the interface for anything else, so a decorator or a test double still works.

**The source generator**, when mapping really is the thing your program spends its time on. It
writes the mapping as ordinary C# while you build, and runs at the speed of code you would have
written by hand — a far bigger difference than `MapFast` can give back.

Neither is worth reaching for by default. The saving is nanoseconds per object, which is nothing
beside almost anything else a request does; `Map` is the one to write until a profiler says
otherwise. [Performance](performance.md) has the numbers.

## When a map fails

A failure names the member it happened at, including the path through nested maps and the position
in a collection:

```
Mapping Batch -> BatchDto failed at 'Readings[2].Ratio'. See the inner exception.
```

`MappingException.MemberPath` carries the same path for code that wants to read it.
