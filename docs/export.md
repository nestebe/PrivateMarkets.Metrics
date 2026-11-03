# Export to CSV and JSON

```csharp
using PrivateMarkets.Metrics.Export;
```

## CSV

| Call | One row per | Columns |
|---|---|---|
| `CsvExport.ToCsv(IEnumerable<FundMetrics>)` | snapshot (J-curve point) | `as_of, commitment, paid_in, invested, fees, distributed, recallable_distributed, residual_value, total_value, net_cash_flow, unfunded, tvpi, dpi, rvpi, pic, moic, irr` |
| `CsvExport.ToCsv(IEnumerable<FundPerformance>)` | fund | `fund, vintage_year, currency` + the columns above |
| `CsvExport.ToCsv(IEnumerable<VintageSummary>)` | vintage | `vintage_year, fund_count, paid_in, distributed, residual_value, pooled_tvpi, pooled_dpi, pooled_rvpi, pooled_irr`, then `lower_quartile, median, upper_quartile, mean` for TVPI, DPI and IRR |

Format: comma separator, `.` decimal separator, ISO dates, CRLF line endings, RFC 4180 quoting, empty cell for a
missing value. Values are written with full `decimal` precision; round them in the consuming tool.

```csharp
File.WriteAllText("j-curve.csv", CsvExport.ToCsv(fund.JCurve()));
File.WriteAllText("funds.csv", CsvExport.ToCsv(portfolio.FundPerformances(asOf)));
File.WriteAllText("vintages.csv", CsvExport.ToCsv(portfolio.ByVintage(asOf)));
```

```text
fund,vintage_year,currency,as_of,commitment,paid_in,invested,fees,distributed,...,tvpi,dpi,rvpi,pic,moic,irr
Buyout A,2019,EUR,2026-06-30,40000000,30000000,30000000,0,25000000,...,1.7666666666666666666666666667,...,0.10453764439
```

**Opening in Excel with a French (or other `,`-decimal) locale:** use *Data > From Text/CSV* and choose
comma as delimiter and "English (United States)" as locale, instead of double-clicking the file.

## JSON

`JsonExport.ToJson(...)` accepts a `FundMetrics`, a series of `FundMetrics`, `FundPerformance` rows,
`VintageSummary` rows or a `PmeResult`. Property names are camelCase, dates ISO, numbers plain JSON numbers,
missing values `null`, and computed ratios are included:

```json
{
  "asOf": "2026-09-30",
  "commitment": 50000000,
  "paidIn": 25000000,
  "invested": 25000000,
  "fees": 0,
  "distributed": 8000000,
  "recallableDistributed": 0,
  "residualValue": 31500000,
  "navDate": "2026-09-30",
  "totalValue": 39500000,
  "netCashFlow": -17000000,
  "unfunded": 25000000,
  "tvpi": 1.58,
  "dpi": 0.32,
  "rvpi": 1.26,
  "pic": 0.5,
  "moic": 1.58,
  "irr": 0.105458950656
}
```

The serializer is source-generated: it works with trimming and Native AOT.

To use your own `JsonSerializerOptions`, serialize the result types directly with `System.Text.Json`; they are
plain records.
