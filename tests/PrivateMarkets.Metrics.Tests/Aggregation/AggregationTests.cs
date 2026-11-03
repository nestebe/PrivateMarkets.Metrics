using PrivateMarkets.Metrics.Aggregation;

namespace PrivateMarkets.Metrics.Tests.Aggregation;

public sealed class AggregationTests
{
    private static readonly DateOnly AsOf = new(2024, 12, 31);

    private static readonly Fund Alpha = new Fund("Alpha", 100m, "USD", vintageYear: 2019)
        .Call(new DateOnly(2019, 3, 1), 80m)
        .Distribute(new DateOnly(2022, 3, 1), 60m)
        .Nav(AsOf, 70m);

    private static readonly Fund Beta = new Fund("Beta", 50m, "USD", vintageYear: 2019)
        .Call(new DateOnly(2019, 9, 1), 40m)
        .Nav(AsOf, 30m);

    private static readonly Fund Gamma = new Fund("Gamma", 200m, "USD", vintageYear: 2021)
        .Call(new DateOnly(2021, 5, 1), 100m)
        .Nav(AsOf, 150m);

    private static readonly Portfolio Book = new("Book", [Alpha, Beta, Gamma]);

    [Fact]
    public void Pooled_metrics_are_capital_weighted()
    {
        var pooled = Book.Metrics(AsOf);

        Assert.Equal(350m, pooled.Commitment);
        Assert.Equal(220m, pooled.PaidIn);
        Assert.Equal(60m, pooled.Distributed);
        Assert.Equal(250m, pooled.ResidualValue);
        Assert.Equal(310m / 220m, pooled.Tvpi);
        Assert.Null(pooled.NavDate);
    }

    [Fact]
    public void Pooled_irr_is_the_irr_of_the_combined_cash_flows()
    {
        var combined = new Fund("Combined", 0m, "USD")
            .WithCashFlows(Book.Funds.SelectMany(f => f.CashFlows))
            .Nav(AsOf, 250m);

        Precision.Close((double)combined.Metrics(AsOf).Irr!.Value, Book.Metrics(AsOf).Irr, 1e-12);
    }

    [Fact]
    public void Vintage_summaries_group_funds_by_vintage_year()
    {
        var vintages = Book.ByVintage(AsOf);

        Assert.Equal([2019, 2021], vintages.Select(v => v.VintageYear));
        Assert.Equal(2, vintages[0].FundCount);
        Assert.Equal(160m / 120m, vintages[0].Pooled.Tvpi);
        Assert.Equal((130m / 80m + 30m / 40m) / 2m, vintages[0].Tvpi!.Mean);
        Assert.Equal(["Alpha", "Beta"], vintages[0].Funds.Select(f => f.FundName));
    }

    [Fact]
    public void Funds_without_vintage_are_left_out_of_vintage_summaries()
    {
        var portfolio = Book.Add(new Fund("Unfunded", 10m, "USD"));

        Assert.Equal(3, portfolio.ByVintage(AsOf).Sum(v => v.FundCount));
        Assert.Equal(4, portfolio.FundPerformances(AsOf).Count);
    }

    [Fact]
    public void Mixed_currencies_are_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => Book.Add(new Fund("Euro fund", 1m, "EUR")));
        Assert.Contains("EUR", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_portfolio_has_no_currency_and_no_ratios()
    {
        var empty = new Portfolio("Empty", []);

        Assert.Null(empty.Currency);
        Assert.Null(empty.Metrics(AsOf).Tvpi);
        Assert.Empty(empty.ByVintage(AsOf));
    }

    [Fact]
    public void Quartiles_use_linear_interpolation()
    {
        var stats = QuartileStatistics.Compute([1m, 2m, 3m, 4m, 10m])!;

        Assert.Equal(5, stats.Count);
        Assert.Equal(1m, stats.Minimum);
        Assert.Equal(2m, stats.LowerQuartile);
        Assert.Equal(3m, stats.Median);
        Assert.Equal(4m, stats.UpperQuartile);
        Assert.Equal(10m, stats.Maximum);
        Assert.Equal(4m, stats.Mean);
        Assert.Equal(1.75m, QuartileStatistics.Compute([1m, 2m, 3m, 4m])!.LowerQuartile);
        Assert.Equal(7m, QuartileStatistics.Compute([7m])!.UpperQuartile);
        Assert.Null(QuartileStatistics.Compute([]));
    }

    [Theory]
    [InlineData(4.5, 1)]
    [InlineData(4, 1)]
    [InlineData(3.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(1.5, 4)]
    public void Quartile_rank_puts_the_best_funds_in_the_first_quartile(double value, int expected)
    {
        var stats = QuartileStatistics.Compute([1m, 2m, 3m, 4m, 10m])!;

        Assert.Equal(expected, stats.QuartileOf((decimal)value));
    }
}
