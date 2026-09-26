# Migrating from AutoMapper

The goal is that changing `using AutoMapper;` to `using Mapperion;` resolves most files, and that
what is left is a short list you can work through in an afternoon.

## What that is worth so far

There is a sample in the repository that configures one invoicing layer twice — once on
AutoMapper 14, once on Mapperion — over a shared domain, maps the same invoices through both, and
fails when the two disagree on any field. CI runs it.

The configuration it migrates uses profiles, value resolvers, a type converter, `ForCtorParam`
onto a positional record, `NullSubstitute`, `Condition`, `Ignore`, `AfterMap`,
`ResolutionContext.Items` filled per call, flattening three hops in, and an enum that only
crosses by name.

The entire diff between the two versions is **one `using` per file and one `!`**: Mapperion types
the items bag as `IDictionary<string, object?>` rather than `IDictionary<string, object>`, so a
cast out of it needs a null-forgiving operator under a nullable context.

Worth being straight about what that is not. It says the API takes the same calls and gives the
same answers. It does not say anything about your build, your container wiring or your thirty
profiles, and it was written by somebody who already knew both libraries. The shapes match; that
is a smaller claim than "your migration will be easy".

## The procedure

1. Make sure your tests pass on AutoMapper first. You want a known-good starting point.
2. Install `Mapperion` and `Mapperion.Extensions.DependencyInjection` alongside it. Both libraries
   can sit in a project at once — the namespaces and type names differ — so you can migrate module
   by module.
3. Replace `using AutoMapper;` with `using Mapperion;`.
4. Adjust the handful of renames in the table below.
5. Compile. What still fails is a real difference; the table says which.
6. Run `AssertIsValid()`. It usually turns up pairs AutoMapper was resolving implicitly.
7. Run your tests, then remove AutoMapper.

## What is the same

`MapperConfiguration`, `Profile`, `CreateMap`, `ForMember`, `ForPath`, `ForCtorParam`, `MapFrom`,
`Ignore`, `Condition`, `PreCondition`, `NullSubstitute`, `ConvertUsing`, `ConstructUsing`,
`ReverseMap`, `Include`, `IncludeBase`, `IncludeMembers`, `BeforeMap`, `AfterMap`, `MaxDepth`,
`PreserveReferences`, `AllowNullCollections`, `ProjectTo`, `ITypeConverter`, `IValueConverter`,
`IValueResolver`, `IMappingAction`, `ResolutionContext.Items`, `SourceMemberNamingConvention`,
`DestinationMemberNamingConvention`.

## What is renamed

| AutoMapper | Mapperion |
|---|---|
| `AssertConfigurationIsValid()` | `AssertIsValid()`, and the long name works as an alias |
| `AddAutoMapper(...)` | `AddMapperion(...)` |
| `RecognizePrefixes` / `RecognizePostfixes` | `RecognizeSourcePrefixes` / `RecognizeSourcePostfixes`, because there are destination ones too |
| `AutoMapperConfigurationException` | `MapperConfigurationException` |
| `AutoMapperMappingException` | `MappingException` |
| static `Mapper.Map(...)` from v4 | `MapperHost.Instance.Map(...)`, after `MapperHost.Initialize` |

## What differs on purpose

**A pair with no map is always an error.** AutoMapper can be configured to map types you never
declared. Mapperion will not: an undeclared pair is a `MapperConfigurationException`, at startup
if you call `AssertIsValid()` and on first use otherwise. Surprises in production are worse than
a few extra `CreateMap` lines.

**A looping object graph fails instead of taking the process down.** If two maps reference each
other and the data has a cycle, AutoMapper recurses until the stack is gone, and a
`StackOverflowException` cannot be caught — that is
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), unfixed on its MIT line.
Mapperion counts the depth of any map that can reach itself and throws `RecursionLimitException`,
which derives from `MappingException`, past `RecursionLimit`. The default is 64, the same as
`System.Text.Json`. Only maps that close an unguarded loop pay for the counter.

**`ProjectTo` reports what it cannot translate.** AutoMapper silently skips converters, resolvers
and before/after steps in a projection, so the same map gives different answers through `Map` and
through `ProjectTo`. Mapperion throws instead, and says which member and why.

**`AfterMap` cannot replace the destination.** Use `ConstructUsing`, which exists with both of
AutoMapper's overloads. Declaring `ConstructUsing` and `ForCtorParam` on the same map is rejected
when the configuration is built, rather than the factory quietly winning.

**Validation is off by default**, as in AutoMapper. `cfg.ValidateOnBuild = true` turns it on.

## Not in the box

`ProjectTo` for EF6. Everything else AutoMapper does has an equivalent.
