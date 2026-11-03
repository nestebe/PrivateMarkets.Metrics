# Benchmarking with PME

A **public market equivalent** (PME) answers: *what if the LP had invested the same cash flows, on the same
dates, in a public index?* The library implements the five methods used in practice and returns them together
in a `PmeResult`.

## 1. Prepare the index

```csharp
using PrivateMarkets.Metrics.Pme;

var index = IndexSeries.FromCsv("msci-world-nr.csv");                    // from a file
var index2 = IndexSeries.ParseCsv(csvText, name: "MSCI World NR");      // from a string
var index3 = new IndexSeries(points, "S&P 500 TR");                     // from IndexPoint objects
```

- Use a **total-return** index (dividends reinvested), in the **currency of the fund**. A price index
  understates the benchmark by the dividend yield and makes every fund look better.
- CSV format: first column an ISO date (`yyyy-MM-dd`, `yyyy/MM/dd` or `yyyyMMdd`), second column the level.
  The separator (`,`, `;` or tab) is detected, an optional header line is skipped, quotes are removed and, with
  `;` or tab separators, a decimal comma is accepted (`1234,56`).
- Lookups are **as of**: the level used for a date is the last observation on or before it. Daily, weekly or
  monthly series all work; the closer the frequency to the cash flow dates, the more precise the result.
- The index must start on or before the first cash flow; otherwise an `ArgumentOutOfRangeException` names the
  missing date.

## 2. Compute

```csharp
var pme = fund.Pme(index, asOf: new DateOnly(2026, 9, 30));
```

`asOf` defaults to the fund's last cash flow or NAV date. `MetricsOptions` (day count, NAV roll-forward)
applies as for the other metrics.

With the fund of [Getting started](getting-started.md) and an illustrative index going from 1,000 to 1,480:

```csharp
Console.WriteLine($"Fund IRR       {pme.FundIrr:P2}");
Console.WriteLine($"KS-PME         {pme.KaplanSchoar:F3}");
Console.WriteLine($"Direct Alpha   {pme.DirectAlpha:P2}  (continuous {pme.DirectAlphaContinuous:P2})");
Console.WriteLine($"LN-PME IRR     {pme.LongNickelsIrr:P2}  spread {pme.LongNickelsSpread:P2}  shadow negative: {pme.LongNickelsShadowNegative}");
Console.WriteLine($"PME+ IRR       {pme.PmePlusIrr:P2}  spread {pme.PmePlusSpread:P2}  lambda {pme.PmePlusScalingFactor:F3}");
Console.WriteLine($"mPME IRR       {pme.ModifiedPmeIrr:P2}  spread {pme.ModifiedPmeSpread:P2}");
```

```text
Fund IRR       10.55 %
KS-PME         1.204
Direct Alpha   4.16 %  (continuous 4.08 %)
LN-PME IRR     6.08 %  spread 4.47 %  shadow negative: False
PME+ IRR       6.27 %  spread 4.28 %  lambda 0.273
mPME IRR       6.03 %  spread 4.51 %
```

Reading: the fund returned 10.55 % a year; the same cash flows invested in the index would have returned
about 6 %; the fund created about 4 points of excess return a year (Direct Alpha 4.16 %), and 20 % more wealth
than the index over its life (KS-PME 1.204).

## 3. The methods

Each flow is compounded to the valuation date $T$ with the index: $FV(x_t) = x_t \cdot I_T / I_t$.

| Property | Method | Output | Outperformance when |
|---|---|---|---|
| `KaplanSchoar` | Kaplan & Schoar (2005) | wealth ratio $\dfrac{\sum FV(D_t) + NAV_T}{\sum FV(C_t)}$ | > 1 |
| `DirectAlpha` | Gredil, Griffiths & Stucke (2014) | annual excess return: IRR of the compounded flows | > 0 |
| `DirectAlphaContinuous` | same | $\ln(1 + \alpha)$, continuous compounding | > 0 |
| `LongNickelsIrr` | Long & Nickels (1996) | IRR of the fund flows with the shadow index position as terminal value | fund IRR above it |
| `PmePlusIrr` | Rouvinez (2003) | IRR of calls, distributions scaled by λ, and the fund NAV | fund IRR above it |
| `ModifiedPmeIrr` | Cambridge Associates mPME (2013) | IRR of an index investment selling, at each distribution, the same share of its value as the fund | fund IRR above it |

Supporting values:

| Property | Meaning |
|---|---|
| `FundIrr` | fund IRR at `AsOf` (same as `Metrics(asOf).Irr`) |
| `LongNickelsTerminalValue` | value at $T$ of the shadow index position: $\sum FV(C_t) - \sum FV(D_t)$ |
| `LongNickelsRatio` | `ResidualValue / LongNickelsTerminalValue` (`null` if the shadow value is not positive) |
| `LongNickelsShadowNegative` | the shadow position went below zero at some point (see below) |
| `PmePlusScalingFactor` | $\lambda = \dfrac{\sum FV(C_t) - NAV_T}{\sum FV(D_t)}$ |
| `ModifiedPmeTerminalValue` | value at $T$ of the mPME index investment |
| `LongNickelsSpread`, `PmePlusSpread`, `ModifiedPmeSpread` | `FundIrr` minus the benchmark IRR |

## 4. Which one to use?

- **For a single number in a report:** Direct Alpha (an annual excess return, comparable across funds and
  vintages) together with KS-PME (a multiple, easy to explain). They are the academic standard.
- **To show the index IRR next to the fund IRR:** mPME or PME+. Both keep the benchmark NAV consistent with
  the fund NAV; mPME is the version most LPs see in Cambridge Associates reports.
- **Avoid Long-Nickels alone for funds that distribute fast.** When distributions outrun the index, the shadow
  position turns negative (`LongNickelsShadowNegative == true`) and its IRR becomes meaningless. The library
  still returns it, flagged, so you can see it and switch to PME+ or mPME.
- KS-PME and Direct Alpha only compound the actual cash flows. LN, PME+ and mPME each build a fictitious index
  position with a different rule (no adjustment, scaled distributions, proportional sales), which is why their
  IRRs differ by a few basis points.

## 5. Pitfalls

- **Young funds**: the result is driven by the NAV, which is an appraisal. Treat PMEs of funds with a low DPI
  as indicative.
- **NAV date vs valuation date**: if the last NAV is older than `asOf`, it is rolled forward with the cash flows
  but not with the market; the index, however, moves until `asOf`. Value at the NAV date for a like-for-like
  comparison.
- **Currency**: benchmark a EUR fund with a EUR index, or convert one of the two.
- **Fees**: fees are contributions. PMEs are therefore net of fees, like the fund IRR.

## References

- Kaplan, S. and Schoar, A. (2005). *Private Equity Performance: Returns, Persistence, and Capital Flows*. Journal of Finance, 60(4).
- Gredil, O., Griffiths, B. and Stucke, R. (2014). *Benchmarking Private Equity: The Direct Alpha Method*. [directalphamethod.info](https://directalphamethod.info)
- Long, A. and Nickels, C. (1996). *A Private Investment Benchmark*.
- Rouvinez, C. (2003). *Private Equity Benchmarking with PME+*. Venture Capital Journal.
- Cambridge Associates (2013). *Private Equity and Venture Capital Benchmarks: An Introduction for Readers of Performance Reports*.
