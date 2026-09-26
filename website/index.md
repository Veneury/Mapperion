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
dotnet add package Mapperion
```

## Why not just stay on AutoMapper 14

AutoMapper moved to a paid commercial licence at v15. Version 14 stays MIT — an MIT grant cannot
be withdrawn from something already released — but that line is frozen: no new features, no new
target frameworks, and no security fixes.

That last one is not hypothetical. AutoMapper 14 carries
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x): an object graph that loops
recurses until the stack is gone, and it will not be patched on the MIT line. Mapperion bounds
recursion by default, so the same graph raises a `RecursionLimitException` you can catch rather
than a `StackOverflowException` you cannot.

And the move is cheap in a way that is checked rather than promised. A sample in the repository
configures one invoicing layer twice, on AutoMapper 14 and on Mapperion, and fails the build if
the two disagree on any field. The whole diff between them is one `using` per file and one `!`.

## Two engines, one set of rules

The **run-time engine** compiles a plan the first time a pair is mapped and reuses it. It is the
one that behaves like AutoMapper, and it is what you get from `MapperConfiguration`.

The **source generator** writes the same mapping as plain C# at compile time. No reflection, no
code emitted while running, which is what makes it work under trimming and ahead-of-time
compilation — and it runs at roughly the speed of a mapping written by hand.

The two share no code. They share the rules, and a test suite checks they agree on the same cases.

## Nothing comes with it

`Mapperion` has no dependencies at all — only the base class library — on every one of its six
target frameworks, from .NET Framework 4.7.2 to .NET 10. `ProjectTo` is in there too: it builds an
expression tree and leaves the rest to whichever query provider you have, so there is no package
per ORM to pick between.

The source generator and the analyzer are separate packages because neither ships any code that
runs in your application, and keeping them out is what lets the sentence above stay true.

## Where to start

- [Getting started](articles/getting-started.md) — the first map, and the shape of a configuration.
- [Migrating from AutoMapper](articles/migrating-from-automapper.md) — what is identical, what
  differs on purpose, and why.
- [Ahead-of-time and trimming](articles/aot.md) — the source generator, and what the run-time
  engine cannot do.
- [Performance](articles/performance.md) — measurements against AutoMapper, Mapster and Mapperly,
  including where Mapperion loses.
- [The configuration analyzer](articles/analyzer.md) — an optional package that reads your
  configuration while it compiles and reports what is wrong with it there.
- [API reference](api/Mapperion.yml) — every public type, generated from the documentation in the source.
