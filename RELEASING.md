# Releasing

Pushing a tag `vX.Y.Z` runs [.github/workflows/release.yml](.github/workflows/release.yml), which:

1. checks that `CHANGELOG.md` has a `## [X.Y.Z]` section;
2. builds, runs every test, and packs `PrivateMarkets.Metrics` and `PrivateMarkets.Metrics.Mcp` with version `X.Y.Z`
   (the tag is the single source of truth; `<Version>` in `Directory.Build.props` is only the local default);
3. publishes both packages and their symbols to nuget.org;
4. creates a GitHub release with the packages attached and generated release notes.

A tag with a suffix (`v0.5.0-rc.1`) publishes a prerelease on nuget.org and GitHub.

## One-time setup

Publishing uses nuget.org **Trusted Publishing**: GitHub Actions exchanges a short-lived OIDC token for a
temporary API key valid for one hour. No API key is stored in the repository.

1. **nuget.org**: sign in, open *your username > Trusted Publishing* and add a policy:

   | Field | Value |
   |---|---|
   | Repository Owner | `nestebe` |
   | Repository | `PrivateMarkets.Metrics` |
   | Workflow File | `release.yml` |
   | Environment | `nuget` |

   The nuget.org profile name (`nestebe`) is set in the `user:` input of the `NuGet/login` step; it is public
   information, not a credential.

2. Optional, recommended: *Settings > Environments > nuget* (created by the first run, or create it beforehand)
   and add yourself as **required reviewer**, so every publication waits for your approval.

## Cutting a release

```bash
# 1. Move the "Unreleased" notes under a new "## [X.Y.Z] - YYYY-MM-DD" heading in CHANGELOG.md, commit, push.
# 2. Tag and push:
git tag vX.Y.Z
git push origin vX.Y.Z
```

Then follow the run in *Actions > Release*. If a step fails before publishing, fix it, delete the tag
(`git push --delete origin vX.Y.Z && git tag -d vX.Y.Z`) and tag again. Once a version is on nuget.org it cannot
be replaced, only unlisted: publish a new patch version instead.

## MCP registry

The MCP server package embeds `.mcp/server.json` (name `io.github.nestebe/privatemarkets-metrics`), stamped with the
package version at pack time, and its README carries the matching `mcp-name` marker. nuget.org uses it to show the
server configuration on the package page, and it allows a later listing in the official MCP Registry.
