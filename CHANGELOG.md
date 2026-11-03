# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.4.0] - 2026-11-03

### Added

- Data model: `CashFlow` (call, distribution, fee, recallable distribution), `NavPoint`, immutable fluent `Fund`, `Portfolio`.
- Multiples: TVPI, DPI, RVPI, PIC, MOIC, unfunded commitment, NAV roll-forward.
- Returns: XIRR (Newton with bisection fallback; ACT/365, ACT/360, ACT/365.25), since-inception and period IRR.
- PME: Kaplan-Schoar, Direct Alpha (discrete and continuous), Long-Nickels, PME+, mPME; `IndexSeries` with CSV loading.
- Aggregation: pooled metrics, vintage-year summaries, quartile statistics and quartile rank.
- J-curve time series; CSV and JSON (source-generated) export.
- MCP server `PrivateMarkets.Metrics.Mcp` with `compute_fund_metrics`, `compute_pme`, `vintage_summary`, `compute_j_curve`.
- Parity datasets generated from `privateassets`, `pypme` and `xirr`, checked to 1e-6.
- `.mcp/server.json` in the MCP server package, for nuget.org and the MCP Registry.
- User guide in `docs/`.
- Release workflow publishing both packages to nuget.org with Trusted Publishing (OIDC) when a `vX.Y.Z` tag is pushed.
