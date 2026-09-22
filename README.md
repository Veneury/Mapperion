# Mapperion

Object-to-object mapper for .NET, **MIT licensed**, built as a drop-in alternative to AutoMapper
for commercial projects.

> **Status: early development.** Configuration, conventions, validation and the mapping engine
> work, including records. Dependency injection, `ProjectTo` and the source generator do not exist
> yet. Not published to NuGet.

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

Records are built through their constructor, matching parameter names against source members:

```csharp
public sealed record EmployeeDto(string Name, int Age);

cfg.CreateMap<Employee, EmployeeDto>();
```

In an ASP.NET Core application:

```csharp
builder.Services.AddMapperion(typeof(Program).Assembly);
```

That registers the configuration as a singleton and `IMapper` as scoped, so a resolver may depend
on scoped services. Every mapper shares the same compiled plans, so one per request costs nothing.

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
| `CreateMapper()`, `IMapper.Map<T>(...)` | same |
| `ForCtorParam(name, o => o.MapFrom(...))` | same |
| `ReverseMap()` | same, minus unflattening |
| `ITypeConverter`, `IValueConverter`, `IValueResolver` | same, with `ResolutionContext` |
| `ConvertUsing<T>()`, `MapFrom<TResolver>()` | same |
| `BeforeMap(...)`, `AfterMap(...)`, `IMappingAction` | same |
| `AddAutoMapper(...)` | `AddMapperion(...)` |
| `RecognizePrefixes` / `RecognizePostfixes` | `RecognizeSourcePrefixes` / `RecognizeDestinationPostfixes` |
| `AssertConfigurationIsValid()` | same name works, or the shorter `AssertIsValid()` |

Deliberate difference: a type pair with no `CreateMap` is an error rather than an implicit map.
Validation stays opt-in, exactly as in AutoMapper.

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
- Validation: `AssertIsValid()` reports every problem at once — unmapped destination members,
  missing nested maps (looking through nullables and collections), and, with
  `MemberListValidation.Source`, source members nobody reads.
- Mapping: flat and nested POCOs, flattened paths with null guards, nullables, numeric
  conversions, enums by name or value, `ToString`, `IConvertible`, and collections into arrays,
  `List<>`, `HashSet<>` and the sequence interfaces.
- Records and any destination built through a constructor, with `ForCtorParam` to override an
  argument and parameter defaults filling what the source does not provide.
- `ReverseMap()`, which inverts renamed members and leaves the rest to the conventions.
- Type converters, value converters and value resolvers, each created once and reused. They need
  a parameterless constructor until dependency injection support lands.
- `BeforeMap` and `AfterMap`, as a lambda or as an `IMappingAction` type. A map with a type
  converter runs neither: the converter replaces the whole map.
- `Mapperion.Extensions.DependencyInjection`, which registers the mapper and lets converters and
  resolvers take their dependencies from the container.
- A frozen configuration model exposed through `MapperConfiguration.Model`.

## Not yet

Dictionaries, `PreCondition`, `MaxDepth` and `PreserveReferences` at run time, `ProjectTo`,
inheritance, and the source generator. `ReverseMap` does not unflatten: a member mapped from a nested path is
resolved by convention on the way back, not written into the nested object.

## Development

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK, pinned in `global.json`. The build treats warnings as errors and enforces
`.editorconfig` style, so an analyzer failure is intentional.

## License

MIT. See [LICENSE](LICENSE).
