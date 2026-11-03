namespace PrivateMarkets.Metrics.Tests.Model;

public sealed class FundMetricsTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    /// <summary>The example of the README.</summary>
    private static readonly Fund FundThree = new Fund("Fonds III", commitment: 50_000_000m, currency: "EUR")
        .Call(new DateOnly(2021, 3, 15), 10_000_000m)
        .Call(new DateOnly(2022, 1, 10), 15_000_000m)
        .Distribute(new DateOnly(2024, 6, 30), 8_000_000m)
        .Nav(AsOf, 31_500_000m);

    [Fact]
    public void Multiples_of_the_readme_example()
    {
        var m = FundThree.Metrics(AsOf);

        Assert.Equal(25_000_000m, m.PaidIn);
        Assert.Equal(8_000_000m, m.Distributed);
        Assert.Equal(31_500_000m, m.ResidualValue);
        Assert.Equal(1.58m, m.Tvpi);
        Assert.Equal(0.32m, m.Dpi);
        Assert.Equal(1.26m, m.Rvpi);
        Assert.Equal(0.5m, m.Pic);
        Assert.Equal(25_000_000m, m.Unfunded);
        Assert.Equal(-17_000_000m, m.NetCashFlow);
        Assert.Equal(AsOf, m.NavDate);
    }

    [Fact]
    public void Irr_of_the_readme_example_matches_python()
    {
        // Same flows solved by the Python xirr package (Excel XIRR, ACT/365).
        Precision.Close(0.10545895065556511, FundThree.Metrics(AsOf).Irr);
    }

    [Fact]
    public void Fees_count_as_paid_in_but_not_as_invested_capital()
    {
        var fund = new Fund("Fund", 100m)
            .Call(new DateOnly(2020, 1, 1), 40m)
            .Fee(new DateOnly(2020, 1, 1), 10m)
            .Nav(new DateOnly(2021, 1, 1), 60m);

        var m = fund.Metrics(new DateOnly(2021, 1, 1));

        Assert.Equal(50m, m.PaidIn);
        Assert.Equal(40m, m.Invested);
        Assert.Equal(10m, m.Fees);
        Assert.Equal(1.2m, m.Tvpi);
        Assert.Equal(1.5m, m.Moic);
    }

    [Fact]
    public void Recallable_distributions_restore_unfunded_commitment()
    {
        var fund = new Fund("Fund", 100m)
            .Call(new DateOnly(2020, 1, 1), 60m)
            .Distribute(new DateOnly(2020, 6, 1), 15m, recallable: true)
            .Distribute(new DateOnly(2020, 9, 1), 5m);

        var m = fund.Metrics(new DateOnly(2020, 12, 31));

        Assert.Equal(20m, m.Distributed);
        Assert.Equal(15m, m.RecallableDistributed);
        Assert.Equal(55m, m.Unfunded);
    }

    [Fact]
    public void Flows_and_navs_after_the_valuation_date_are_ignored()
    {
        var m = FundThree.Metrics(new DateOnly(2023, 12, 31));

        Assert.Equal(25_000_000m, m.PaidIn);
        Assert.Equal(0m, m.Distributed);
        Assert.Null(m.NavDate);
    }

    [Fact]
    public void Ratios_are_null_before_any_contribution()
    {
        var m = new Fund("Empty", 0m).Metrics(AsOf);

        Assert.Null(m.Tvpi);
        Assert.Null(m.Dpi);
        Assert.Null(m.Rvpi);
        Assert.Null(m.Pic);
        Assert.Null(m.Moic);
        Assert.Null(m.Irr);
    }

    [Fact]
    public void Nav_is_rolled_forward_with_later_calls_and_distributions_but_not_fees()
    {
        var fund = new Fund("Fund", 100m)
            .Call(new DateOnly(2020, 1, 10), 50m)
            .Nav(new DateOnly(2020, 3, 31), 55m)
            .Call(new DateOnly(2020, 4, 15), 20m)
            .Fee(new DateOnly(2020, 4, 15), 2m)
            .Distribute(new DateOnly(2020, 5, 15), 8m);
        var asOf = new DateOnly(2020, 6, 30);

        Assert.Equal(67m, fund.ResidualValueAt(asOf));
        Assert.Equal(55m, fund.ResidualValueAt(asOf, new MetricsOptions { RollForwardNav = false }));
    }

    [Fact]
    public void Rolled_forward_nav_never_goes_negative()
    {
        var fund = new Fund("Fund", 100m)
            .Call(new DateOnly(2020, 1, 10), 50m)
            .Nav(new DateOnly(2020, 3, 31), 10m)
            .Distribute(new DateOnly(2020, 5, 15), 30m);

        Assert.Equal(0m, fund.ResidualValueAt(new DateOnly(2020, 6, 30)));
    }

    [Fact]
    public void Without_any_nav_the_roll_forward_starts_from_cost()
    {
        var fund = new Fund("Fund", 100m).Call(new DateOnly(2020, 1, 10), 50m);

        var m = fund.Metrics(new DateOnly(2020, 3, 31));

        Assert.Equal(50m, m.ResidualValue);
        Assert.Equal(1m, m.Tvpi);
    }

    [Fact]
    public void Period_irr_uses_opening_and_closing_nav()
    {
        var fund = new Fund("Fund", 100m)
            .Call(new DateOnly(2019, 6, 1), 80m)
            .Nav(new DateOnly(2019, 12, 31), 100m)
            .Nav(new DateOnly(2020, 12, 31), 112m);

        var irr = fund.PeriodIrr(new DateOnly(2019, 12, 31), new DateOnly(2020, 12, 31));

        Precision.Close(Math.Pow(1.12, 365d / 366d) - 1d, irr, 1e-12);
    }

    [Fact]
    public void Period_irr_requires_an_increasing_period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FundThree.PeriodIrr(AsOf, AsOf));
    }
}
