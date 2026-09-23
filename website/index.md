---
_layout: landing
---

# Mapperion

An object-to-object mapper for .NET, **MIT licensed**, shaped like AutoMapper so that moving an
existing codebase is mostly a namespace change.

```csharp
var configuration = new MapperConfiguration(cfg =>
    cfg.CreateMap<Order, OrderDto>());

IMapper mapper = configuration.CreateMapper();
OrderDto dto = mapper.Map<Order, OrderDto>(order);
```

```
dotnet add package Mapperion --prerelease
```

## Two engines, one set of rules

The **run-time engine** compiles a plan the first time a pair is mapped and reuses it. It is the
one that behaves like AutoMapper, and it is what you get from `MapperConfiguration`.

The **source generator** writes the same mapping as plain C# at compile time. No reflection, no
code emitted while running, which is what makes it work under trimming and ahead-of-time
compilation — and it runs at roughly the speed of a mapping written by hand.

The two share no code. They share the rules, and a test suite checks they agree on the same cases.

## Where to start

- [Getting started](articles/getting-started.md) — the first map, and the shape of a configuration.
- [Migrating from AutoMapper](articles/migrating-from-automapper.md) — what is identical, what
  differs on purpose, and why.
- [Ahead-of-time and trimming](articles/aot.md) — the source generator, and what the run-time
  engine cannot do.
- [Performance](articles/performance.md) — measurements against AutoMapper, Mapster and Mapperly,
  including where Mapperion loses.
- [API reference](api/Mapperion.yml) — every public type, generated from the documentation in the source.
