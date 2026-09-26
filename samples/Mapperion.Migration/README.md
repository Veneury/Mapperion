# One invoicing layer, configured twice

The readme says moving an existing codebase off AutoMapper is mostly a change of namespace. This
is that sentence written as something a machine can fail.

```bash
dotnet run --project samples/Mapperion.Migration
```

An invoicing layer is configured twice over one shared domain: [Before.cs](Before.cs) on
AutoMapper 14, [After.cs](After.cs) on Mapperion. Both mappers are handed the same invoices with
the same per-operation state, and the two results are compared field by field. It exits non-zero
when they disagree, and CI runs it.

Comparing the serialised form rather than writing an equality member is deliberate: it covers
every field, including ones added later that nobody remembers to compare.

## What the configuration uses

Written to reach for what an invoicing layer reaches for, not for what is easy to migrate:

`Profile` · `CreateMap` · `ForMember` · `MapFrom` with an expression · `MapFrom<TResolver>` ·
`IValueResolver` · `ITypeConverter` · `ConvertUsing` · `ForCtorParam` onto a positional record ·
`NullSubstitute` · `Condition` · `Ignore` · `AfterMap` · `ResolutionContext.Items` filled per call
· `AssertConfigurationIsValid` · flattening two and three hops in · an enum that only crosses by
name.

## What the migration cost

Run `diff Before.cs After.cs` and this is all of it:

**One `using` per file.** `using AutoMapper;` becomes `using Mapperion;`. Every type, method and
overload above kept its name and its shape, including `AssertConfigurationIsValid()`, which
Mapperion carries as an alias for `AssertIsValid()` so that a migrated call site does not have to
move.

**One `!`.** The only thing that did not compile:

```csharp
return context.Items.TryGetValue("user", out object? user) ? (string)user : "unknown";
//                                                                    ^ CS8600 on Mapperion
```

AutoMapper types the bag as `IDictionary<string, object>`; Mapperion types it as
`IDictionary<string, object?>`, which is the more truthful of the two, since nothing stops a
caller putting a null in it. Under a nullable context the cast needs a `!` it did not need before.

That is the whole diff. Two lines, one of them a namespace.

## What it does not prove

This is a migration of a configuration, not of a codebase. It says the API takes the same calls
and gives the same answers; it does not say anything about how a real project's build, its DI
wiring, or its thirty profiles behave, and it cannot, because it was written in one sitting by
someone who already knew both libraries.

The gap is worth naming rather than papering over: the evidence here is that the shapes match, and
that is a smaller claim than "your migration will be easy".

## Why AutoMapper 14 is referenced here

It is the last MIT release, and it is what somebody migrating is coming from. It carries
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), uncontrolled recursion that
will not be patched on that line, which is one of the reasons to be leaving. The reference is in a
sample that never ships and never runs in production, and the advisory is suppressed in the
project file with that written next to it.
