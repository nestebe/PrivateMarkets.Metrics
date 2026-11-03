# Portfolios and vintages

## Build a portfolio

```csharp
using PrivateMarkets.Metrics.Aggregation;

var fundA = new Fund("Buyout A", 40_000_000m, "EUR", vintageYear: 2019)
    .Call(new DateOnly(2019, 4, 1), 30_000_000m)
    .Distribute(new DateOnly(2023, 6, 30), 25_000_000m)
    .Nav(new DateOnly(2026, 6, 30), 28_000_000m);
var fundB = new Fund("Growth B", 20_000_000m, "EUR", vintageYear: 2019)
    .Call(new DateOnly(2019, 9, 1), 18_000_000m)
    .Nav(new DateOnly(2026, 6, 30), 21_000_000m);
var fundC = new Fund("Venture C", 10_000_000m, "EUR", vintageYear: 2019)
    .Call(new DateOnly(2019, 11, 1), 9_000_000m)
    .Nav(new DateOnly(2026, 6, 30), 6_000_000m);

var program = new Portfolio("Programme 2019", [fundA, fundB, fundC]);
var asOf = new DateOnly(2026, 6, 30);
```

All funds must share one currency (`ArgumentException` otherwise). `program.Add(fund)` returns a new portfolio.

## Pooled metrics

```csharp
var pooled = program.Metrics(asOf);   // FundMetrics, as for a single fund
// Pooled TVPI 1.40x, pooled IRR 5.8 %
```

**Pooled** means the funds are merged into one: amounts are summed and the IRR is computed on the combined
cash flows. Large funds weigh more (capital-weighted). This is the figure to report for a programme or a
fund-of-funds.

## Fund-level metrics

```csharp
IReadOnlyList<FundPerformance> rows = program.FundPerformances(asOf);
// FundPerformance(FundName, VintageYear, Currency, Metrics)
```

## Vintage summaries

The **vintage year** is the explicit `vintageYear` passed to `Fund`, otherwise the year of the first
contribution. Funds without vintage (no contribution and none given) are left out of vintage summaries.

```csharp
var vintage = program.ByVintage(asOf).Single();

Console.WriteLine($"Vintage {vintage.VintageYear}: {vintage.FundCount} funds, " +
    $"TVPI median {vintage.Tvpi!.Median:F2}x (Q1 {vintage.Tvpi.UpperQuartile:F2}x / Q3 {vintage.Tvpi.LowerQuartile:F2}x), " +
    $"mean {vintage.Tvpi.Mean:F2}x");

foreach (var f in vintage.Funds)
{
    Console.WriteLine($"  {f.FundName,-10} TVPI {f.Metrics.Tvpi:F2}x  quartile {vintage.Tvpi.QuartileOf(f.Metrics.Tvpi!.Value)}");
}
```

```text
Vintage 2019: 3 funds, TVPI median 1.17x (Q1 1.47x / Q3 0.92x), mean 1.20x
  Buyout A   TVPI 1.77x  quartile 1
  Growth B   TVPI 1.17x  quartile 2
  Venture C  TVPI 0.67x  quartile 4
```

Each `VintageSummary` contains:

| Property | Content |
|---|---|
| `VintageYear`, `FundCount` | the group |
| `Pooled` | pooled `FundMetrics` of the vintage |
| `Tvpi`, `Dpi`, `Rvpi`, `Irr` | `QuartileStatistics` of the fund-level figures (`null` if no fund has the figure) |
| `Funds` | the fund-level `FundPerformance` rows |

## Quartiles

`QuartileStatistics` holds `Count`, `Minimum`, `LowerQuartile` (25th percentile), `Median`, `UpperQuartile`
(75th percentile), `Maximum` and `Mean` (equal-weighted). Percentiles use linear interpolation, the method of
Excel `QUARTILE.INC` and NumPy.

Private equity numbers quartiles **from the top**: quartile 1 is the best. `QuartileOf(value)` follows that
convention: 1 when `value >= UpperQuartile`, 2 when `>= Median`, 3 when `>= LowerQuartile`, 4 otherwise.

Compute statistics on any sample, for instance to rank a fund against an external peer group:

```csharp
var peers = QuartileStatistics.Compute(new[] { 1.21m, 1.45m, 1.62m, 1.80m, 2.35m })!;
int quartile = peers.QuartileOf(fundA.Metrics(asOf).Tvpi!.Value);
```

## Pooled vs average vs median

| Figure | Answers | Use it for |
|---|---|---|
| `Pooled.Tvpi` | how did the money do as a whole? | programme reporting, fund-of-funds |
| `Tvpi.Mean` | how did the average fund do? | equal-weighted comparisons |
| `Tvpi.Median` | how did the typical fund do? | peer benchmarking, quartile thresholds |
