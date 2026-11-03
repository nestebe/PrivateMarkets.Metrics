# PrivateMarkets.Metrics.Mcp

MCP server (stdio) exposing [PrivateMarkets.Metrics](https://www.nuget.org/packages/PrivateMarkets.Metrics) to AI agents.

| Tool | What it computes |
|---|---|
| `compute_fund_metrics` | TVPI, DPI, RVPI, PIC, MOIC, paid-in, distributions, NAV, unfunded, since-inception IRR |
| `compute_pme` | Kaplan-Schoar PME, Direct Alpha, Long-Nickels, PME+, mPME against a benchmark index |
| `vintage_summary` | Pooled metrics and quartiles per vintage year |
| `compute_j_curve` | Metrics at each period end (J-curve) |

## Use it from an MCP client

```json
{
  "mcpServers": {
    "private-markets": {
      "command": "dotnet",
      "args": ["run", "--project", "src/PrivateMarkets.Metrics.Mcp"]
    }
  }
}
```

Once packed as a .NET tool (`dotnet pack`), the server can also be started with `privatemarkets-mcp`.

Cash flow amounts are positive; their direction comes from `type` (`Call`, `Distribution`, `Fee`, `RecallableDistribution`). Dates use `yyyy-MM-dd`.
