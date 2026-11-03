using System.Globalization;
using PrivateMarkets.Metrics;
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Export;
using PrivateMarkets.Metrics.Pme;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var asOf = new DateOnly(2026, 9, 30);

var fund = new Fund("Fonds III", commitment: 50_000_000m, currency: "EUR")
    .Call(new DateOnly(2021, 3, 15), 10_000_000m)
    .Call(new DateOnly(2022, 1, 10), 15_000_000m)
    .Distribute(new DateOnly(2024, 6, 30), 8_000_000m)
    .Nav(asOf, 31_500_000m);

var m = fund.Metrics(asOf);
Console.WriteLine($"{fund.Name}: TVPI {m.Tvpi:F2}x  DPI {m.Dpi:F2}x  RVPI {m.Rvpi:F2}x  PIC {m.Pic:P0}  IRR {m.Irr:P1}");

// Synthetic total-return index shipped with the sample; it is not real market data.
var index = IndexSeries.FromCsv(Path.Combine(AppContext.BaseDirectory, "data", "sample-index.csv"));
var pme = fund.Pme(index, asOf);
Console.WriteLine(
    $"vs {pme.IndexName}: KS-PME {pme.KaplanSchoar:F2}  Direct Alpha {pme.DirectAlpha:P2}  " +
    $"PME+ {pme.PmePlusIrr:P1}  mPME {pme.ModifiedPmeIrr:P1}  LN-PME {pme.LongNickelsIrr:P1}");

var fundFour = new Fund("Fonds IV", 80_000_000m, "EUR")
    .Fee(new DateOnly(2022, 2, 1), 400_000m)
    .Call(new DateOnly(2022, 2, 1), 12_000_000m)
    .Call(new DateOnly(2023, 5, 15), 20_000_000m)
    .Nav(new DateOnly(2026, 6, 30), 36_000_000m)
    .Call(new DateOnly(2026, 8, 1), 5_000_000m);

var portfolio = new Portfolio("Programme", [fund, fundFour]);
var pooled = portfolio.Metrics(asOf);
Console.WriteLine($"Pooled: TVPI {pooled.Tvpi:F2}x  IRR {pooled.Irr:P1}  Unfunded {pooled.Unfunded:N0} {portfolio.Currency}");

foreach (var vintage in portfolio.ByVintage(asOf))
{
    Console.WriteLine($"Vintage {vintage.VintageYear}: {vintage.FundCount} fund(s), pooled TVPI {vintage.Pooled.Tvpi:F2}x, median IRR {vintage.Irr?.Median:P1}");
}

Console.WriteLine();
Console.WriteLine("J-curve (CSV):");
Console.Write(CsvExport.ToCsv(fund.JCurve(ReportingFrequency.Annual, asOf)));
