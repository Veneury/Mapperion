# Getting started

```
dotnet add package Mapperion --prerelease
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

## Checking the configuration

```csharp
configuration.AssertIsValid();
```

It reports every problem at once rather than the first: destination members nothing maps to, nested
pairs with no map declared, and loops with nothing to stop them. Worth calling in a test, so a
configuration mistake fails the build rather than a request.

## With dependency injection

```
dotnet add package Mapperion.Extensions.DependencyInjection --prerelease
```

```csharp
services.AddMapperion(typeof(SomeProfile).Assembly);
```

That scans for `Profile` classes, registers `IMapper`, and resolves converters and resolvers from
the container, so a resolver can take its own dependencies.

## When a map fails

A failure names the member it happened at, including the path through nested maps and the position
in a collection:

```
Mapping Batch -> BatchDto failed at 'Readings[2].Ratio'. See the inner exception.
```

`MappingException.MemberPath` carries the same path for code that wants to read it.
