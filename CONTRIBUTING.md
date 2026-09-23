# Contributing

## The public surface is written down

`PublicAPI.Shipped.txt` beside each shipping project lists everything that project exposes, and
the build checks the two against each other. Adding, removing or changing anything public fails the
build until the file is updated, which means it shows up in the diff and gets reviewed like the
rest of the change.

- **Adding something public**: the build fails with `RS0016` and the message contains the exact
  line to add. Put it in `PublicAPI.Unshipped.txt`.
- **Removing or changing something public**: the build fails with `RS0017` for the line that no
  longer matches. If the version it was shipped in is already on NuGet, this is a breaking change:
  say so in `CHANGELOG.md`. Before 1.0 that is allowed; after it, it is not.
- **Releasing**: move everything from `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` and
  leave the unshipped file with only its `#nullable enable` line.

An IDE offers "Add to public API" as a fix on `RS0016`, which does the first case for you.

`Mapperion.SourceGenerator` is not tracked this way. What it exposes to a user is the attributes it
writes into their compilation, not the surface of its own assembly, and those are covered by the
generator tests.

## Before opening a pull request

- `dotnet test Mapperion.sln -c Release` passes. It covers .NET Framework 4.7.2 and 4.8 as well as
  .NET 8, 9 and 10, and a failure on Framework alone is a real failure.
- Warnings are errors here, including the analyzers and the missing-documentation one. A public
  member without XML documentation does not build.
- Comments in code are XML documentation and nothing else.

## Releasing

See [RELEASING.md](RELEASING.md).
