# The bookshop

A small application that uses Mapperion the way an application does. It is the pilot the roadmap
asked for: something written by deciding what the bookshop needs and then finding out what the
library makes of it, rather than by reaching for a feature on purpose.

```bash
dotnet run --project samples/Mapperion.Bookshop
```

It seeds a SQLite database in memory, serves two views of the same orders, prints them, and checks
its own output. It exits non-zero when a check does not hold, so CI runs it as a test.

## What it joins up

Nothing here is unusual on its own. What the suites do not cover is all of it at once, over one
configuration:

- Two profiles, discovered by `AddMapperion(typeof(Program))` and resolved from a container.
- `AssertIsValid()` at startup, which is where an application wants to hear about a missing map.
- A **list view** through `ProjectTo`: the database returns six columns and no entity is built.
- A **detail view** mapped in memory from an aggregate loaded with `Include`.
- Flattening two and three hops in — `CustomerFullName`, `CustomerAddressCity` — through an owned
  type.
- A collection of lines onto a `record`, built through its constructor.
- An enum that exists twice with the numbers in a different order, so it can only cross by name.
- A payment hierarchy whose DTO base is **abstract**, because nothing is ever just a payment.
- The configuration analyzer, referenced so it gets a say on a configuration nobody wrote to
  please it. It has nothing to say, which is the result.

## What it found

Two things, which is the whole reason it exists.

**The projection carried enums across by number while the mapper carried them across by name.**
The same order was `Shipped` in the detail view and `Placed` in the list. Both came from one
`CreateMap` and one policy. The projection now emits the correspondence as a chain of conditions
that the provider turns into a `CASE`, settled from the same table the mapping engine uses; EF Core
and EF6 both translate it, and there are tests in both suites saying so.

**A destination member called `City` is not found from `Address.City`.** Flattening wants the
prefix, so `AddressCity` would have resolved on its own. That one is the application's fault rather
than the library's — AutoMapper does the same — and it is left configured in
[BookshopProfiles.cs](BookshopProfiles.cs) with the reason written next to it, because the next
person to shape a contract will meet it too. `AssertIsValid()` named the member and said what to
do, which is what it is for.
