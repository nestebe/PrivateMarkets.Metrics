# Multiples, IRR and J-curve

`fund.Metrics(asOf)` returns a `FundMetrics` snapshot. Notation below: $C$ contributions (calls + fees),
$D$ distributions, $NAV_T$ residual value at the valuation date $T$.

## Amounts

| Property | Meaning |
|---|---|
| `PaidIn` | calls + fees |
| `Invested` | calls only |
| `Fees` | capital called for fees and expenses |
| `Distributed` | all distributions, recallable included |
| `RecallableDistributed` | recallable part of `Distributed` |
| `ResidualValue` | NAV at `AsOf` (see [NAV roll-forward](concepts.md#nav-roll-forward)) |
| `NavDate` | date of the reported NAV used, `null` if none |
| `TotalValue` | `Distributed + ResidualValue` |
| `NetCashFlow` | `Distributed − PaidIn`: cumulative cash position of the LP |
| `Unfunded` | `Commitment − PaidIn + RecallableDistributed` |

## Multiples

| Property | Formula | How to read it |
|---|---|---|
| `Tvpi` | $\dfrac{\sum D + NAV_T}{\sum C}$ | total value created per unit paid in; 1.58x = 58 % gain, realised or not |
| `Dpi` | $\dfrac{\sum D}{\sum C}$ | cash actually returned; above 1.0x the LP got its money back |
| `Rvpi` | $\dfrac{NAV_T}{\sum C}$ | unrealised part; `Tvpi = Dpi + Rvpi` |
| `Pic` | $\dfrac{\sum C}{\text{Commitment}}$ | how much of the commitment has been drawn |
| `Moic` | $\dfrac{\sum D + NAV_T}{\sum \text{Calls}}$ | multiple on invested capital, fees excluded (closer to a gross multiple) |

All are `null` when the denominator is zero (no contribution yet, or a commitment of 0 for `Pic`).

## IRR

`Irr` is the since-inception internal rate of return: the annual rate $r$ that makes the net present value
of the LP cash flows, plus the residual value as a final inflow at $T$, equal to zero:

$$
\sum_t (D_t - C_t)(1+r)^{-\tau_t} + NAV_T (1+r)^{-\tau_T} = 0,
\qquad \tau_t = \frac{\text{days}(t_0, t)}{365}
$$

With the default `Actual365Fixed` convention this is exactly Excel's `XIRR`.

`Irr` is `null` when no rate solves the equation, typically when all flows have the same sign (only calls and a
zero NAV, or only distributions).

The solver is also available directly, for any series of signed amounts:

```csharp
using PrivateMarkets.Metrics.Returns;

DatedAmount[] flows =
[
    new(new DateOnly(2008, 1, 1), -10_000m),
    new(new DateOnly(2008, 3, 1), 2_750m),
    new(new DateOnly(2008, 10, 30), 4_250m),
    new(new DateOnly(2009, 2, 15), 3_250m),
    new(new DateOnly(2009, 4, 1), 2_750m),
];

decimal? irr = Xirr.Calculate(flows);                     // 0.373362533519 (Excel: 37.34 %)
decimal npv = Xirr.NetPresentValue(flows, rate: 0.08);    // Excel XNPV
bool found = Xirr.TryCalculate(flows, out double rate);   // raw double, no exception
```

## Period IRR

`PeriodIrr(start, end)` measures performance over a window: the residual value at `start` is treated as an
investment, the one at `end` as a sale, plus every cash flow in between. Typical uses: year-to-date,
last 12 months, 3- or 5-year horizon IRR.

```csharp
// Fonds III from Getting started, with its year-end NAV added
var withYearEnd = fund.Nav(new DateOnly(2025, 12, 31), 29_000_000m);
withYearEnd.PeriodIrr(new DateOnly(2025, 12, 31), new DateOnly(2026, 9, 30));   // 0.1169... (11.69 % YTD 2026)
```

`end` must be after `start`.

## J-curve

`JCurve` returns one `FundMetrics` per period end, from the first cash flow to the last activity date (or the
`end` you pass), which is always included as the last point.

```csharp
var fund = new Fund("Fonds VI", 100_000_000m, "EUR")
    .Call(new DateOnly(2021, 2, 1), 15_000_000m).Fee(new DateOnly(2021, 2, 1), 500_000m)
    .Nav(new DateOnly(2021, 12, 31), 13_000_000m)
    .Call(new DateOnly(2022, 3, 1), 25_000_000m).Fee(new DateOnly(2022, 3, 1), 1_000_000m)
    .Nav(new DateOnly(2022, 12, 31), 37_000_000m)
    .Call(new DateOnly(2023, 4, 1), 20_000_000m).Fee(new DateOnly(2023, 4, 1), 1_000_000m)
    .Nav(new DateOnly(2023, 12, 31), 66_000_000m)
    .Distribute(new DateOnly(2024, 9, 30), 12_000_000m)
    .Nav(new DateOnly(2024, 12, 31), 72_000_000m)
    .Distribute(new DateOnly(2025, 6, 30), 25_000_000m)
    .Nav(new DateOnly(2025, 12, 31), 63_000_000m);

foreach (var p in fund.JCurve(ReportingFrequency.Annual))
{
    Console.WriteLine($"{p.AsOf:yyyy-MM-dd}  {p.PaidIn / 1e6m:N1}  {p.Distributed / 1e6m:N1}  {p.ResidualValue / 1e6m:N1}  {p.NetCashFlow / 1e6m:N1}  {p.Tvpi:F2}x  {p.Irr:P1}");
}
```

| As of | Paid-in (m) | Distributed (m) | NAV (m) | Net cash flow (m) | TVPI | IRR |
|---|---:|---:|---:|---:|---:|---:|
| 2021-12-31 | 15.5 | 0.0 | 13.0 | −15.5 | 0.84x | −17.5 % |
| 2022-12-31 | 41.5 | 0.0 | 37.0 | −41.5 | 0.89x | −8.9 % |
| 2023-12-31 | 62.5 | 0.0 | 66.0 | −62.5 | 1.06x | 3.2 % |
| 2024-12-31 | 62.5 | 12.0 | 72.0 | −50.5 | 1.34x | 11.4 % |
| 2025-12-31 | 62.5 | 37.0 | 63.0 | −25.5 | 1.60x | 14.4 % |

The IRR dips while fees and early write-downs dominate, then climbs as value is created: the "J".
`NetCashFlow` draws the cash J-curve (the LP's cumulative cash position).

- Frequencies: `Monthly`, `Quarterly` (default), `SemiAnnual`, `Annual`. Period ends are calendar month ends.
- Custom dates: `fund.JCurve(new[] { date1, date2, ... })`.
- Feed it NAV history: with a single final NAV, intermediate points are valued at cost by the roll-forward and
  the curve is flat at 1.00x.
- Export the series with `CsvExport.ToCsv(curve)` to chart it in Excel ([Export](export.md)).
