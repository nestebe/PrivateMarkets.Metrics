# FAQ and troubleshooting

## Why is a ratio `null`?

Ratios are `null` instead of `0` or `NaN` when they cannot be computed:

| Property | `null` when |
|---|---|
| `Tvpi`, `Dpi`, `Rvpi` | nothing has been paid in at `asOf` |
| `Moic` | no capital call (fees only) at `asOf` |
| `Pic` | the commitment is 0 (unknown) |
| `Irr` | no rate solves the equation: all flows have the same sign (calls with a zero NAV), or every flow is on the same day |
| `KaplanSchoar` | no contribution |
| `PmePlusIrr`, `PmePlusScalingFactor` | no distribution yet (PME+ is undefined) |
| `LongNickelsRatio` | the shadow index position is zero or negative |
| `DirectAlpha` and other PME IRRs | no root, as for `Irr` |

## Why is `Unfunded` negative?

`Unfunded = Commitment − PaidIn + RecallableDistributed`. A negative value means the commitment is missing
(0 by default) or smaller than what was called. Pass the commitment when creating the `Fund`.

## Why is the TVPI exactly 1.00x before the first NAV?

Without a reported NAV, the residual value is rolled forward from zero with the calls and distributions, i.e. the
fund is valued at cost. Add the NAVs from the quarterly reports, or set `RollForwardNav = false` to value the
fund at zero until its first NAV.

## Why does my IRR differ from Excel?

With the same flows and dates, `Irr` equals Excel `XIRR` to better than 1e-8. Differences come from the inputs:

- **Terminal flow**: the library adds the residual value at `asOf`. In Excel, the NAV must be the last flow, dated at the valuation date.
- **NAV roll-forward**: if the last NAV is older than `asOf`, the library adjusts it with later flows. Excel uses whatever you typed.
- **Day count**: Excel uses 365 days. `Actual36525` or `Actual360` change the result slightly.
- **Signs**: Excel needs calls negative and distributions positive; the library derives the signs from the types.

## Why do my figures differ from the GP's report?

- **Gross vs net**: GPs often show gross IRR/MOIC (before fees and carry). The library computes LP net figures from the LP's cash flows.
- **Fees**: fees called outside capital calls must be recorded as `Fee`, otherwise paid-in is understated.
- **NAV date**: the report may use a NAV at a different date, or a NAV net/gross of accrued carry.
- **Recallable distributions**: some reports net them against later calls.
- **Subscription lines**: if the fund uses a credit facility, LP-level IRR (what the library computes from LP flows) is higher than the fund-level IRR from the deal dates.

## Why does Direct Alpha differ from the Python `privateassets` package?

It does not, except when the alpha is below −50 % or above 500 %: `privateassets` only searches that range and
returns NaN outside it, while this library searches the full range above −100 %. Everything else matches to 1e-6
(see [Parity with Python](../README.md#parity-with-python)).

## `ArgumentOutOfRangeException: The index '...' starts on ...`

The PME needs an index level on (or before) every cash flow date. Extend the index history back to the first
call, or check the date format of the CSV.

## `ArgumentException: All funds of a portfolio must share one currency`

Convert the funds to one currency first ([recipe](recipes.md#funds-in-several-currencies)). The library never
converts currencies implicitly.

## `FormatException` when loading an index CSV

The message gives the line number. Dates must be ISO (`yyyy-MM-dd`, `yyyy/MM/dd` or `yyyyMMdd`); day-first dates
such as `31/12/2024` are ambiguous and rejected. Numbers use `.` as decimal separator, or `,` when the file uses
`;` or tab as separator.

## Can a cash flow amount be negative?

No. Amounts are magnitudes; the direction comes from the type. Corrections of a previous call are recorded as a
distribution (or the original call is fixed in your data).

## Is it fast enough?

All calculations are linear in the number of cash flows (the IRR solver converges in a handful of Newton
iterations). Thousands of funds with hundreds of flows each are computed in well under a second. J-curves
recompute the metrics at each date; for very long monthly series, pass only the dates you need.

## Is it thread-safe?

Yes. Every type is immutable and every calculation is a pure function, so you can compute funds in parallel
(`Parallel.ForEach`, PLINQ).

## How are flows on the same date handled?

They are all included. For NAV purposes, a NAV dated the same day as a flow already includes it. For mPME, the
contributions and distributions of a date are netted per side before applying the distribution share.
