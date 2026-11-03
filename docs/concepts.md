# Core concepts

## Point of view

Every figure is computed from the **limited partner's** point of view: money the LP pays is a contribution,
money it receives is a distribution, and the NAV is the value of its remaining interest.

## Cash flow types

| `CashFlowType` | Fluent method | Counts in | Typical source |
|---|---|---|---|
| `Call` | `.Call(date, amount)` | paid-in **and** invested capital | capital call notice (investments) |
| `Fee` | `.Fee(date, amount)` | paid-in only | the fee/expense part of a capital call |
| `Distribution` | `.Distribute(date, amount)` | distributions | distribution notice |
| `RecallableDistribution` | `.Distribute(date, amount, recallable: true)` | distributions, and it restores unfunded commitment | recallable proceeds (bridge repayments, early exits) |

- Amounts must be strictly positive (`ArgumentOutOfRangeException` otherwise).
- `CashFlow.SignedAmount` gives the LP-signed value: negative for `Call` and `Fee`, positive for distributions.
- If your data does not split fees from investments, record everything as `Call`: TVPI, DPI, RVPI and IRR
  are unaffected; only `Invested`, `Fees` and `Moic` change.
- Equalisation interest, late-closing adjustments and similar items should be booked as calls or distributions
  according to their direction.

You can also build cash flows explicitly, for instance when loading them from a file:

```csharp
var flows = new[]
{
    new CashFlow(new DateOnly(2023, 5, 2), 7_000_000m, CashFlowType.Call),
    new CashFlow(new DateOnly(2023, 5, 2), 150_000m, CashFlowType.Fee),
};
var fund = new Fund("Fonds V", 30_000_000m, "EUR").WithCashFlows(flows);
```

## NAV

`NavPoint(date, value)` is a NAV reported for the LP's interest.

- A NAV is **end of day**: it already reflects the cash flows dated the same day.
- Reporting a NAV twice for the same date replaces the first one (restatement).
- A NAV can be zero (fully realised fund) but not negative.

### NAV roll-forward

GPs report NAVs quarterly, often with a delay, while cash flows keep happening. To value the fund at a date
that is not a NAV date, the library **rolls the last NAV forward**:

> residual value = last NAV on or before `asOf` + calls after that NAV − distributions after that NAV

Fees are not added (they pay expenses, they do not buy assets). The result is floored at zero. Before the
first NAV, the roll-forward starts from zero, which values the fund at cost.

```csharp
var fund = new Fund("Fonds IV", 80_000_000m)
    .Call(new DateOnly(2025, 2, 1), 20_000_000m)
    .Nav(new DateOnly(2025, 12, 31), 22_000_000m)
    .Call(new DateOnly(2026, 2, 15), 5_000_000m)
    .Fee(new DateOnly(2026, 2, 15), 300_000m)
    .Distribute(new DateOnly(2026, 3, 20), 1_000_000m);

var march = new DateOnly(2026, 3, 31);
fund.ResidualValueAt(march);                                                    // 26,000,000
fund.ResidualValueAt(march, new MetricsOptions { RollForwardNav = false });     // 22,000,000
```

`FundMetrics.NavDate` tells you which reported NAV the residual value is based on (`null` if none).

## Valuation date (`asOf`)

All calculations take a valuation date. Everything dated after it is ignored, so you can replay any
historical quarter-end from the same data. The residual value at `asOf` is used as a terminal cash flow for
the IRR and as the terminal value in PME calculations.

## Options

`MetricsOptions` gathers the conventions; pass it to any calculation, or omit it to use `MetricsOptions.Default`.

| Option | Default | Effect |
|---|---|---|
| `DayCount` | `Actual365Fixed` | year fractions for IRR-type measures: `Actual365Fixed` (Excel `XIRR`), `Actual360`, `Actual36525` (365.25 days, used by several academic papers) |
| `RollForwardNav` | `true` | see above |

```csharp
var academic = new MetricsOptions { DayCount = DayCountConvention.Actual36525 };
fund.Metrics(asOf, academic);
```

## Currency

Each `Fund` has an ISO currency code (default `EUR`). A `Portfolio` refuses funds in different currencies:
convert the cash flows before pooling, with the FX policy of your choice (spot at each flow, at reporting date...).

## Precision and types

- Amounts and ratios are `decimal`: TVPI of 1.58 is exactly `1.58m`, without binary rounding.
- IRR-type measures are solved in `double` (Newton-Raphson, bisection fallback) to a 1e-12 tolerance, then
  converted to `decimal` and rounded to 12 decimals.
- `null` means "not computable" (division by zero, no IRR root, missing data). It is never used for zero.

## Immutability and thread safety

`Fund`, `Portfolio`, `IndexSeries` and every result type are immutable. Methods such as `Call`, `Nav`,
`WithCashFlows` or `Portfolio.Add` return new instances. All calculations are pure functions of their inputs and
can run in parallel.
