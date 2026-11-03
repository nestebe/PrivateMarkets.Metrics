# PrivateMarkets.Metrics.Mcp

<!-- mcp-name: io.github.nestebe/privatemarkets-metrics -->

MCP server (stdio) exposing [PrivateMarkets.Metrics](https://www.nuget.org/packages/PrivateMarkets.Metrics) to AI agents.

| Tool | What it computes |
|---|---|
| `compute_fund_metrics` | TVPI, DPI, RVPI, PIC, MOIC, paid-in, distributions, NAV, unfunded, since-inception IRR |
| `compute_pme` | Kaplan-Schoar PME, Direct Alpha, Long-Nickels, PME+, mPME against a benchmark index |
| `vintage_summary` | Pooled metrics and quartiles per vintage year |
| `compute_j_curve` | Metrics at each period end (J-curve) |

## Install and connect

```bash
dotnet tool install --global PrivateMarkets.Metrics.Mcp
claude mcp add private-markets -- privatemarkets-mcp
```

Other clients (Claude Desktop, VS Code):

```json
{
  "mcpServers": {
    "private-markets": { "command": "privatemarkets-mcp" }
  }
}
```

Full guide, tool parameters and troubleshooting: [docs/mcp-server.md](https://github.com/nestebe/PrivateMarkets.Metrics/blob/main/docs/mcp-server.md).

Cash flow amounts are positive; their direction comes from `type` (`Call`, `Distribution`, `Fee`, `RecallableDistribution`). Dates use `yyyy-MM-dd`.
