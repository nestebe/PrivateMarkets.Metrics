using System.Globalization;
using System.Text;
using PrivateMarkets.Metrics.Aggregation;

namespace PrivateMarkets.Metrics.Export;

/// <summary>
/// RFC 4180 CSV export of metrics (comma separator, invariant culture, ISO dates, empty cell for missing values).
/// </summary>
public static class CsvExport
{
    private static readonly string[] MetricsHeader =
    [
        "as_of", "commitment", "paid_in", "invested", "fees", "distributed", "recallable_distributed",
        "residual_value", "total_value", "net_cash_flow", "unfunded", "tvpi", "dpi", "rvpi", "pic", "moic", "irr",
    ];

    /// <summary>Exports a series of snapshots, typically a J-curve.</summary>
    public static string ToCsv(IEnumerable<FundMetrics> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var csv = new StringBuilder();
        AppendRow(csv, MetricsHeader);
        foreach (var metrics in series)
        {
            AppendRow(csv, MetricsCells(metrics));
        }

        return csv.ToString();
    }

    /// <summary>Exports fund-level metrics.</summary>
    public static string ToCsv(IEnumerable<FundPerformance> funds)
    {
        ArgumentNullException.ThrowIfNull(funds);
        var csv = new StringBuilder();
        AppendRow(csv, ["fund", "vintage_year", "currency", .. MetricsHeader]);
        foreach (var fund in funds)
        {
            AppendRow(csv, [fund.FundName, Format(fund.VintageYear), fund.Currency, .. MetricsCells(fund.Metrics)]);
        }

        return csv.ToString();
    }

    /// <summary>Exports vintage summaries: pooled figures, then quartiles of TVPI, DPI and IRR.</summary>
    public static string ToCsv(IEnumerable<VintageSummary> vintages)
    {
        ArgumentNullException.ThrowIfNull(vintages);
        var csv = new StringBuilder();
        AppendRow(csv,
        [
            "vintage_year", "fund_count", "paid_in", "distributed", "residual_value",
            "pooled_tvpi", "pooled_dpi", "pooled_rvpi", "pooled_irr",
            .. QuartileHeader("tvpi"), .. QuartileHeader("dpi"), .. QuartileHeader("irr"),
        ]);

        foreach (var vintage in vintages)
        {
            var pooled = vintage.Pooled;
            AppendRow(csv,
            [
                Format(vintage.VintageYear), Format(vintage.FundCount), Format(pooled.PaidIn), Format(pooled.Distributed),
                Format(pooled.ResidualValue), Format(pooled.Tvpi), Format(pooled.Dpi), Format(pooled.Rvpi), Format(pooled.Irr),
                .. QuartileCells(vintage.Tvpi), .. QuartileCells(vintage.Dpi), .. QuartileCells(vintage.Irr),
            ]);
        }

        return csv.ToString();
    }

    private static string[] MetricsCells(FundMetrics m) =>
    [
        m.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Format(m.Commitment), Format(m.PaidIn),
        Format(m.Invested), Format(m.Fees), Format(m.Distributed), Format(m.RecallableDistributed),
        Format(m.ResidualValue), Format(m.TotalValue), Format(m.NetCashFlow), Format(m.Unfunded),
        Format(m.Tvpi), Format(m.Dpi), Format(m.Rvpi), Format(m.Pic), Format(m.Moic), Format(m.Irr),
    ];

    private static string[] QuartileHeader(string metric) =>
        [$"{metric}_lower_quartile", $"{metric}_median", $"{metric}_upper_quartile", $"{metric}_mean"];

    private static string[] QuartileCells(QuartileStatistics? stats) =>
    [
        Format(stats?.LowerQuartile), Format(stats?.Median), Format(stats?.UpperQuartile), Format(stats?.Mean),
    ];

    private static string Format(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static void AppendRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.AppendJoin(',', cells.Select(Escape));
        csv.Append("\r\n");
    }

    private static string Escape(string cell) =>
        cell.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{cell.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : cell;
}
