# The configuration analyzer

`Mapperion.Analyzers` is a separate, optional package. It reads your mapping configuration while
the project compiles and reports the mistakes that are visible in what you wrote.

```bash
dotnet add package Mapperion.Analyzers
```

It is separate so that `Mapperion` itself keeps having no dependencies at all. Nothing else
changes: the analyzer ships no runtime code and nothing of it reaches your output.

## What it reports

Everything is a warning, never an error. Two of the four describe something that throws when the
configuration is built, so an error would be defensible, but the same two can be written across
branches of an `if` where only one of them ever runs. A project that wants these to stop the build
already turns warnings into errors.

### MPR1001 — the pair is already declared

```csharp
cfg.CreateMap<Person, PersonDto>();
cfg.CreateMap<Person, PersonDto>();   // MPR1001
```

A pair may be declared once per configuration. The second one raises
`MapperConfigurationException` when the configuration is built, so this only moves the complaint
earlier — but earlier is where it is cheap.

### MPR1002 — the member is given a source more than once

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Nickname));   // MPR1002
```

`MapFrom` replaces whatever source the member had, so the first one is simply gone while still
reading as though it applies.

This is about the source alone. Settings accumulate, so splitting a member's configuration across
two calls is a style rather than a mistake:

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
   .ForMember(d => d.Name, o => o.SetMappingOrder(5));         // nothing reported
```

### MPR1003 — the member is both ignored and given a source

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => { o.MapFrom(s => s.Name); o.Ignore(); });   // MPR1003
```

`Ignore` drops the source and `MapFrom` clears the ignore, so one of the two is doing nothing and
which one depends on the order. It reads the same written across two calls, where it is easier to
miss, and it is reported there too.

### MPR1004 — the destination is built two ways

```csharp
cfg.CreateMap<Person, PersonRecord>()
   .ConstructUsing(s => new PersonRecord(s.Name, s.Age))
   .ForCtorParam("name", o => o.MapFrom(s => s.Name));   // MPR1004
```

`ConstructUsing` hands the whole construction to a factory, so there are no constructor arguments
left for `ForCtorParam` to configure.

## What it does not do

**It does not tell you whether a destination member will find a source.** That is what
`AssertIsValid()` is for. Answering it at compile time would mean a second copy of the convention
engine living beside the first one and drifting from it, and a wrong answer from an analyzer is
worse than no answer.

**It follows fluent chains.** A configuration that holds the expression in a local and calls into
it later is not followed, and nothing is reported about it rather than something wrong:

```csharp
var map = cfg.CreateMap<Person, PersonDto>();
map.ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
map.ForMember(d => d.Name, o => o.MapFrom(s => s.Nickname));   // not reported
```

**It knows the difference between your builder and ours.** Everything is matched by symbol, so a
`ForMember` on somebody else's fluent API is left alone.

## Turning one off

Like any analyzer, in `.editorconfig`:

```ini
dotnet_diagnostic.MPR1002.severity = none
```

Or at one site, where the mistake is deliberate — a test that checks the configuration rejects it,
for instance:

```csharp
#pragma warning disable MPR1001
// ...
#pragma warning restore MPR1001
```

The library's own test suite does exactly that in three places, and runs the analyzer over itself
everywhere else.
