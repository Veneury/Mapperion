# Mapperion

Object-to-object mapper for .NET, **MIT licensed**, built as a drop-in alternative to AutoMapper
for commercial projects.

> **Status: early development.** The configuration API and the convention engine work. The mapping
> engine does not exist yet, so you cannot map anything at run time. Not published to NuGet.

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
```

Everything not configured by hand is resolved by convention, in the same order AutoMapper uses:

```csharp
cfg.CreateMap<Person, PersonDto>();

// PersonDto.Name               <- Name
// PersonDto.ContactEmail       <- Contact.Email
// PersonDto.CompanyContactEmail<- Company.Contact.Email
```

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
| `Ignore()`, `Condition()`, `NullSubstitute()` | same |
| `MaxDepth()`, `PreserveReferences()` | same |
| `AddProfile<T>()`, `AddProfiles(assembly)` | same |
| `RecognizePrefixes` / `RecognizePostfixes` | `RecognizeSourcePrefixes` / `RecognizeDestinationPostfixes` |
| `AssertConfigurationIsValid()` | `AssertIsValid()` *(not implemented yet)* |
| `AddAutoMapper(...)` | `AddMapperion(...)` *(not implemented yet)* |

Deliberate differences: a type pair with no `CreateMap` is an error rather than an implicit map,
and validation is strict by default.

## Supported frameworks

Currently built for `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`.
`net472` and `netstandard2.1` are next, so .NET Framework consumers get a native target instead of
the netstandard compatibility shims.

Trimming and AOT: the runtime engine resolves members by reflection and is annotated
`[RequiresUnreferencedCode]` accordingly. A source generator mode with full AOT support is planned.

## What works today

- Fluent configuration: `CreateMap`, `ForMember` with `MapFrom`, `Ignore`, `Condition`,
  `NullSubstitute`, `SetMappingOrder`, `UseDestinationValue`; plus `ValidateMemberList`,
  `MaxDepth`, `PreserveReferences`.
- Profiles, including assembly scanning.
- Conventions: exact name, case-insensitive name, configurable prefixes and suffixes, and
  flattening up to a configurable depth.
- A frozen configuration model exposed through `MapperConfiguration.Model`.

## Not yet

The expression compiler, `IMapper` and its `Map` methods, collections, enums, constructor mapping,
`AssertIsValid()`, `ProjectTo`, dependency injection integration and the source generator.

## Development

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK, pinned in `global.json`. The build treats warnings as errors and enforces
`.editorconfig` style, so an analyzer failure is intentional.

## License

MIT. See [LICENSE](LICENSE).
