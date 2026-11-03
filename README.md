# PrivateMarkets.Metrics

[![NuGet](https://img.shields.io/nuget/v/PrivateMarkets.Metrics.svg)](https://www.nuget.org/packages/PrivateMarkets.Metrics)
[![NuGet MCP server](https://img.shields.io/nuget/v/PrivateMarkets.Metrics.Mcp.svg?label=mcp%20server)](https://www.nuget.org/packages/PrivateMarkets.Metrics.Mcp)
[![CI](https://github.com/nestebe/PrivateMarkets.Metrics/actions/workflows/ci.yml/badge.svg)](https://github.com/nestebe/PrivateMarkets.Metrics/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Performance metrics for private equity and private markets funds in .NET: **TVPI, DPI, RVPI, PIC, MOIC,
XIRR, PME (Long-Nickels, PME+, Kaplan-Schoar, mPME), Direct Alpha**, vintage-year aggregation and J-curves.

- Zero dependencies, `decimal` everywhere except inside the IRR solver (`double`, converted back).
- Targets .NET 8 and .NET 10, trimming and Native AOT compatible.
- Checked against the Python reference implementations (`privateassets`, `pypme`, `xirr`) to **1e-6**.
- Ships with an MCP server so AI agents can call the calculations.

```
dotnet add package PrivateMarkets.Metrics
```

## Documentation

| | |
|---|---|
| [Getting started](docs/getting-started.md) | install and compute a first TVPI / IRR in five minutes |
| [Core concepts](docs/concepts.md) | cash flow types, NAV roll-forward, valuation date, conventions |
| [Multiples, IRR and J-curve](docs/metrics.md) | what each figure means and how it is computed |
| [Benchmarking with PME](docs/pme.md) | KS-PME, Direct Alpha, LN, PME+, mPME and which one to use |
| [Portfolios and vintages](docs/portfolios-and-vintages.md) | pooled metrics, vintage summaries, quartile ranking |
| [Export](docs/export.md) | CSV and JSON for Excel, Power BI or other systems |
| [MCP server](docs/mcp-server.md) | connect Claude, Copilot or any MCP client |
| [Recipes](docs/recipes.md) | load a CSV, quarterly LP report, GP figure checks, period IRRs... |
| [FAQ](docs/faq.md) | `null` values, gaps with Excel or GP reports, errors |

## Quick start

```csharp
using PrivateMarkets.Metrics;
using PrivateMarkets.Metrics.Pme;

var fund = new Fund("Fonds III", commitment: 50_000_000m, currency: "EUR")
    .Call(new DateOnly(2021, 3, 15), 10_000_000m)
    .Call(new DateOnly(2022, 1, 10), 15_000_000m)
    .Distribute(new DateOnly(2024, 6, 30), 8_000_000m)
    .Nav(new DateOnly(2026, 9, 30), 31_500_000m);

var m = fund.Metrics(asOf: new DateOnly(2026, 9, 30));
Console.WriteLine($"TVPI {m.Tvpi:F2}x  DPI {m.Dpi:F2}x  IRR {m.Irr:P1}");
// TVPI 1.58x  DPI 0.32x  IRR 10.5%

var index = IndexSeries.FromCsv("msci-world-tr.csv");   // date,level
var pme = fund.Pme(index);
Console.WriteLine($"KS-PME {pme.KaplanSchoar:F2}  Direct Alpha {pme.DirectAlpha:P2}");
```

`Fund` is immutable: every fluent call returns a new instance. A runnable version lives in
[samples/PrivateMarkets.Metrics.Sample](samples/PrivateMarkets.Metrics.Sample/Program.cs).

### Portfolio, vintages, J-curve, export

```csharp
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Export;

var portfolio = new Portfolio("Programme", [fundIII, fundIV]);   // one currency per portfolio
FundMetrics pooled = portfolio.Metrics(asOf);                     // pooled IRR on combined cash flows
IReadOnlyList<VintageSummary> vintages = portfolio.ByVintage(asOf);
int quartile = vintages[0].Tvpi!.QuartileOf(fundIII.Metrics(asOf).Tvpi!.Value); // 1 = top quartile

IReadOnlyList<FundMetrics> jCurve = fundIII.JCurve(ReportingFrequency.Quarterly);
File.WriteAllText("j-curve.csv", CsvExport.ToCsv(jCurve));
File.WriteAllText("vintages.json", JsonExport.ToJson(vintages));
```

## Data model and conventions

| Type | Role |
|---|---|
| `CashFlow` | Date, positive amount, `CashFlowType`: `Call`, `Distribution`, `Fee`, `RecallableDistribution` |
| `NavPoint` | Reported NAV, end of day (after the cash flows of the same date) |
| `Fund` | Name, commitment, currency, vintage, cash flows and NAVs |
| `Portfolio` | Funds sharing one currency (no implicit FX conversion) |
| `IndexSeries` | Benchmark levels; lookups are *as of* (last level on or before the date) |
| `MetricsOptions` | `DayCount` (`Actual365Fixed` default, `Actual360`, `Actual36525`) and `RollForwardNav` |

- **Paid-in** = calls + fees. **Invested** = calls only. **Distributions** include recallable ones.
- **Residual value** at a date = last reported NAV, rolled forward with later calls (+) and distributions (−);
  fees do not increase the NAV. Without any NAV the roll-forward starts at zero (cost). Disable with
  `RollForwardNav = false`. The value is floored at zero.
- **Unfunded** = commitment − paid-in + recallable distributions.
- Ratios with a zero denominator, and IRRs without a root, are `null`.

## Formulas

Notation: $C_t$ contributions and $D_t$ distributions at date $t$, $NAV_T$ residual value at the valuation
date $T$, $I_t$ index level, $\tau_t$ year fraction from the first cash flow under the day count convention.

### Multiples

$$
\text{TVPI} = \frac{\sum D_t + NAV_T}{\sum C_t} \qquad
\text{DPI} = \frac{\sum D_t}{\sum C_t} \qquad
\text{RVPI} = \frac{NAV_T}{\sum C_t} \qquad
\text{PIC} = \frac{\sum C_t}{\text{Commitment}} \qquad
\text{MOIC} = \frac{\sum D_t + NAV_T}{\sum \text{Calls}_t}
$$

### IRR (XIRR)

$r$ solves $\sum_t (D_t - C_t)(1+r)^{-\tau_t} + NAV_T (1+r)^{-\tau_T} = 0$ with $\tau_t = \frac{\text{days}(t_0, t)}{B}$,
$B \in \{365, 360, 365.25\}$. Newton-Raphson from the guess, then bisection over a scanned bracket when Newton
does not converge. `PeriodIrr(start, end)` uses $-NAV_{start}$ as the opening flow and $NAV_{end}$ as the closing one.

### Public market equivalents

Each flow is compounded to $T$ with the index: $FV(x_t) = x_t \, I_T / I_t$.

| Measure | Definition |
|---|---|
| **Kaplan-Schoar PME** | $\displaystyle \text{KS} = \frac{\sum FV(D_t) + NAV_T}{\sum FV(C_t)}$ — above 1, the fund beat the index |
| **Direct Alpha** | $\alpha$ such that $\sum_t FV(D_t - C_t)(1+\alpha)^{-\tau_t} + NAV_T(1+\alpha)^{-\tau_T} = 0$; also reported as $\ln(1+\alpha)$ |
| **Long-Nickels (LN-PME)** | IRR of $(-C_t, D_t)$ with terminal value $\sum FV(C_t) - \sum FV(D_t)$; flags when the shadow position turns negative |
| **PME+** | $\lambda = \dfrac{\sum FV(C_t) - NAV_T}{\sum FV(D_t)}$, IRR of $(-C_t, \lambda D_t, NAV_T)$ |
| **mPME** | The index investment receives $C_t$ and sells, at each distribution, the fraction $w_t = \dfrac{D_t}{D_t + NAV_t}$ of its value; IRR of $(-C_t, w_t V_t, V_T)$ |

`PmeResult` also exposes the spreads (fund IRR − benchmark IRR) for LN, PME+ and mPME.

### Aggregation

- **Pooled**: amounts summed across funds; pooled IRR on the combined cash flows (capital-weighted).
- **Quartiles**: linear interpolation (Excel `QUARTILE.INC`, NumPy default). `QuartileOf` follows the private
  equity convention: quartile 1 is the top quartile.

## Parity with Python

[`tests/parity/generate_parity.py`](tests/parity/generate_parity.py) builds seeded synthetic funds and index paths,
runs the Python libraries, and stores inputs and expected values as JSON. The xUnit suite reloads them and compares at 1e-6:

| Dataset | Reference |
|---|---|
| `xirr.json` | `xirr` 0.1.8 (Excel XIRR, ACT/365), `privateassets` 0.7.1 (ACT/365.25), SciPy (ACT/360) |
| `pme.json` | `privateassets` 0.7.1: KS-PME, Direct Alpha, Long-Nickels ratio and shadow NAV; SciPy reference for PME+ and LN IRR |
| `mpme.json` | `pypme` 0.7.0: mPME IRR and asset IRR |
| `vintage.json` | `privateassets` vintage statistics, NumPy quartiles, SciPy pooled IRR |

One deliberate difference: `privateassets` brackets Direct Alpha on [−50 %, 500 %] and returns NaN outside; this
library solves it anywhere above −100 % (the dataset includes a −65 % case, checked against SciPy).

Regenerate the data:

```bash
python -m venv .venv && .venv/Scripts/pip install -r tests/parity/requirements.txt
.venv/Scripts/python -I tests/parity/generate_parity.py
```

## MCP server

`PrivateMarkets.Metrics.Mcp` exposes `compute_fund_metrics`, `compute_pme`, `vintage_summary` and
`compute_j_curve` over stdio, for prompts such as *"compute the DPI of Fund III and compare it to its vintage median"*.

```bash
dotnet tool install --global PrivateMarkets.Metrics.Mcp
claude mcp add private-markets -- privatemarkets-mcp
```

Setup for Claude Desktop, VS Code and other clients, tool parameters and troubleshooting: [docs/mcp-server.md](docs/mcp-server.md).

## Build and test

```bash
dotnet build
dotnet test
dotnet run --project samples/PrivateMarkets.Metrics.Sample
```

## Out of scope (v1)

NAV de-smoothing, GPME, performance attribution, distribution waterfalls and index data sourcing.

## References

- Kaplan, S. and Schoar, A. (2005). *Private Equity Performance: Returns, Persistence, and Capital Flows*. Journal of Finance, 60(4).
- Gredil, O., Griffiths, B. and Stucke, R. (2014). *Benchmarking Private Equity: The Direct Alpha Method*. [directalphamethod.info](https://directalphamethod.info)
- Long, A. and Nickels, C. (1996). *A Private Investment Benchmark*. AIMR Conference on Venture Capital Investing.
- Rouvinez, C. (2003). *Private Equity Benchmarking with PME+*. Venture Capital Journal.
- Cambridge Associates (2013). *Private Equity and Venture Capital Benchmarks: An Introduction for Readers of Performance Reports* (mPME).
- Python implementations: [privateassets](https://github.com/ArturSepp/privateassets), [pypme](https://pypi.org/project/pypme/), [PyPME](https://github.com/Maween/PyPME).

## License

[MIT](LICENSE)
