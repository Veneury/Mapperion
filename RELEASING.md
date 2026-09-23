# Releasing

A version on NuGet cannot be replaced, only delisted. Everything here exists because of that.

## Once, before the first release

There is no NuGet API key in this repository, and there should never be one. Publishing uses
trusted publishing: the job proves who it is with a token GitHub signs for this workflow, in this
repository, in this environment, and nuget.org hands back a key that lasts an hour.

1. On nuget.org, open *your username → Trusted Publishing* and add a policy:

   | Field | Value |
   |---|---|
   | Policy Name | anything, e.g. `Mapperion release` |
   | Package Owner | the nuget.org account that will own the packages |
   | CI/CD Provider | GitHub Actions |
   | Repository Owner | `Veneury` |
   | Repository | `Mapperion` |
   | Workflow File | `release.yml` — the file name only, no `.github/workflows/` |
   | Environment | `nuget` — must match `environment:` in the workflow |
   | Scopes | Push, *Push new packages and package versions*, pattern `Mapperion*` |

   The workflow file name and the environment are part of what nuget.org checks, so renaming
   either one stops publishing until the policy is updated to match. That is the point of them.

2. Add the nuget.org **profile name** (not the email address) as the repository secret
   `NUGET_USER`, under *Settings → Secrets and variables → Actions*. It is not sensitive, but
   keeping it out of a public file is what NuGet recommends, and the workflow stops with a clear
   message when it is missing.

3. Open *Settings → Environments → nuget* and add yourself as a required reviewer. The release
   workflow waits there before pushing, which is the last chance to stop a release that should not
   go out. The environment is created by the first run if it does not exist, but without a
   reviewer it does not stop anything.

On a private repository a new policy is only provisionally active for seven days and lapses if
nothing is published in that time. This repository is public, so that does not apply, but it is
worth knowing if the repository is ever made private.

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
  `--skip-duplicate`, so what is already on NuGet is left alone. The key is asked for again on the
  re-run, since each one lasts only an hour.
- **The push is rejected as unauthorized.** The policy on nuget.org no longer matches the job: check
  the workflow file name, the environment name and the package owner against the table above.
- **A version went out that should not have.** It cannot be taken back. Delist it on nuget.org so
  it stops appearing in search and in the version list, then release the fix as a new version.
  Delisting does not break anyone who already depends on that exact version, which is the point.
- **The tag was wrong.** Deleting a tag does not unpublish anything. If the workflow had already
  pushed, treat it as the case above.

## Version numbers

Semantic versioning, with the caveat already in `CHANGELOG.md`: before 1.0 a minor version may
break things. Pre-release versions are tagged the same way and are published as pre-release,
which the workflow works out from the `-` in the tag.
