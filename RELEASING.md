# Releasing

A version on NuGet cannot be replaced, only delisted. Everything here exists because of that.

## Once, before the first release

1. Create a NuGet API key at <https://www.nuget.org/account/apikeys> scoped to **Push** for the
   package IDs `Mapperion`, `Mapperion.Extensions.DependencyInjection` and
   `Mapperion.SourceGenerator`. Glob patterns work: `Mapperion*`.
2. Add it to the repository as the secret `NUGET_API_KEY`, under
   *Settings → Secrets and variables → Actions*.
3. Open *Settings → Environments → nuget* and add yourself as a required reviewer. The release
   workflow waits there before pushing, which is the last chance to stop a release that should not
   go out. The environment is created by the first run if it does not exist, but without a
   reviewer it does not stop anything.

## Each release

1. Make sure `main` is green and `CHANGELOG.md` describes the version under a heading of its own
   rather than `Unreleased`.
2. Tag the commit and push the tag:

   ```
   git tag v0.8.0-preview.1
   git push origin v0.8.0-preview.1
   ```

3. The `Release` workflow builds, runs the whole suite on the tagged commit, publishes the
   ahead-of-time sample natively and runs it, packs the three packages and waits for approval.
4. Approve it. The packages go to NuGet and a GitHub release is created with them attached.

The version comes from the tag, so nothing needs editing to release. `VersionPrefix` in
`Directory.Build.props` only names the builds made in between.

## If it goes wrong

- **A package failed to push and others went up.** Re-run the job. The push uses
  `--skip-duplicate`, so what is already on NuGet is left alone.
- **A version went out that should not have.** It cannot be taken back. Delist it on nuget.org so
  it stops appearing in search and in the version list, then release the fix as a new version.
  Delisting does not break anyone who already depends on that exact version, which is the point.
- **The tag was wrong.** Deleting a tag does not unpublish anything. If the workflow had already
  pushed, treat it as the case above.

## Version numbers

Semantic versioning, with the caveat already in `CHANGELOG.md`: before 1.0 a minor version may
break things. Pre-release versions are tagged the same way and are published as pre-release,
which the workflow works out from the `-` in the tag.
