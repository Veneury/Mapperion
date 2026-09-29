# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioned according to [SemVer 2.0](https://semver.org/).

Before v1.0, a minor version may introduce breaking changes.

## [0.13.0] - 2026-09-29

### Added

- **The four members a migration from AutoMapper actually writes.** `ForMember` taking the
  destination member as text, `MapFrom` taking the source member as text, `ForAllMembers`, and
  `ForSourceMember` with the one thing there is to say about a source member — `DoNotValidate()`,
  which excuses it from validation against the source list. All four were run side by side against
  AutoMapper and answer the same.
- A dotted name walks a path, the shape the expression overloads already accepted, and the name is
  resolved while the configuration is built: a misspelling is a configuration error rather than a
  member that quietly maps to nothing. The expression overloads are still the ones to prefer,
  since they keep the compiler involved.
- The rest of the surface comparison was left alone on purpose. Copying AutoMapper's overload list
  wholesale would break source compatibility here: its extra `PreCondition` overloads make
  `o.PreCondition(s => false)` ambiguous, and that call compiles today.

### Fixed

- **`ForPath` now owns the member its path starts at.** It only owned the one the path ends at,
  so the naming convention claimed the first step as well and the two disagreed. Where the source
  happened to have a member of the same name and an unrelated type, the mapper refused to build at
  all: `No conversion from X to Y is available`, for a member the configuration had already spoken
  for.
- Found by running the same configuration through AutoMapper, where it maps. Every existing test
  for `ForPath` used a path whose root had no namesake on the source, which is why four releases
  went by without it showing. There is a test for the clash now.

## [0.12.0] - 2026-09-29

### Documentation

- `RELEASING.md` said the release workflow waits for approval before pushing. It waits before
  starting: the whole job runs inside the `nuget` environment, so GitHub holds it at step zero.
  Approving means "start this release", not "these packages look right", and nothing is built
  until it happens. Worth having either way — a tag pushed by accident publishes nothing — but
  not what was written, and the difference decides whether the gate is any use to you.
- The value of `NUGET_USER` is named rather than described. It is the nuget.org account that
  created the trusted publishing policy, and where that differs from the account that owns the
  packages it is the creator that counts. Getting it wrong costs a full release run: everything
  passes and the token exchange fails with a 401 one step before the push, which is now in the
  list of things that go wrong and how to recover from them.

### Added

- The source generator matches members the way the run-time engine does, by spelling out a path:
  a destination called `CustomerAddressCity` finds `Customer.Address.City` on its own, three
  members deep. It is the convention AutoMapper is known for, and leaving it out meant the
  generator reported as unmapped what the other engine mapped without being asked.
- Dictionaries are copied into dictionaries, keys and values converted. Written as a loop and not
  as `ToDictionary`, because the two disagree about a repeated key: the engine's last entry wins
  and `ToDictionary` throws.
- `Guid`, `DateOnly` and `TimeOnly` are read out of text, and two types that both say they can
  convert themselves go through `Convert.ChangeType` with the invariant culture. The first three
  arrived in the run-time engine in 0.11.0 and never arrived here, so the same configuration
  mapped on one engine and stopped the build on the other.
- `[MapperInclude(typeof(CardPayment), typeof(CardPaymentDto))]` is `Include`, said where the
  generator can read it: mapping through a base reference now hands the work to the derived
  method instead of building the base destination and dropping what the derived type added. Most
  derived first. Declared and not inferred, for the reason it is declared in the other engine
  too — a class that happens to derive from another is not a statement that mapping one should
  produce the other.
- `[MapperResolve(nameof(Total), "Total")]` is what a value resolver becomes when there is no
  container to take one out of: a method beside the mapping ones, handed the whole source.
- Three diagnostics for the two new attributes: `MPR0007` when an included pair has no method,
  `MPR0008` when the pair named does not derive from the one being mapped, `MPR0009` when no
  method of that name takes the source.
- `MapperFor<TSource, TDestination>()`, for when the same pair is mapped many times. It settles
  everything that does not depend on the object — the generic dispatch through the interface and
  the plan lookup — once, and hands back a `Func<TSource, TDestination>` to call in the loop.
- **It is the only shape that measures below Mapster**: 1.62x against 2.12x in one run and 1.20x
  against 1.51x in another, a 4-5 ns margin against spreads of about 1. And it is explained by
  what it removes rather than found in a lucky run: the dispatch and the lookup are per pair, not
  per object, so a loop was paying for them per item for no reason.
- It returns a `Func` rather than a new type: it is literally a mapping function, it drops into a
  `Select`, and there is nothing to learn. It is an extension and not a member of `IMapper`,
  because adding to a public interface breaks everyone who implements it and `IMapper` is mocked
  constantly. The drop-in stays a drop-in.
- Per-operation state was the trap: if the configuration reads it, the context is **not** settled
  alongside the plan, because two calls would share the bag and two requests would see each
  other. It is only settled when the engine says it is not needed, and a test maps twice through
  the same function and checks that neither sees the other.
- The documentation says outright that asking for one per call costs more than it saves, which is
  the obvious way to use it wrong.

### Changed

- A plan that cannot fail no longer carries the `try/catch` that would report it, nor the
  breadcrumb written before each member for the handler to read. The question is answered when
  the plan is compiled, and answered pessimistically: "it can fail" for anything that admits user
  code — converters, resolvers, conditions, `BeforeMap`, a method as a source — for any
  conversion that can throw, and for anything that reaches another map, because an exception out
  of a nested map is reported by that map and would pass through this one unattributed.
- What is known about which conversions can throw lives in `ConversionBuilder`, beside the code
  that decides how to convert, so the two cannot drift apart: a conversion that gains a way to
  fail while this still calls it harmless would remove the reporting exactly where it was needed.
- A pair's plan is reached without the `castclass` that was paid on every call. The delegate is
  derived from those two exact types, so the check could never fail: it was always paid and never
  used. On net6+ only; `netstandard2.0` and `net472` keep the cast, because `Unsafe` there would
  be a package, and zero dependencies is worth more than a nanosecond.
- Measured: the lookup falls from 3.40 to 2.05 ns and the interface dispatch from 9.31 to 7.44.

### Fixed

- **Generated code matched enum members by number where the run-time engine matches them by
  name.** `ByNameThenValue` is the default and means the name first, so the two engines returned
  different members the first time anybody reordered an enum — quietly, with no error anywhere.
  Both enums are known while the project compiles, so the correspondence is now settled there and
  what is emitted is a switch over constants.
- **`CreateMap<Grade, GradeDto>()` on its own returned the destination's zero value** whatever it
  was handed. An enum has nothing to assign into, so the member-by-member path created it and
  stopped, while the same pair met as a member of something else went through the conversion
  builder and came out right: the engine disagreed with itself depending on where the pair was
  met. A map that configures anything of its own is untouched, so a converter declared on the
  pair still wins.
- **A collection the source did not have threw** in generated code and gave an empty collection
  in the other engine. `AllowNullCollections` cannot be seen while compiling, so the generator
  follows the default.
- **A collection of the same type on both sides was handed over rather than copied**, so source
  and destination shared one list and a change to either was a change to both.
- Text into an enum is read by name ignoring case and then as a number, with empty text giving
  the default, which is what the other engine does with it.
- Conversions are lifted over nullables where the run-time engine lifts them, before anything
  else is asked about the pair, so an absent value gives the destination's default instead of
  reaching a cast that throws on it.
- Enums by name enter the CI budget with a 40% tolerance of their own, the third scenario to
  need one and for the reason the other two did. CI reported 6.07x against 4.56x on a change
  that cannot reach it: the only run-time edit in that pull request is a branch taken while a
  plan is compiled, and only where the destination is an enum, which that scenario's is not. Six
  short runs were taken anyway — 4.14x, 4.44x and 3.45x on the branch against 3.65x, 4.09x and
  3.70x on main. The two sets overlap and each spans about a whole point, because the
  hand-written floor is five switch statements and it moved seventeen per cent between
  consecutive runs over code that had not changed.
- The record-via-constructor scenario enters the CI budget with a 40% tolerance of its own, for
  the same reason as enums by value: its hand-written floor is five nanoseconds, and three
  iterations do not settle that. The demonstration is written down in `baseline.json` — over
  untouched code, the short job reported 4.91x and a full run of the same scenario 2.99x. It was
  the guard's second false positive.

### Documentation

- `MapFast` stops being a footnote. It has a section of its own in the getting-started guide, with
  when it is worth it and when the answer is the source generator, and the readme says why it
  matters instead of mentioning it in passing.
- The six main rows of the performance table were measured again in full, every column in the
  same run. Our columns were not updated on their own: mixing runs would have produced a table
  that flatters us by accident of measurement, and two runs on this machine have put the same
  untouched library twenty per cent apart.
- The wall is written down: `mapper.Map<A,B>(x)` is a generic method through an interface and its
  dispatch costs about 7.4 ns, while **everything** Mapster spends above hand-written code is
  about 9. There is no room to win in without changing `IMapper`. `MapFast` closes most of the gap
  but does not reliably beat it: 1.95x against 1.80x in one run and 1.43x against 1.51x in
  another, same code and same machine. What does hold is that **the source generator beats
  Mapster** in all three rows it appears in.
- The site stops being the default DocFX template. A theme of its own: Fraunces for headings, IBM
  Plex Sans for text, IBM Plex Mono for code, a deep green on warm ivory in light and a greenish
  black in dark. The logo and the favicon become the package icon, the same one NuGet shows,
  served from the root of the repository rather than duplicated.
- It is an override, not a fork: every rule sets a Bootstrap variable the template already reads,
  and the few that target a class target one its layout declares. A DocFX upgrade brings its own
  fixes and this keeps applying on top.
- Four things only turned up by looking at the built pages, and they are written down in the CSS:
  Bootstrap paints with `rgb` triples rather than colours, so setting `--bs-link-color` leaves
  every link blue; there are three names for what looks like one thing
  (`--bs-secondary-color-rgb` and `--bs-secondary-rgb` are different variables); half the
  selectors in the first attempt pointed at `article.content`, which does not exist, because the
  class is on the parent `div`; and code blocks cannot go dark on a light page because
  highlight.js ships a palette per theme, and forcing the surface leaves comments and strings
  unreadable.
- That second one was a real accessibility failure: the "View source" link and the namespace line
  on all **260 API pages** kept Bootstrap's grey, at 3.53:1 against the dark background, under the
  4.5:1 normal text needs. Measured afterwards in both themes: the worst case is 7.10 in dark and
  6.06 in light.
- The site is bilingual. English stays where it is and Spanish lives under `/es/`, so no indexed
  URL moves. All five articles and the landing page are translated; the API reference stays
  English, because it is generated from the XML documentation and translating it would mean two
  truths about one signature.
- The switch sits in the navbar through the template's own `iconLinks` hook, so the template
  places it and a DocFX upgrade does not move it. It appears only on pages that have both
  languages, and on a Spanish page the navbar's own links are pointed at the Spanish articles, so
  reading in Spanish does not drop back into English on the next click.
- `website/check-translations.sh` records each translation against the hash of the English page it
  was written from, and the docs workflow fails when they no longer match. Dates were the obvious
  way and the wrong one: a merge brings in commits older than the translation, and the translation
  would still be describing an older library. A page with no translation is a warning, since a
  reader simply stays in English.
- It earned itself immediately: merging the performance work turned the docs build red on
  `getting-started` and `performance`, which are the two English pages `MapperFor` changed. Both
  Spanish pages now carry the new table and the new section.
- The ahead-of-time article says what the generator covers now, lists the four attributes in a
  table, and is straight about the two things it still does not read: the affixes and the naming
  conventions, which live on a `MapperConfiguration` and so do not exist while the project
  compiles.

## [0.11.0] - 2026-09-26

A minor rather than a patch: reading values out of text is new capability — a mapping that used to
throw now works — and that is a minor even though it adds no public API, which it does not.
`PublicAPI.Unshipped.txt` leaves this version empty.

And it is not 1.0, however much the code looks like it. 1.0 is a promise — the public API is
settled, and breaking it costs a 2.0 — and the project is not yet in a position to make it: the
first application to use the library in earnest found a semantic bug within minutes, and nobody
outside this repository has used it at all. From this version the readme says exactly what is
missing, so the wait has an end anybody can check rather than a date.

### Added

- `string` to `Guid`, `DateOnly` and `TimeOnly`. An identifier or a date arriving as text — out of
  JSON, or out of a column somebody typed as `varchar` — is the first thing anyone meets. `string`
  to `DateTime` already worked because `DateTime` is `IConvertible` and these three are not, a
  distinction that means nothing to whoever is writing the map.
- Empty text gives the default, as it already did for enums: absent is not the same as wrong. Text
  that is meant to be a value and is not raises an exception naming it, because the useful thing to
  know is which row had the rubbish in it.
- It is read with the invariant culture. A mapping that understood the same date differently
  depending on the machine would be a worse problem than the one it solves.
- It is looked at **after** a declared map, so `CreateMap<string, Guid>()` still wins for anyone
  who wants their own reading.
- `samples/Mapperion.Migration`: one invoicing layer configured twice over the same domain, once on
  AutoMapper 14 and once on Mapperion, with both mapping the same invoices and compared field by
  field. It exits non-zero if they differ, and CI runs it. The readme's line about migrating being
  mostly a change of namespace becomes something a machine can fail.
- The migrated configuration uses profiles, `IValueResolver`, `ITypeConverter`, `ForCtorParam` onto
  a positional record, `NullSubstitute`, `Condition`, `Ignore`, `AfterMap`,
  `ResolutionContext.Items` filled per call, flattening three hops in, and an enum that only
  crosses by name. The entire diff between the two versions is **one `using` per file and one `!`**.
- That `!` is the only real difference: Mapperion types the items bag as
  `IDictionary<string, object?>` where AutoMapper types it as `IDictionary<string, object>`. Ours
  is the more truthful of the two — nothing stops a caller putting a null in it — which is why a
  cast out of it needs an operator it did not need before.
- What it does **not** prove, said in the sample itself: matching shapes is a smaller claim than
  "your migration will be easy". It says nothing about anyone's build, their container, or thirty
  profiles written by somebody who does not know both libraries.

## [0.10.0] - 2026-09-26

The first one without a `-preview` suffix. Not because the API has stopped moving — before 1.0 a
minor may still break things, as the line at the top says — but because NuGet does not show
pre-release versions in search and does not install one unless it is asked for by number, and a
library nobody can find does not get the one thing it is short of, which is people using it.

A minor rather than a patch: `ProjectTo` changes what it returns for enums that do not line up by
number. It is a fix, but it changes an answer.

### Added

- A pilot project, `samples/Mapperion.Bookshop`: a small application that uses the library the way
  an application does, not the way a test does. A container, two profiles, EF Core over SQLite,
  `AssertIsValid()` at startup, a list view through `ProjectTo` and a detail view mapped in memory.
  It checks its own output and exits non-zero when something is wrong, so CI runs it as one more
  test.
- What it covers is nothing unusual on its own: it is all of it at once over a single
  configuration, which is exactly where a suite of unit tests is least likely to look.

- `Mapperion.Analyzers`, a separate and optional package that reads the configuration at compile
  time. Four rules: **MPR1001** the same pair declared twice, **MPR1002** a member given two
  sources, **MPR1003** a member both ignored and sourced, **MPR1004** `ConstructUsing` next to
  `ForCtorParam`.
- It is its own package so that `Mapperion` keeps having no dependencies at all, which is one of
  the few things it can say that most others cannot. It ships no runtime code and nothing of it
  reaches your output.
- All warnings, no errors. Two of the four describe something that throws when the configuration is
  built, but those same two can be written across branches of an `if` where only one of them runs.
  Anyone who wants them to stop the build already turns warnings into errors.
- What it does **not** do: tell you whether a destination member will find a source. That is what
  `AssertIsValid()` is for. Answering it here would mean a second copy of the convention engine
  beside the first one, drifting from it over time, and a wrong answer from an analyzer is worse
  than no answer.
- Everything is matched by symbol rather than by name: a `ForMember` on somebody else's fluent
  builder is left alone. There is a test that says exactly that.
- Run over the library's own suite: 320 tests and three warnings, all three in tests written on
  purpose to check that mistake. They are silenced at those three sites with a `#pragma` that says
  why, and the analyzer goes on watching everything else.
- The first version of MPR1002 said that configuring a member twice left only the last one
  standing. That is false: settings accumulate, and a test in the library already said so. The rule
  now speaks only about the source, which is the one thing that really is replaced.

- `ProjectTo` over Entity Framework 6. It turned out to work almost entirely already — it is the
  same method from the core, which depends on no ORM — except for one detail: the projection was
  emitting `Expression.Default` nodes for the value of a member that cannot be read, and the EF6
  translator refuses with "Unknown LINQ expression of type 'Default'". EF Core accepts them, so it
  had never come up. It now emits a constant, which both understand, and so does any other
  provider.
- Its own suite in `tests/Mapperion.EntityFramework6.Tests`, on net472 and with no database: EF6
  builds the SQL from its model and `ToString()` on the query returns it, so the SQL is the
  assertion and no server is needed in the build.
- The four benchmarks that were missing: B06 enums, B08 polymorphism, B09 `ProjectTo` over EF Core
  and SQLite, and B11 sixteen threads at once. With those the table in doc 07 is complete.
- B08 and B11 came out well: polymorphism runs at 4.69x of the hand-written version (2.81x with
  `MapFast`) against 8.75x for AutoMapper, and with sixteen workers the multiple *improves* to
  1.73x, which means there is no lock and no per-instance state to queue behind. B09 ties with
  everybody because the time belongs to EF and to SQLite; what it catches is a projection that
  falls back to the client, which would not be a tie.
- B06 found something, the enums by name, and that is fixed below too.
- B08 found another, the abstract base destination, and that one is fixed below.
- Polymorphism and enums by value join the CI budget. The other three do not, and for reasons: by
  name is a known cost rather than a floor to hold, the projection needs a database, and the
  concurrent one depends on how many cores the runner has.
- Configurable naming conventions, with AutoMapper's shape: `SourceMemberNamingConvention` and
  `DestinationMemberNamingConvention`, plus `PascalCaseNamingConvention`,
  `LowerUnderscoreNamingConvention` and `ExactMatchNamingConvention`. A source that writes
  `first_name` and a destination that writes `FirstName` now meet without a `ForMember` per
  property. Ignoring case was not enough: they differ by a character, not by capitalisation.
- Flattening crosses the two spellings, so `ShipToCityName` reaches `ship_to.city_name`. And
  `ExactMatchNamingConvention` as the source convention turns flattening off, as it does in
  AutoMapper.
- The defaults leave a name exactly as it stands, so no existing configuration changes behaviour or
  pays anything for this.
- `Explain()` writes out, member by member, what a map resolved to and where each value comes from.
  It separates what was configured by hand from what a convention decided, which is where almost
  all the confusion comes from when a member brings something unexpected, and it makes the one left
  without a source plainly visible, which is why someone opens this in the first place. Three
  forms: by type arguments, by `Type`, and with no arguments for every declared map.
- It is text to read, not to parse: the wording will change when a better one turns up. It runs no
  mapping, it only reads the model that is already built.
- A performance regression budget in CI. The tests say what a mapping returns; nothing said how
  long it takes, and the two come apart easily: a change can leave every result identical and
  double the time with the whole suite green. It nearly happened while the error-path option was
  being written, and only a second read of the code avoided it.
- What is compared is the multiple over the hand-written mapping measured in the same run, never
  the time: a shared runner moves both figures together, so the ratio survives what the nanoseconds
  do not. The baseline lives in `benchmarks/baseline.json` and is raised by hand.
- Checked in both directions: it passes on a clean tree, and with the inlining switched off on
  purpose it fails on the collection and on the nested one, saying which and by how much.

### Fixed

- **A projection was crossing enums by number while the engine crossed them by name.** The same
  order came out `Shipped` in the detail view and `Placed` in the list, with a single `CreateMap`
  and a single policy behind both. The pilot found it within ten minutes of existing.
- The projection now emits the correspondence as a chain of conditions that the provider turns into
  a `CASE`, taken from the same table the mapping engine uses — now in `EnumCorrespondence`, shared
  by both — so they cannot disagree again. EF Core and EF6 both translate it, and there are tests
  in both suites saying so, including one that checks the `CASE` is done by the database and not by
  the client.
- The one thing a projection cannot do is raise the error `ByName` raises in memory for a value
  with no counterpart: nothing of ours runs per row, and the SQL either has an arm for a value or
  it does not. A value outside the declared ones falls through to its number, which is what it did
  before.
- Mapping enums **by name** cost 25x the hand-written mapping and allocated 280 B where the
  hand-written one allocates 40. The name was resolved on every call: a `ToString()` to get it out
  of the source, an `Enum.TryParse` against the destination and another `ToString()` to confirm the
  name came back unchanged — per member, per map.
- Both types are known when the plan is compiled, so the correspondence is settled there once and
  emitted as a `switch`. **4.56x and 40 B**, the same allocations as the hand-written version.
- No answer changes, none. Only the members that can be settled at compile time become cases;
  everything else — a value outside the declared ones, a combination of flags, a name the
  destination lacks under `ByName` — falls to the `default`, which is the same run-time call as
  before, with the same exception and the same message. The eleven new tests were written against
  the new implementation and were also run against the old one, which is what shows that only the
  speed moved.
- Mapping enums **by value** cost 15x the hand-written mapping and allocated 400 B, ten times what
  the hand-written one allocates: it was the worse of the two routes, not the good one. The number
  was carried across with a `Convert.ToInt64` and an `Enum.ToObject`, which box twice per member,
  when carrying it is exactly what a conversion does. It is now emitted as one: **from 82.7 ns to
  20.6 ns and from 400 B to 40 B**, the same allocations as the hand-written version.
- A defect nobody had seen goes with it: passing the value through an `Int64` made a `ulong` enum
  holding more than `long.MaxValue` raise `OverflowException`, even on its way into an enum of
  exactly the same shape, where nothing was being narrowed. Checked across all 64 combinations of
  underlying types: 56 identical, and the 8 that change are all that one case.
- Both halves of B06 join the CI budget. Neither deserved it before: at 25x and 15x, pinning them
  would have protected nothing.

- A polymorphic map whose base destination is **abstract** did not compile at all. The plan insisted
  on being able to construct the destination even when the map carried an `Include` for every
  concrete type, and an abstract class has no public parameterless constructor, so the first map
  raised `MapperConfigurationException`. AutoMapper accepts the same configuration, which made this
  a direct migration blocker: anyone with a hierarchy of DTOs on an abstract base — which is the
  normal way to have one — could not move across.
- Now, when the map has derived maps, that construction is emitted as a map-time exception rather
  than refused at compile time. It is reached only when no derived map matched, and it then says
  which type turned up and what to declare. Without derived maps, a destination that cannot be
  constructed is still a configuration error, which is what it is. And if the caller brings their
  own destination instance, it is written into as always: there is nothing to construct.
- The B08 benchmark went back to the abstract base, which is the real shape, and measures the same
  as it did with the concrete one.

- Restoring the solution failed on Linux from the moment the EF6 tests arrived. The project was
  left with no target framework at all off Windows, and NuGet does not restore a project like that:
  the build died with an `MSB4181` that names neither the project nor the reason. Off Windows it is
  now an empty assembly that restores, compiles nothing and is not a test project, so `dotnet test`
  does not look at it. On Windows it is still net472 with its five tests.

### Changed

- **The B06 by-value benchmark was wrong, and so were the numbers published from it.** I had given
  the destination the source's own enum types, and the conversion never even looks at those —
  identical types are assigned straight across — so it was timing five assignments. It shows in
  AutoMapper's column, which goes from 10.06x to 39.95x without having changed: it was not
  converting anything before either. Both halves now use the same destination with different types
  and only the policy separates them.
- What I said in the previous version about by name being faster than by value was also wrong. I
  compared two multiples over different floors: five hand-written casts take 4.0 ns and five
  hand-written `switch`es take 5.7 ns, so by value carries the larger multiple (5.21x against
  4.56x) while being the faster of the two in absolute terms (20.6 ns against 25.7 ns).

## [0.9.0-preview.1] - 2026-09-23

A minor rather than a patch because the strong name changes the identity of the assemblies, and
that is a break. Before 1.0 it is allowed, and it is when it costs least.

### Added

- All three boxes carry an icon, so NuGet stops showing the generic placeholder.
- The public surface is written down in `PublicAPI.Shipped.txt` next to each project that is
  published, and the build compares the two: adding, removing or changing anything public breaks
  the compilation until the file is updated, which puts it in the diff. A single file covers all
  six TFMs, because the surface is identical on all of them.
- `CONTRIBUTING.md`, with what to do when the build fails over that, and the house rules.
- A documentation site in `website/`, generated with DocFX and published to GitHub Pages from
  `main`. The API reference comes from the XML documentation the build already requires on every
  public member, so it cannot drift from the code. Four hand-written articles: getting started,
  migrating from AutoMapper, AOT and performance. The source lives in `website/` and not in
  `docs/`, which is outside the repository.
- `GOVERNANCE.md`, `SECURITY.md` and `CODE_OF_CONDUCT.md`. The governance one explains why the
  licence commitment is worth anything: there is no CLA and no copyright assignment, so relicensing
  future versions would need the agreement of everyone who has contributed, and the ones already
  published stay MIT forever whatever happens. It also says where that protection is still thin,
  which is today, with a single contributor.
- Every assembly carries a strong name, with the key `mapperion.snk`, which is in the repository.
  Without it a signed codebase on .NET Framework cannot reference Mapperion at all, and .NET
  Framework is a target this library takes seriously. The key is committed on purpose: a strong
  name is identity, not security, and anyone can strip one and re-sign with their own.

### Changed

- **The identity of the assemblies changes**, and they now carry the token `03d4952d6f16ebf2`.
  Anyone referencing 0.8.0-preview.3 will see a different assembly when they update. It is done
  now, days after publishing for the first time and before 1.0, because later it would cost far
  more.
- Author signing of the packages is still not done, and it is not a matter of work: nuget.org
  requires a code signing certificate that chains to a trusted root and rejects self-issued ones.
  What there is, at no cost, is that nuget.org repository-signs everything it accepts.

## [0.8.0-preview.3] - 2026-09-23

The first public version. Everything below piled up before anything was published, so this entry is
long for once; the ones after it will not be.

### Added

- `MapFast`, an extension method on `IMapper` that does the same mapping while avoiding the cost of
  calling a generic method through an interface. It recognises the mapper the library builds and
  calls it directly; with any other implementation, such as a decorator, it falls back to the
  interface and goes on working. It changes nothing about `IMapper`, so the usual `Map` is
  untouched.
- On the flat scenario it goes from 28.1 ns to 18.7 ns, from 2.75x to 1.83x over the hand-written
  mapping, which ties with Mapster inside the error bars. The saving is a fixed cost per call, so
  the more work the mapping has, the less it weighs. For a genuinely hot path the source generator
  is still the better answer: it is at 0.93x.
- `IncludeMembers(s => s.Applicant, s => s.Employment)` builds one destination out of several
  nested source objects. The map is consulted first: what it configures explicitly and what its own
  conventions resolve wins, and only what is left without a source is offered to the included
  members, in the order given. The first one with something to say provides it, and a member the
  included map ignores counts as having nothing to say.
- If there is a declared map for the included type it is used, so its renames and its
  `IValueConverter`s travel with it; if there is not, the member is matched against the included
  type by the same conventions as always, without forcing a map nobody would need.
- An `IValueResolver` from the included map receives the included instance, not the outer source. A
  condition is reported rather than dropped: it is written against the included type and there is
  no way to carry it over to the outer one.
- An included member that is null leaves what it would have filled at zero, the same as a flattened
  path with a null along the way already does.
- `ProjectTo` goes through included members when what they provide is a path or an expression.

- `samples/Mapperion.Aot`, an application published with `PublishAot=true` that maps with the
  generated code and checks its own result, exiting non-zero when something is wrong. It has the
  trimming and AOT analysers turned on, so an ordinary build already fails on anything the trimmer
  cannot follow, and CI publishes it natively and runs it. Until now AOT support was a claim with
  nothing behind it; now it is measured on every build.

- `ForPath(d => d.Address.Street, ...)` writes a member that sits inside the destination rather
  than on it. The objects along the way are created if missing; one that cannot be written and is
  null has to arrive in place, and the map says which. Paths are assigned after every direct
  member, so configuring both the whole object and something inside it leaves the path with the
  last word rather than depending on declaration order. A single-step path is an ordinary member. A
  projection reports one rather than ignoring it.
- `ConstructUsing`, with AutoMapper's two overloads: the one that takes only the source and the one
  that also takes the `ResolutionContext`. The factory runs only when the destination has to be
  created, so mapping onto an instance the caller brings still uses that one. Declaring it next to
  `ForCtorParam` is refused when the configuration is built, because the factory would win and the
  parameters would do nothing.
- `ResolutionContext.Items`, with `Map` overloads taking an `Action<IMappingOperationOptions>` to
  fill them. They are for passing context that is not in the source object, such as the current
  user or tenant. The dictionary belongs to one operation and is not shared with another. State is
  only allocated when the configuration has something that could read it: a converter, a resolver
  or a step that takes the context.
- `MapperHost`, a slot for a statically reachable `IMapper`, meant for .NET Framework without a
  container and documented as a last resort. Installing a second one without calling `Reset` first
  is refused, because it would change what already-written calls resolve to mid-run.

- An object graph that closes on itself no longer takes the process down. The map that closes the
  loop counts its own depth and raises `RecursionLimitException` past `RecursionLimit`, which
  defaults to 64 levels, the same value `System.Text.Json` and Newtonsoft use for the same
  protection. Before, the recursion ended in a `StackOverflowException`, which cannot be caught and
  takes the process with it: that is the shape of AutoMapper's CVE-2026-32933, which will not be
  patched on its MIT line.
- Only the maps that close a loop with no `MaxDepth` and no `PreserveReferences` are counted, so a
  configuration whose types cannot recurse pays nothing for this, and a map that already protects
  itself keeps its own behaviour.
- `RecursionLimit` in the configuration, to raise it when the graph really is deeper. Zero or less
  removes the ceiling and brings the stack overflow back.
- `RecursionLimitException` derives from `MappingException`, so an existing `catch` still catches
  it, and it carries the map and the limit that was reached.

- Inheritance and polymorphism. `Include<TDerivedSource,TDerivedDestination>()` makes mapping
  through a base reference produce the matching derived destination; the checks are emitted
  most-derived first, so a hierarchy several levels deep picks the closest match rather than the
  first one that fits.
- `IncludeBase<TBaseSource,TBaseDestination>()` takes the member configuration from the base map
  before the conventions run. What the derived map configures wins.
- A collection of the base type maps each element to its own derived type.
- Validation reports an `Include` or an `IncludeBase` pointing at a map that was not declared, and
  an `Include` whose derived destination does not inherit from the base destination.
- A projection reports a polymorphic map: the shape of a projection is fixed before any row is
  read, so it cannot depend on the run-time type.
- Open generics: `CreateMap(typeof(Page<>), typeof(PageDto<>))` declares a template the engine
  closes the first time a matching pair arrives, and keeps the result. It works the same inside a
  `Profile` and with several type arguments.
- A closed map declared by hand takes precedence over the template that would also match.
- `IOpenMappingExpression` exposes only what can be said without knowing the types:
  `IgnoreMember(name)`, `ValidateMemberList`, `MaxDepth` and `PreserveReferences`. Configuring a
  member with an expression against a type that has no arguments yet would make no sense, so the
  members are left to the conventions when it closes.
- Templates stay out of member validation and cycle detection, neither of which means anything
  about an unclosed type. What is checked is that both sides have the same number of type
  arguments, and that an open type is not mixed with a closed one.
- The `Mapperion.SourceGenerator` package: an incremental Roslyn generator that writes the bodies
  of the `partial` methods of a class marked `[Mapper]`. The output is ordinary C#, with no
  reflection and no code emitted at run time, which is what makes it valid under trimming and AOT.
- Matching by exact name and then ignoring case, the same as the run-time engine;
  `[MapProperty("Customer.Address.City", "CustomerCity")]` for explicit paths, with a null guard at
  every step; `[MapperIgnore]` to skip a member.
- It covers nested objects by calling another method of the same mapper, collections with `Select`
  and `ToList`/`ToArray`/`ToHashSet`, nullables, enums, numeric conversions, `ToString` and
  construction through a constructor, records included.
- Six diagnostics, `MPR0001` to `MPR0006`, for what it cannot write: a class that is not `partial`,
  a member with no source, a conversion that does not exist, a destination that cannot be
  constructed, an unsupported signature, and an attribute naming a member that does not exist.
- The attributes are emitted by the generator itself on every compilation, `internal`, so the
  package drags in no run-time dependency and two assemblies never collide.
- `BothEnginesAgreeTests` puts the same cases through both engines and compares the results. It is
  the guarantee ADR-0005 left as a condition: the two share not one line of code, so the only thing
  keeping them honest is running them against the same thing.

- Complete planning documentation (`docs/`), four ADRs included.
- The skeleton of the solution: multi-targeting `netstandard2.0;net8.0;net9.0`, Central Package
  Management, an `.editorconfig` with mandatory style and warnings as errors.
- Base contracts: `IMapper`, the exception hierarchy and `TypeMapKey` from the configuration model.
- The final name settled: **Mapperion** (D-01 closed after checking availability on NuGet, GitHub
  and product collisions).
- The `net10.0` target framework added to the core and to the tests.
- The configuration model (layer 2): `MapperModel`, `MapperOptions`, `TypeMapDefinition`,
  `MemberDefinition`, `ConstructorParameterDefinition`, `MemberSource` and its four variants,
  `MemberPath`, `MemberDescriptor` and the policy enumerations.
- `Internal/Guard` centralises the null checks and the `#if` that `netstandard2.0` requires.
- ADR-0005: pins down what the run-time engine and the source generator really share, correcting
  ADR-0001 and the architecture document.
- The fluent API (layer 1): `MapperConfiguration`, `IMapperConfigurationExpression`,
  `IMappingExpression<,>` and `IMemberConfigurationExpression<,,>`, with `CreateMap`, `ForMember`,
  `MapFrom`, `Ignore`, `Condition`, `NullSubstitute`, `SetMappingOrder`, `UseDestinationValue`,
  `ValidateMemberList`, `MaxDepth` and `PreserveReferences`.
- `MemberExpressionParser` translates the lambdas into elements of the model: a chain of members is
  kept as a `MemberPathSource` and any other expression as an opaque `CustomSource`.
- Conventions (layer 3): matching by exact name, ignoring case, with configurable prefixes and
  suffixes, and flattening up to `MaxFlatteningDepth`. Explicit configuration always wins, and a
  member with no source is left with a null `Source` so validation reports it.
- `Profile`, `AddProfile<T>()`, `AddProfile(instance)` and `AddProfiles(assemblies)`, brought
  forward from v0.2 for being the biggest blocker to migrating from AutoMapper.
- The paths that use reflection are annotated `[RequiresUnreferencedCode]`, the `MapperConfiguration`
  constructor included. `IsAotCompatible` is withdrawn from the core: it was a false claim while the
  engine depends on reflection and on `Expression.Compile`.
- `Internal/TrimmingAttributes.cs` provides the polyfill for `RequiresUnreferencedCodeAttribute` on
  `netstandard2.0`, which PolySharp does not cover.
- The `docs/` folder is left outside the repository.
- `AssertIsValid()` and the alias `AssertConfigurationIsValid()`: they report every problem together
  in `MapperConfigurationException.Errors`, never only the first. They catch destination members
  with no source, missing nested maps (unwrapping nullables and collections) and, with
  `MemberListValidation.Source`, source members nobody consumes.
- `Internal/TypeClassifier`: tells simple types, nullables and sequences apart.
- `ValidateOnBuild` becomes `false` by default, like AutoMapper, so that freshly migrated
  configurations do not break at startup.
- The global `MemberListValidation` is now really applied to the maps that do not override it.
- The expression compiler (layer 4) and the execution engine (layer 5):
  `MapperConfiguration.CreateMapper()` returns an `IMapper` with `Map<TDest>(object)`,
  `Map<TSource,TDest>(source)`, `Map(source, destination)` and the non-generic overload.
- Plans are compiled the first time each pair is used and cached. Nested maps are resolved at run
  time rather than inlined, which is what lets two maps reference each other without the compiler
  recursing.
- Conversions: nullables both ways, numeric ones, enums according to the configured policy, enum
  with string, `ToString` and `IConvertible` as a last resort.
- Collections: array, `List<>`, `HashSet<>`, `ISet<>` and the sequence interfaces, with
  `AllowNullCollections` respected.
- Flattened paths with null guards that evaluate each step exactly once.
- `Condition`, `NullSubstitute`, `SetMappingOrder` and `UseDestinationValue` reach the generated
  code.
- Constructor mapping: positional records, primary constructors and any destination with no
  parameterless constructor. The overload with the most resolvable arguments is chosen; parameters
  with a default value cover what the source does not provide.
- `ForCtorParam(name, o => o.MapFrom(...))` and `UseValue(...)` to configure an argument by hand.
- Parameter names are always compared ignoring case: C# names parameters in camelCase and
  properties in PascalCase, so an exact comparison would never match a record's primary constructor
  with the source's properties.
- A member fed by the constructor is no longer assigned again after construction.
- Validation reports constructor parameters with no source, and the maps they are missing.
- `ReverseMap()`: declares the reverse pair and returns it so it can be configured further. It
  inverts members renamed with a `MapFrom` onto a single writable member; flattened paths, arbitrary
  expressions and ignored members are not inverted, and the reverse resolves those by convention. It
  works the same inside a `Profile`.
- A CI workflow: build and test on Linux and Windows against .NET 8, 9 and 10, and `pack` of the
  core.
- `ITypeConverter<TSource,TDest>`, `IValueConverter<TSourceMember,TDestMember>` and
  `IValueResolver<TSource,TDest,TMember>`, with `ResolutionContext` as the way back to the mapper so
  that user code can map nested values.
- `ConvertUsing<TTypeConverter>()` replaces the whole map of a type pair; in that case member
  configuration stops applying and member validation is skipped.
- `ConvertUsing<TValueConverter, TSourceMember>()` and `MapFrom<TValueResolver>()` per member, both
  with type constraints, so the C# compiler rejects a converter that does not fit.
- Converters and resolvers are instantiated once and reused, cached by the engine. For now they need
  a parameterless constructor; resolution through DI arrives in v0.4.
- A resolver feeding a constructor parameter receives the default destination, because it does not
  exist yet when the arguments are worked out.
- `BeforeMap` and `AfterMap`, each in three forms: a two-argument lambda, a lambda with
  `ResolutionContext`, and `IMappingAction<TSource,TDestination>` as a type of its own.
- The steps are placed in the compiled plan in the order they were declared: the before ones right
  after the destination is created, the after ones once every member is assigned. Steps written as a
  type are instantiated once, like converters and resolvers.
- A map with `ConvertUsing` runs no steps: the converter replaces the whole map, just as it replaces
  the member configuration.
- The `Mapperion.Extensions.DependencyInjection` package with `AddMapperion(...)`, in three forms: a
  configuration callback, assemblies to scan, or marker types.
- `MapperConfiguration.CreateMapper(IServiceProvider)`: the mapper asks the container for
  converters, resolvers and actions, and falls back to building the ones it does not know about
  directly, so a resolver with no dependencies does not need registering.
- Instance resolution moves out of the engine into `IServiceResolver`. The engine still owns the
  compiled plans and is shared between every mapper of one configuration, so registering `IMapper`
  as scoped recompiles nothing.
- `MappingContext` now also carries the resolver and the mapper in play, so that
  `ResolutionContext.Mapper` returns the one from the scope rather than a global one.
- `ProjectTo`: `IQueryable.ProjectTo<TDest>(configuration)` and `IMapper.ProjectTo<TDest>(query)`. A
  separate projection compiler emits an `Expression<Func<TSource,TDest>>` that a LINQ provider knows
  how to translate: member initialisation, member access, conditionals and `Select`, with no blocks,
  no variables and no calls into this library.
- It lives in the core: all it needs is `IQueryable` and `System.Linq.Expressions`, so the
  `Mapperion.EntityFrameworkCore` package that was planned is not needed.
- What a provider cannot run is reported rather than dropped: type converters, value converters,
  resolvers and the `BeforeMap`/`AfterMap` steps. AutoMapper drops them silently; the error was
  preferred because a projection that differs from the same map through `Map` is an expensive bug to
  find.
- A map that references itself is reported too: a projection is expanded completely in advance, so a
  cycle has no end.
- Projections are cached per type pair, the same as plans.
- Real integration tests with EF Core and SQLite in memory: they verify the query is translated to
  SQL, that only the destination's columns are asked for, and that filtering and paging still happen
  in the database.
- The `netstandard2.1` and `net472` TFMs in both packages, which now publish six: `netstandard2.0`,
  `netstandard2.1`, `net472`, `net8.0`, `net9.0` and `net10.0`.
- `Microsoft.NETFramework.ReferenceAssemblies` provides the .NET Framework reference assemblies, so
  compiling `net472` does not require the Developer Pack to be installed, not even on Linux.
- `Mapperion.Compatibility.Tests`: a representative slice of the library running on real .NET
  Framework (net472 and net48) as well as net8.0 and net10.0. The project narrows its TFMs off
  Windows, where .NET Framework cannot run.
- Dictionaries: `Dictionary<,>`, `IDictionary<,>` and `IReadOnlyDictionary<,>`, converting both keys
  and values. They are checked before collections, because a dictionary is also a sequence of
  `KeyValuePair<,>` and would be mapped wrongly.
- Validation recognises them now too: it used to report a missing map from `KeyValuePair` to
  `KeyValuePair` for a dictionary that actually mapped fine.
- A projection refuses dictionaries with a clear message: a query provider has no way to
  materialise one.
- `PreCondition(s => ...)`: drops the member before its source is even read.
- `Condition` is now evaluated **after** the value is resolved, as in AutoMapper. It used to behave
  as a precondition, which left the two indistinguishable; now `Condition` pays for the read and
  `PreCondition` avoids it, which is exactly the difference between them.
- A failure at mapping time now says which member caused it, with the full path through nested maps
  and collections: `Batch.Readings[0].Ratio`. The original exception is left as the
  `InnerException`.
- `MappingException.MemberPath` is really filled in; it existed before and nobody wrote to it.
- A configuration problem discovered while compiling a nested map mid-mapping still comes out as a
  `MapperConfigurationException`, not disguised as a mapping failure.
- The plan's `catch` carries no exception filter: a filter compiles to an IL filter block and
  `DynamicMethod` rejects those on .NET Framework. The compatibility tests caught it, passing on
  .NET 8, 9 and 10 and failing on net472 and net48.

### Changed

- The plan for a pair known at compile time is reached through a numbered slot in an array rather
  than by looking a key up in a dictionary. The number is a `static readonly` of a generic type,
  which the JIT folds to a constant, so there is no key to build and no hash to compute: 6.1 ns to
  3.2 ns. The dictionary is still the only place a plan is created; this is a cache in front of it.
- The context of an operation that needs no state is built once when the mapper is created, not on
  every call.
- Over the hand-written mapping: the record through its constructor goes from 5.45x to 3.46x, the
  existing destination from 4.52x to 3.27x, the flattening from 4.27x to 3.40x and the flat one from
  3.50x to 3.01x. The big improvements are where the fixed cost weighed most, which is maps with few
  members.

### Fixed

- `MemberDescriptor` is compared by declaring type, kind and name, not by the raw `MemberInfo`.
  Reflection returns a different `MemberInfo` for the same property depending on the type it is
  reached through, so an inherited property showed up as two different members and was mapped
  twice. It only surfaced with inheritance, but the defect had been there from the start.

- `MaxDepth` and `PreserveReferences` **work**. They were configured, they were kept in the model,
  and the compiler ignored them entirely: silent no-ops since the fluent API existed. The
  consequence was worse than a dead option, because a graph with a real cycle recursed until the
  stack was gone and took the process down with an exception that cannot even be caught.
- `PreserveReferences` registers the destination right after creating it and before mapping any
  member, which is what lets a cycle find its way back. One source instance always produces the
  same destination within an operation.
- `MaxDepth` cuts the recursion of that type pair and leaves the default value. The counter is
  released in a `finally`, so an exception does not leave it raised.
- Per-operation state is only created if some configuration asks for it, and the dictionaries it
  carries are created on first use: a map that uses neither of the two pays nothing.
- `AssertIsValid()` now detects unguarded cycles by walking the graph of maps, so what used to kill
  the process in production is an error at startup.
- `PreserveReferences` on value types is reported: there is no identity to preserve.
- `AllowNullDestinationValues` **works**. It was the last silent no-op: it was configured, it
  reached the model and nobody read it. With the option off, a member whose source resolves to null
  receives the destination type's empty content: an empty string, or a new instance if the type has
  a parameterless constructor. Value types are untouched and collections go on answering to
  `AllowNullCollections`, which is the option that speaks about them. In a projection it only
  applies to the string case: a query provider cannot build an object out of nothing.
- Collections are always rebuilt, never shared. Before, a `List<X>` to `List<X>` or to
  `IReadOnlyList<X>` went through the shortcut for identical or assignable types and the destination
  was left with **the same list** as the source: changing one changed the other, and
  `AllowNullCollections` had no effect on those pairs either. A test of the interaction between the
  two options uncovered it.
