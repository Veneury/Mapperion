# Mapperion

Object-to-object mapper for .NET, **MIT licensed**, built as a drop-in alternative to AutoMapper
for commercial projects.

[![Mapperion](https://img.shields.io/nuget/vpre/Mapperion?label=Mapperion)](https://www.nuget.org/packages/Mapperion)
[![Mapperion.Extensions.DependencyInjection](https://img.shields.io/nuget/vpre/Mapperion.Extensions.DependencyInjection?label=DependencyInjection)](https://www.nuget.org/packages/Mapperion.Extensions.DependencyInjection)
[![Mapperion.SourceGenerator](https://img.shields.io/nuget/vpre/Mapperion.SourceGenerator?label=SourceGenerator)](https://www.nuget.org/packages/Mapperion.SourceGenerator)
[![CI](https://github.com/Veneury/Mapperion/actions/workflows/ci.yml/badge.svg)](https://github.com/Veneury/Mapperion/actions/workflows/ci.yml)

**[Documentation](https://veneury.github.io/Mapperion/)** — getting started, migrating from
AutoMapper, ahead-of-time, performance, and the full API reference.

```
dotnet add package Mapperion --prerelease
```

> **Status: pre-release.** Everything listed below works and is covered by the test suite, which
> runs on .NET Framework 4.7.2 and 4.8 as well as .NET 8, 9 and 10. There is nothing left that
> AutoMapper does and Mapperion does not. What is still missing is the thing no amount of code
> supplies: nobody has yet migrated a real project onto it, so the API may still move before 1.0.

## The short version of the licence

MIT, and every version already published stays MIT: an MIT grant cannot be withdrawn from something
already released. There is no contributor licence agreement here and no copyright assignment, which
is what would make relicensing future versions possible for one party to decide alone.
[GOVERNANCE.md](https://github.com/Veneury/Mapperion/blob/main/GOVERNANCE.md) explains the mechanism, and is honest about where that protection is
still thin.

## Why

AutoMapper moved to a paid commercial license at v15. Earlier versions stay MIT but are frozen:
no new features, no support for new target frameworks, and a limited security horizon.

Mapperion exists to fill that gap with three commitments:

1. **MIT forever** — free commercial use, no per-seat or per-company licensing.
2. **Broad reach** — .NET Framework 4.6.2+, .NET Standard 2.0/2.1, .NET 8/9/10.
3. **Cheap migration** — the API is deliberately shaped like AutoMapper's, so moving an existing
   codebase is mostly a namespace change.

## Example

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<OrderProfile>();

    cfg.CreateMap<Order, OrderDto>()
       .ForMember(d => d.Total,      o => o.MapFrom(s => s.Lines.Sum(l => l.Price)))
       .ForMember(d => d.AuditToken, o => o.Ignore());
});

IMapper mapper = config.CreateMapper();

OrderDto dto = mapper.Map<Order, OrderDto>(order);
mapper.Map(order, existingDto);
```

Maps are compiled to delegates the first time each type pair is used, so nothing is resolved by
reflection once a map is warm.

Everything not configured by hand is resolved by convention, in the same order AutoMapper uses:

```csharp
cfg.CreateMap<Person, PersonDto>();

// PersonDto.Name               <- Name
// PersonDto.ContactEmail       <- Contact.Email
// PersonDto.CompanyContactEmail<- Company.Contact.Email
```

Validation reports everything it finds in one go, rather than stopping at the first problem:

```csharp
config.AssertIsValid();

// MapperConfigurationException: The mapper configuration is not valid. 2 problems were found:
//   1. Sale -> SaleDto: destination member 'Reference' has no source. Map it with ForMember, or ignore it.
//   2. Sale -> SaleDto: member 'Lines' needs a map from 'Line' to 'LineDto'. Declare it with CreateMap<Line, LineDto>().
```

One map covers every closing of a generic pair:

```csharp
cfg.CreateMap(typeof(Page<>), typeof(PageDto<>));
cfg.CreateMap<Order, OrderDto>();

PageDto<OrderDto> page = mapper.Map<Page<Order>, PageDto<OrderDto>>(source);
```

Records are built through their constructor, matching parameter names against source members:

```csharp
public sealed record EmployeeDto(string Name, int Age);

cfg.CreateMap<Employee, EmployeeDto>();
```

Queries are projected in the database rather than materialised:

```csharp
List<BookDto> books = await context.Books
    .Where(b => b.Pages > 200)
    .ProjectTo<BookDto>(configuration)
    .ToListAsync();
```

Anything a query provider cannot run — type converters, value converters, resolvers, `BeforeMap`
and `AfterMap` — is reported rather than skipped. AutoMapper skips them silently, which lets a
projection quietly disagree with the same map run through `Map`.

In an ASP.NET Core application:

```csharp
builder.Services.AddMapperion(typeof(Program).Assembly);
```

That registers the configuration as a singleton and `IMapper` as scoped, so a resolver may depend
on scoped services. Every mapper shares the same compiled plans, so one per request costs nothing.

For a trimmed or ahead-of-time compiled application, the same mapping can be written at compile
time instead, with no reflection anywhere:

```csharp
[Mapper]
public partial class OrderMapper
{
    [MapProperty("Customer.Address.City", "CustomerCity")]
    public partial OrderDto ToDto(Order source);

    public partial LineDto ToDto(Line source);
}
```

The generator writes the bodies and reports what it cannot write as a compiler error rather than
leaving it to fail later.

Profiles work the way you already know them:

```csharp
public sealed class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<OrderLine, OrderLineDto>();
    }
}

cfg.AddProfile<OrderProfile>();
cfg.AddProfiles(typeof(Program).Assembly);
```

## Coming from AutoMapper

| AutoMapper | Mapperion |
|------------|-----------|
| `MapperConfiguration(cfg => ...)` | same |
| `Profile`, `CreateMap<S,D>()` | same |
| `ForMember(d => d.X, o => o.MapFrom(...))` | same |
| `Ignore()`, `Condition()`, `PreCondition()`, `NullSubstitute()` | same |
| `MaxDepth()`, `PreserveReferences()` | same |
| `AddProfile<T>()`, `AddProfiles(assembly)` | same |
| `CreateMapper()`, `IMapper.Map<T>(...)` | same |
| `ForCtorParam(name, o => o.MapFrom(...))` | same |
| `ReverseMap()` | same, minus unflattening |
| `Include<,>()`, `IncludeBase<,>()` | same |
| `CreateMap(typeof(A<>), typeof(B<>))` | same |
| `ITypeConverter`, `IValueConverter`, `IValueResolver` | same, with `ResolutionContext` |
| `ConvertUsing<T>()`, `MapFrom<TResolver>()` | same |
| `BeforeMap(...)`, `AfterMap(...)`, `IMappingAction` | same |
| `AddAutoMapper(...)` | `AddMapperion(...)` |
| `ProjectTo<T>(configuration)` | same, and on `IMapper` too |
| `RecognizePrefixes` / `RecognizePostfixes` | `RecognizeSourcePrefixes` / `RecognizeDestinationPostfixes` |
| `AssertConfigurationIsValid()` | same name works, or the shorter `AssertIsValid()` |

Deliberate difference: a type pair with no `CreateMap` is an error rather than an implicit map.
Validation stays opt-in, exactly as in AutoMapper.

## Supported frameworks

Built for `netstandard2.0`, `netstandard2.1`, `net472`, `net8.0`, `net9.0` and `net10.0`.

.NET Framework gets a native `net472` target rather than the netstandard shims, so a project on
4.7.2 or 4.8 does not drag in dozens of `System.*` compatibility packages. A slice of the test
suite runs on net472 and net48 on every build, so Framework support is verified rather than
assumed.

Trimming and AOT: the runtime engine compiles expression trees, so it is annotated
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` and is not for a trimmed or
ahead-of-time application. The source generator is, and writes plain C# with no reflection in it.

`samples/Mapperion.Aot` is an application published with `PublishAot=true` that maps with the
generated code and checks its own output. It builds with the trimming and AOT analysers turned on,
and every build publishes it natively and runs it, so what this section claims is measured rather
than asserted.

## What works today

- Fluent configuration: `CreateMap`, `ForMember` with `MapFrom`, `Ignore`, `Condition`,
  `NullSubstitute`, `SetMappingOrder`, `UseDestinationValue`; plus `ValidateMemberList`,
  `MaxDepth`, `PreserveReferences`.
- Profiles, including assembly scanning.
- Conventions: exact name, case-insensitive name, configurable prefixes and suffixes, and
  flattening up to a configurable depth.
- Validation: `AssertIsValid()` reports every problem at once — unmapped destination members,
  missing nested maps (looking through nullables and collections), and, with
  `MemberListValidation.Source`, source members nobody reads.
- Failures at run time name the member that caused them, with the path through nested maps and
  collections: `Batch.Readings[0].Ratio`.
- Object graphs that loop, through `PreserveReferences` or `MaxDepth`. A cycle with neither is
  reported by `AssertIsValid()`, and bounded at run time regardless, so it fails with an exception
  the caller can catch rather than exhausting the stack.
- Inheritance: `Include` dispatches to the derived map so a base reference still produces the right
  destination, and `IncludeBase` takes the base map's configuration as a starting point.
- Open generics: one `CreateMap(typeof(Page<>), typeof(PageDto<>))` serves every closing of the
  pair, worked out on first use and kept.
- A source generator that writes mappers at compile time, for trimmed and AOT applications. Its
  output is checked against the run-time engine's on the same cases, and a sample published with
  `PublishAot=true` runs on every build.
- `ConstructUsing` to build the destination with a factory, and `ForPath` to assign a member that
  sits inside it.
- `IncludeMembers` to build one destination out of several nested source objects, taking the
  renames and converters of the included maps with it.
- `MapFast`, for a loop over a great many objects: the same map, reached without the cost of
  calling a generic method through an interface.
- Per-operation values through `IMappingOperationOptions.Items`, read back from a converter,
  resolver or step as `ResolutionContext.Items`.
- A recursion ceiling on any map that can reach itself, so a looping object graph raises
  `RecursionLimitException` instead of taking the process down with the stack.
- Mapping: flat and nested POCOs, flattened paths with null guards, nullables, numeric
  conversions, enums by name or value, `ToString`, `IConvertible`, collections into arrays,
  `List<>`, `HashSet<>` and the sequence interfaces, and dictionaries with both keys and values
  converted.
- Records and any destination built through a constructor, with `ForCtorParam` to override an
  argument and parameter defaults filling what the source does not provide.
- `ReverseMap()`, which inverts renamed members and leaves the rest to the conventions.
- Type converters, value converters and value resolvers, each created once and reused. They need
  a parameterless constructor until dependency injection support lands.
- `BeforeMap` and `AfterMap`, as a lambda or as an `IMappingAction` type. A map with a type
  converter runs neither: the converter replaces the whole map.
- `Mapperion.Extensions.DependencyInjection`, which registers the mapper and lets converters and
  resolvers take their dependencies from the container.
- `ProjectTo`, which rewrites a query so the database returns only the columns the destination
  needs. Verified against EF Core with SQLite, not just built.
- A frozen configuration model exposed through `MapperConfiguration.Model`.
- `Explain()`, which writes out what a map resolved to, member by member, and says which of those
  you configured and which a convention decided.
- An optional analyser package, `Mapperion.Analyzers`, that reads your configuration at compile
  time and reports the mistakes it can see there: a pair declared twice, a member given two
  sources, a member both ignored and sourced, and `ConstructUsing` sitting next to `ForCtorParam`.
  It is a separate package on purpose, so the mapper itself keeps its one selling point of having
  no dependencies at all:

  ```bash
  dotnet add package Mapperion.Analyzers
  ```

## Not yet

`ResolutionContext.Items`, a static entry point for .NET Framework without a container, `string`
to `Guid` and the date types, and EF6. A projection cannot build a dictionary or dispatch to a
derived map: its shape is fixed before any row is read. The source generator covers the common
shapes but not yet dictionaries, value resolvers or inheritance. `ReverseMap` does not unflatten: a member mapped from a nested path is
resolved by convention on the way back, not written into the nested object.

## Development

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK, pinned in `global.json`. The build treats warnings as errors and enforces
`.editorconfig` style, so an analyzer failure is intentional.

## License

MIT. See [LICENSE](https://github.com/Veneury/Mapperion/blob/main/LICENSE).
