using ModelContextProtocol;
using PrivateMarkets.Metrics.Mcp.Contracts;
using PrivateMarkets.Metrics.Mcp.Tools;

namespace PrivateMarkets.Metrics.Mcp.Tests;

public sealed class FundMetricsToolsTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    private static readonly FundInput FundThree = new(
        "Fonds III",
        [
            new CashFlowInput(new DateOnly(2021, 3, 15), 10_000_000m, "Call"),
            new CashFlowInput(new DateOnly(2022, 1, 10), 15_000_000m, "call"),
            new CashFlowInput(new DateOnly(2024, 6, 30), 8_000_000m, "Distribution"),
        ],
        [new NavInput(AsOf, 31_500_000m)],
        Commitment: 50_000_000m);

    [Fact]
    public void Fund_metrics_default_to_the_last_activity_date()
    {
        var metrics = FundMetricsTools.ComputeFundMetrics(FundThree);

        Assert.Equal(AsOf, metrics.AsOf);
        Assert.Equal(1.58m, metrics.Tvpi);
        Assert.Equal(0.5m, metrics.Pic);
    }

    [Fact]
    public void Pme_against_a_flat_index_equals_tvpi()
    {
        var pme = FundMetricsTools.ComputePme(FundThree, [new IndexPointInput(new DateOnly(2021, 1, 1), 100m)], "flat");

        Assert.Equal(1.58m, pme.KaplanSchoar);
        Assert.Equal("flat", pme.IndexName);
    }

    [Fact]
    public void Vintage_summary_groups_by_vintage()
    {
        var summary = FundMetricsTools.VintageSummary([FundThree, FundThree with { Name = "Fonds IV", VintageYear = 2023 }], AsOf);

        Assert.Equal([2021, 2023], summary.Select(s => s.VintageYear));
    }

    [Fact]
    public void J_curve_honours_the_frequency()
    {
        var curve = FundMetricsTools.ComputeJCurve(FundThree, "annual");

        Assert.Equal(new DateOnly(2021, 12, 31), curve[0].AsOf);
        Assert.Equal(AsOf, curve[^1].AsOf);
    }

    [Theory]
    [InlineData("Dividend", "Unknown cash flow type 'Dividend'")]
    [InlineData("42", "Unknown cash flow type '42'")]
    public void Invalid_inputs_become_mcp_errors_with_a_helpful_message(string type, string message)
    {
        var fund = FundThree with { CashFlows = [new CashFlowInput(AsOf, 1m, type)] };

        var error = Assert.Throws<McpException>(() => FundMetricsTools.ComputeFundMetrics(fund));

        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_day_count_is_reported()
    {
        var error = Assert.Throws<McpException>(() => FundMetricsTools.ComputeFundMetrics(FundThree, dayCount: "30/360"));

        Assert.Contains("Actual365Fixed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_fund_requires_an_explicit_date()
    {
        var empty = new FundInput("Empty", []);

        Assert.Throws<McpException>(() => FundMetricsTools.ComputeFundMetrics(empty));
        Assert.Null(FundMetricsTools.ComputeFundMetrics(empty, AsOf).Tvpi);
    }
}
