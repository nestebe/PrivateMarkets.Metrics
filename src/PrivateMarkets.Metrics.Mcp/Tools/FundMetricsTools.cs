using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Mcp.Contracts;
using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics.Mcp.Tools;

/// <summary>MCP tools computing private markets performance metrics.</summary>
[McpServerToolType]
public sealed class FundMetricsTools
{
    private const string DayCountDescription = "Day count for IRR-type measures: Actual365Fixed (Excel XIRR, default), Actual360 or Actual36525.";
    private const string RollForwardDescription = "Roll the last NAV forward with later calls and distributions (default true).";

    /// <summary>Multiples and since-inception IRR of a fund.</summary>
    [McpServerTool(Name = "compute_fund_metrics", ReadOnly = true, Idempotent = true)]
    [Description("Computes TVPI, DPI, RVPI, PIC, MOIC, paid-in, distributions, NAV, unfunded commitment and since-inception IRR of a fund at a date.")]
    public static FundMetrics ComputeFundMetrics(
        [Description("The fund position.")] FundInput fund,
        [Description("Valuation date (yyyy-MM-dd); defaults to the last cash flow or NAV date.")] DateOnly? asOf = null,
        [Description(DayCountDescription)] string dayCount = "Actual365Fixed",
        [Description(RollForwardDescription)] bool rollForwardNav = true) =>
        Invoke(() =>
        {
            var model = fund.ToFund();
            return model.Metrics(asOf ?? RequireActivity(model), InputMapper.ToOptions(dayCount, rollForwardNav));
        });

    /// <summary>Public market equivalent measures of a fund.</summary>
    [McpServerTool(Name = "compute_pme", ReadOnly = true, Idempotent = true)]
    [Description("Benchmarks a fund against a public index: Kaplan-Schoar PME, Direct Alpha, Long-Nickels, PME+ and modified PME (mPME). The index must start on or before the first cash flow; levels are looked up as of each date.")]
    public static PmeResult ComputePme(
        [Description("The fund position.")] FundInput fund,
        [Description("Total-return levels of the benchmark index.")] IReadOnlyList<IndexPointInput> index,
        [Description("Display name of the index, e.g. MSCI World NR.")] string? indexName = null,
        [Description("Valuation date (yyyy-MM-dd); defaults to the last cash flow or NAV date.")] DateOnly? asOf = null,
        [Description(DayCountDescription)] string dayCount = "Actual365Fixed",
        [Description(RollForwardDescription)] bool rollForwardNav = true) =>
        Invoke(() => fund.ToFund().Pme(index.ToIndex(indexName), asOf, InputMapper.ToOptions(dayCount, rollForwardNav)));

    /// <summary>Vintage-year aggregation of several funds.</summary>
    [McpServerTool(Name = "vintage_summary", ReadOnly = true, Idempotent = true)]
    [Description("Groups funds by vintage year and returns, per vintage, pooled TVPI/DPI/RVPI/IRR plus quartiles, median and mean of the fund-level TVPI, DPI, RVPI and IRR. All funds must share one currency.")]
    public static IReadOnlyList<VintageSummary> VintageSummary(
        [Description("Fund positions.")] IReadOnlyList<FundInput> funds,
        [Description("Valuation date (yyyy-MM-dd) applied to every fund.")] DateOnly asOf,
        [Description(DayCountDescription)] string dayCount = "Actual365Fixed",
        [Description(RollForwardDescription)] bool rollForwardNav = true) =>
        Invoke(() => new Portfolio("vintage_summary", funds.Select(f => f.ToFund()))
            .ByVintage(asOf, InputMapper.ToOptions(dayCount, rollForwardNav)));

    /// <summary>J-curve of a fund.</summary>
    [McpServerTool(Name = "compute_j_curve", ReadOnly = true, Idempotent = true)]
    [Description("Time series of the fund metrics (net cash flow, TVPI, DPI, IRR...) at each period end, from the first cash flow to the end date.")]
    public static IReadOnlyList<FundMetrics> ComputeJCurve(
        [Description("The fund position.")] FundInput fund,
        [Description("Monthly, Quarterly (default), SemiAnnual or Annual.")] string frequency = "Quarterly",
        [Description("Last date of the series (yyyy-MM-dd); defaults to the last cash flow or NAV date.")] DateOnly? end = null,
        [Description(DayCountDescription)] string dayCount = "Actual365Fixed",
        [Description(RollForwardDescription)] bool rollForwardNav = true) =>
        Invoke(() => fund.ToFund().JCurve(
            InputMapper.ParseEnum<ReportingFrequency>(frequency, "frequency"),
            end,
            InputMapper.ToOptions(dayCount, rollForwardNav)));

    private static DateOnly RequireActivity(Fund fund) =>
        fund.LastActivityDate ?? throw new ArgumentException($"Fund '{fund.Name}' has no cash flow nor NAV; provide asOf.");

    /// <summary>Turns input errors into MCP errors whose message reaches the client.</summary>
    private static T Invoke<T>(Func<T> compute)
    {
        try
        {
            return compute();
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new McpException(exception.Message, exception);
        }
    }
}
