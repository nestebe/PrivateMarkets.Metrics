# Getting started

## 1. Install

The library targets .NET 8 and .NET 10 and has no dependency.

```bash
dotnet add package PrivateMarkets.Metrics
```

**From source**, to use unreleased changes, either reference the project:

```bash
git clone https://github.com/nestebe/PrivateMarkets.Metrics.git
dotnet add reference PrivateMarkets.Metrics/src/PrivateMarkets.Metrics/PrivateMarkets.Metrics.csproj
```

or build a local package and install it from a folder:

```bash
dotnet pack PrivateMarkets.Metrics/src/PrivateMarkets.Metrics -c Release -o ./packages
dotnet add package PrivateMarkets.Metrics --source ./packages
```

## 2. Describe a fund

A `Fund` is the position of one limited partner (LP) in one fund: its commitment, the cash flows exchanged
with the fund and the NAVs reported by the general partner (GP). Amounts are always **positive**; the
method you call gives the direction.

```csharp
using PrivateMarkets.Metrics;

var fund = new Fund("Fonds III", commitment: 50_000_000m, currency: "EUR")
    .Call(new DateOnly(2021, 3, 15), 10_000_000m)          // capital call: LP pays the fund
    .Call(new DateOnly(2022, 1, 10), 15_000_000m)
    .Distribute(new DateOnly(2024, 6, 30), 8_000_000m)     // distribution: the fund pays the LP
    .Nav(new DateOnly(2026, 9, 30), 31_500_000m);          // NAV reported by the GP
```

Other flows: `.Fee(date, amount)` for capital called to pay management fees, and
`.Distribute(date, amount, recallable: true)` for recallable distributions. See [Core concepts](concepts.md).

`Fund` is immutable: each call returns a new `Fund`, so you can share instances safely between threads.

## 3. Compute the metrics

```csharp
var m = fund.Metrics(asOf: new DateOnly(2026, 9, 30));

Console.WriteLine($"Paid-in      {m.PaidIn:N0}");
Console.WriteLine($"Distributed  {m.Distributed:N0}");
Console.WriteLine($"NAV          {m.ResidualValue:N0}");
Console.WriteLine($"Unfunded     {m.Unfunded:N0}");
Console.WriteLine($"TVPI {m.Tvpi:F2}x  DPI {m.Dpi:F2}x  RVPI {m.Rvpi:F2}x  PIC {m.Pic:P0}  MOIC {m.Moic:F2}x");
Console.WriteLine($"IRR  {m.Irr:P2}");
```

```text
Paid-in      25,000,000
Distributed  8,000,000
NAV          31,500,000
Unfunded     25,000,000
TVPI 1.58x  DPI 0.32x  RVPI 1.26x  PIC 50 %  MOIC 1.58x
IRR  10.55 %
```

`asOf` is the valuation date: cash flows and NAVs after it are ignored, so the same `Fund` gives you the
metrics at any past reporting date.

Ratios are `decimal?`. They are `null` when they cannot be computed (no paid-in capital yet, no IRR root...)
rather than `0` or `NaN`, so a missing figure is never mistaken for a real one.

## 4. Benchmark against an index

```csharp
using PrivateMarkets.Metrics.Pme;

var index = IndexSeries.FromCsv("msci-world-nr.csv");   // two columns: date (yyyy-MM-dd), level
var pme = fund.Pme(index, asOf: new DateOnly(2026, 9, 30));

Console.WriteLine($"KS-PME {pme.KaplanSchoar:F3}  Direct Alpha {pme.DirectAlpha:P2}");
```

Use a **total-return** (net or gross dividends reinvested) index. Details and the choice of method are in
[Benchmarking with PME](pme.md).

## 5. Next steps

- Several funds: [Portfolios and vintages](portfolios-and-vintages.md)
- A J-curve chart: [Multiples, IRR and J-curve](metrics.md#j-curve)
- Load your cash flows from a CSV or Excel export: [Recipes](recipes.md#load-cash-flows-from-a-csv-file)
- Give an AI agent access to the calculations: [MCP server](mcp-server.md)
- A runnable console app: [samples/PrivateMarkets.Metrics.Sample](../samples/PrivateMarkets.Metrics.Sample/Program.cs)
  (`dotnet run --project samples/PrivateMarkets.Metrics.Sample`)
