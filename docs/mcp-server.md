# MCP server for AI agents

`PrivateMarkets.Metrics.Mcp` is a [Model Context Protocol](https://modelcontextprotocol.io) server. Once it is
connected, an assistant such as Claude, GitHub Copilot or any MCP client can compute fund metrics, PMEs,
vintage summaries and J-curves itself, with audited formulas instead of improvised arithmetic.

Example prompts once connected:

- *"Here are the cash flows of Fund III (pasted from the capital account statement). Compute TVPI, DPI and IRR at 30 September 2026."*
- *"Benchmark this fund against the MSCI World levels in this CSV and tell me whether it beat the index."*
- *"Group these 12 funds by vintage and tell me in which quartile Fund III sits for TVPI."*

## 1. Install the server

The server runs over **stdio** and needs the .NET 10 runtime.

**As a .NET tool (recommended).** From a clone of the repository:

```bash
dotnet pack src/PrivateMarkets.Metrics.Mcp -c Release -o ./artifacts
dotnet tool install --global PrivateMarkets.Metrics.Mcp --add-source ./artifacts
```

This puts a `privatemarkets-mcp` command on your `PATH`. Once the package is published on NuGet, the second
command alone (without `--add-source`) is enough. Update with `dotnet tool update --global PrivateMarkets.Metrics.Mcp`.

**From source, without installing.** Publish once and point the client at the DLL:

```bash
dotnet publish src/PrivateMarkets.Metrics.Mcp -c Release -o ./mcp-server
```

and use `"command": "dotnet", "args": ["<absolute path>/mcp-server/PrivateMarkets.Metrics.Mcp.dll"]` in the
configurations below. Prefer this to `dotnet run`, which rebuilds at every start and is slower.

## 2. Connect a client

### Claude Code

```bash
claude mcp add private-markets -- privatemarkets-mcp
```

### Claude Desktop

Edit `claude_desktop_config.json` (Windows: `%APPDATA%\Claude\`, macOS: `~/Library/Application Support/Claude/`)
and restart Claude Desktop:

```json
{
  "mcpServers": {
    "private-markets": {
      "command": "privatemarkets-mcp"
    }
  }
}
```

### VS Code (GitHub Copilot agent mode)

Create `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "private-markets": {
      "type": "stdio",
      "command": "privatemarkets-mcp"
    }
  }
}
```

### Any other client

Start `privatemarkets-mcp` as a stdio server. It answers `initialize`, `tools/list` and `tools/call` as
defined by the MCP specification. Logs go to stderr, never to stdout.

## 3. Tools

All tools are read-only and idempotent. Dates use `yyyy-MM-dd`. Results are JSON with the same camelCase
fields as [`JsonExport`](export.md#json).

### The `fund` object

```json
{
  "name": "Fonds III",
  "commitment": 50000000,
  "currency": "EUR",
  "vintageYear": 2021,
  "cashFlows": [
    { "date": "2021-03-15", "amount": 10000000, "type": "Call" },
    { "date": "2022-01-10", "amount": 15000000, "type": "Call" },
    { "date": "2024-06-30", "amount": 8000000, "type": "Distribution" }
  ],
  "navs": [
    { "date": "2026-09-30", "value": 31500000 }
  ]
}
```

| Field | Required | Notes |
|---|---|---|
| `name` | yes | |
| `cashFlows` | yes | positive `amount`; `type` is `Call`, `Distribution`, `Fee` or `RecallableDistribution` (case-insensitive) |
| `navs` | no | reported NAVs |
| `commitment` | no | default 0 (then `pic` is `null`) |
| `currency` | no | default `EUR` |
| `vintageYear` | no | default: year of the first contribution |

### Common optional parameters

| Parameter | Default | Values |
|---|---|---|
| `dayCount` | `Actual365Fixed` | `Actual365Fixed`, `Actual360`, `Actual36525` |
| `rollForwardNav` | `true` | see [NAV roll-forward](concepts.md#nav-roll-forward) |

### `compute_fund_metrics`

| Parameter | Required | Description |
|---|---|---|
| `fund` | yes | the fund object |
| `asOf` | no | valuation date; default: last cash flow or NAV date |

Returns a `FundMetrics` object (`paidIn`, `distributed`, `residualValue`, `tvpi`, `dpi`, `rvpi`, `pic`, `moic`, `irr`...).

### `compute_pme`

| Parameter | Required | Description |
|---|---|---|
| `fund` | yes | the fund object |
| `index` | yes | `[{ "date": "2021-01-01", "level": 1000 }, ...]`, total-return levels starting on or before the first cash flow |
| `indexName` | no | label echoed in the result |
| `asOf` | no | default: last cash flow or NAV date |

Returns a `PmeResult` (`kaplanSchoar`, `directAlpha`, `longNickelsIrr`, `pmePlusIrr`, `modifiedPmeIrr`, spreads...).
See [Benchmarking with PME](pme.md) for the interpretation.

### `vintage_summary`

| Parameter | Required | Description |
|---|---|---|
| `funds` | yes | array of fund objects, one currency |
| `asOf` | yes | valuation date applied to every fund |

Returns one entry per vintage: `vintageYear`, `fundCount`, `pooled` metrics, `tvpi` / `dpi` / `rvpi` / `irr`
quartile statistics and the fund-level rows.

### `compute_j_curve`

| Parameter | Required | Description |
|---|---|---|
| `fund` | yes | the fund object |
| `frequency` | no | `Monthly`, `Quarterly` (default), `SemiAnnual`, `Annual` |
| `end` | no | last date of the series; default: last cash flow or NAV date |

Returns an array of `FundMetrics`, one per period end.

## 4. Errors

Invalid input returns an MCP tool error whose message says what to fix, for instance
`Unknown cash flow type 'Dividend'. Expected one of: Call, Distribution, Fee, RecallableDistribution.`
or `The index '...' starts on 2021-01-01; no level is available on 2020-11-15.` Assistants usually correct the
input and retry on their own.

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| The client lists no tool | run `privatemarkets-mcp` in a terminal: it should wait silently for input (Ctrl+C to quit). If the command is not found, add the .NET tools folder (`~/.dotnet/tools`) to `PATH` or use the full path in the configuration. |
| `You must install or update .NET to run this application` | install the .NET 10 runtime |
| Server starts then stops immediately | the client closed stdin; check the client's MCP logs |
| Slow first call | normal with `dotnet run`; use the installed tool or a published DLL |
