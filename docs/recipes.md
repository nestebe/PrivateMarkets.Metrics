# Recipes

Short, copy-paste solutions. They assume:

```csharp
using System.Globalization;
using PrivateMarkets.Metrics;
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Export;
using PrivateMarkets.Metrics.Pme;
```

## Load cash flows from a CSV file

Most fund administration systems and Excel sheets can export one row per cash flow. With a file like:

```text
fund,date,type,amount
Fonds III,2021-03-15,Call,10000000
Fonds III,2022-01-10,Call,15000000
Fonds III,2024-06-30,Distribution,8000000
Fonds V,2023-05-02,Call,7000000
Fonds V,2023-05-02,Fee,150000
```

```csharp
var funds = File.ReadLines("cash-flows.csv")
    .Skip(1)                                         // header
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(line => line.Split(','))
    .GroupBy(cells => cells[0])
    .Select(rows => new Fund(rows.Key, commitment: 0m, currency: "EUR").WithCashFlows(rows.Select(cells => new CashFlow(
        DateOnly.ParseExact(cells[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal.Parse(cells[3], CultureInfo.InvariantCulture),
        Enum.Parse<CashFlowType>(cells[2], ignoreCase: true)))))
    .ToList();
// Fonds III: 3 flows, vintage 2021 | Fonds V: 2 flows, vintage 2023
```

- Signed exports (negative calls, positive distributions): map the sign to the type, e.g.
  `amount < 0 ? CashFlowType.Call : CashFlowType.Distribution`, and pass `Math.Abs(amount)`.
- Load NAVs the same way and add them with `fund.WithNavPoints(...)`, and pass the real commitment to
  `new Fund(name, commitment, ...)`: with a commitment of 0, `Pic` is `null` and `Unfunded` turns negative.
- For real-world files (quoted fields, other separators), use a CSV parser such as CsvHelper for the reading part.

## Quarterly LP report

```csharp
var quarterEnd = new DateOnly(2026, 9, 30);
var portfolio = new Portfolio("Programme", funds);

File.WriteAllText($"funds-{quarterEnd:yyyy-MM-dd}.csv", CsvExport.ToCsv(portfolio.FundPerformances(quarterEnd)));
File.WriteAllText($"vintages-{quarterEnd:yyyy-MM-dd}.csv", CsvExport.ToCsv(portfolio.ByVintage(quarterEnd)));

var total = portfolio.Metrics(quarterEnd);
Console.WriteLine($"Programme: TVPI {total.Tvpi:F2}x, DPI {total.Dpi:F2}x, IRR {total.Irr:P1}, unfunded {total.Unfunded:N0}");
```

## Check the figures reported by a GP

Recompute from the capital account and flag gaps above a tolerance:

```csharp
var computed = fund.Metrics(reportDate);
var gaps = new (string Metric, decimal? Reported, decimal? Computed, decimal Tolerance)[]
{
    ("TVPI", 1.58m, computed.Tvpi, 0.01m),
    ("DPI", 0.32m, computed.Dpi, 0.01m),
    ("IRR", 0.106m, computed.Irr, 0.002m),
}.Where(g => g.Reported is null || g.Computed is null || Math.Abs(g.Reported.Value - g.Computed.Value) > g.Tolerance);

foreach (var gap in gaps)
{
    Console.WriteLine($"{fund.Name}: {gap.Metric} reported {gap.Reported} vs computed {gap.Computed}");
}
```

Typical causes of gaps are listed in the [FAQ](faq.md#why-do-my-figures-differ-from-the-gps-report).

## Year-to-date, 1-year, 3-year IRR

Period IRRs need a NAV at the start of the period (or the roll-forward will value it at cost).

```csharp
var asOf = new DateOnly(2026, 9, 30);
var ytd = fund.PeriodIrr(new DateOnly(2025, 12, 31), asOf);
var oneYear = fund.PeriodIrr(asOf.AddYears(-1), asOf);
var threeYears = fund.PeriodIrr(asOf.AddYears(-3), asOf);
```

## Rank a fund within its vintage

```csharp
var vintage = portfolio.ByVintage(asOf).Single(v => v.VintageYear == fund.VintageYear);
var tvpi = fund.Metrics(asOf).Tvpi!.Value;
Console.WriteLine($"{fund.Name}: TVPI {tvpi:F2}x vs vintage median {vintage.Tvpi!.Median:F2}x, quartile {vintage.Tvpi.QuartileOf(tvpi)}");
```

Against an external benchmark (Preqin, Cambridge Associates, Burgiss...), build the statistics from the published
thresholds or peer values with `QuartileStatistics.Compute(values)`.

## Capital-weighted Direct Alpha of a programme

```csharp
var index = IndexSeries.FromCsv("msci-world-nr-eur.csv");
var weighted = funds
    .Select(f => (Alpha: f.Pme(index, asOf).DirectAlpha, PaidIn: f.Metrics(asOf).PaidIn))
    .Where(x => x.Alpha is not null)
    .ToList();
var programmeAlpha = weighted.Sum(x => x.Alpha!.Value * x.PaidIn) / weighted.Sum(x => x.PaidIn);
```

For a single pooled figure instead, merge the cash flows into one fund and compute its PME:

```csharp
var pooledFund = new Fund("Programme", 0m, "EUR")
    .WithCashFlows(funds.SelectMany(f => f.CashFlows))
    .Nav(asOf, funds.Sum(f => f.ResidualValueAt(asOf)));
var pooledPme = pooledFund.Pme(index, asOf);
```

## Match Excel XIRR

The default `Actual365Fixed` convention is Excel's. To reproduce an `XIRR` cell exactly, pass the same flows:
the residual value is the last (positive) flow, at the valuation date.

```csharp
using PrivateMarkets.Metrics.Returns;

var flows = fund.CashFlows
    .Where(f => f.Date <= asOf)
    .Select(f => new DatedAmount(f.Date, f.SignedAmount))
    .Append(new DatedAmount(asOf, fund.ResidualValueAt(asOf)));
var irr = Xirr.Calculate(flows);   // same as fund.Metrics(asOf).Irr
```

## Funds in several currencies

`Portfolio` refuses mixed currencies. Convert each flow with your FX policy, then rebuild the fund:

```csharp
Fund ToEur(Fund usdFund, Func<DateOnly, decimal> eurPerUsd) =>
    new Fund(usdFund.Name, usdFund.Commitment * eurPerUsd(usdFund.CashFlows[0].Date), "EUR", usdFund.VintageYear)
        .WithCashFlows(usdFund.CashFlows.Select(f => new CashFlow(f.Date, f.Amount * eurPerUsd(f.Date), f.Type)))
        .WithNavPoints(usdFund.NavPoints.Select(n => new NavPoint(n.Date, n.Value * eurPerUsd(n.Date))));
```
