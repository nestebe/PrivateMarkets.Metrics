namespace PrivateMarkets.Metrics.Tests.Model;

public sealed class FundTests
{
    private static readonly DateOnly Jan2021 = new(2021, 1, 15);
    private static readonly DateOnly Jun2021 = new(2021, 6, 30);

    [Fact]
    public void Fluent_methods_return_new_instances_and_leave_the_original_untouched()
    {
        var empty = new Fund("Fund I", 100m);

        var withCall = empty.Call(Jan2021, 10m);

        Assert.Empty(empty.CashFlows);
        Assert.Single(withCall.CashFlows);
        Assert.NotSame(empty, withCall);
    }

    [Fact]
    public void Cash_flows_are_sorted_by_date_whatever_the_insertion_order()
    {
        var fund = new Fund("Fund I", 100m)
            .Distribute(Jun2021, 5m)
            .Call(Jan2021, 10m);

        Assert.Equal([Jan2021, Jun2021], fund.CashFlows.Select(f => f.Date));
    }

    [Fact]
    public void A_nav_reported_twice_on_the_same_date_is_restated()
    {
        var fund = new Fund("Fund I", 100m).Nav(Jun2021, 10m).Nav(Jun2021, 12m);

        var nav = Assert.Single(fund.NavPoints);
        Assert.Equal(12m, nav.Value);
    }

    [Fact]
    public void Vintage_year_defaults_to_the_first_contribution_year()
    {
        var fund = new Fund("Fund I", 100m)
            .Distribute(new DateOnly(2019, 5, 1), 1m)
            .Fee(new DateOnly(2020, 3, 1), 1m)
            .Call(new DateOnly(2021, 3, 1), 10m);

        Assert.Equal(2020, fund.VintageYear);
        Assert.Equal(2018, new Fund("Fund II", 100m, vintageYear: 2018).Call(Jan2021, 1m).VintageYear);
        Assert.Null(new Fund("Empty", 100m).VintageYear);
    }

    [Fact]
    public void Currency_is_normalised()
    {
        Assert.Equal("USD", new Fund("Fund I", 1m, " usd ").Currency);
    }

    [Fact]
    public void Last_activity_date_covers_flows_and_navs()
    {
        var fund = new Fund("Fund I", 100m).Call(Jan2021, 10m).Nav(Jun2021, 9m);

        Assert.Equal(Jun2021, fund.LastActivityDate);
        Assert.Null(new Fund("Empty", 1m).LastActivityDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cash_flow_amounts_must_be_positive(int amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fund("Fund I", 100m).Call(Jan2021, amount));
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fund("Fund I", -1m));
        Assert.Throws<ArgumentException>(() => new Fund(" ", 1m));
        Assert.Throws<ArgumentException>(() => new Fund("Fund I", 1m, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavPoint(Jan2021, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CashFlow(Jan2021, 1m, (CashFlowType)42));
    }

    [Theory]
    [InlineData(CashFlowType.Call, -10)]
    [InlineData(CashFlowType.Fee, -10)]
    [InlineData(CashFlowType.Distribution, 10)]
    [InlineData(CashFlowType.RecallableDistribution, 10)]
    public void Signed_amount_follows_the_investor_perspective(CashFlowType type, int expected)
    {
        Assert.Equal(expected, new CashFlow(Jan2021, 10m, type).SignedAmount);
    }
}
