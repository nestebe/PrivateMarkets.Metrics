using PrivateMarkets.Metrics.Internal;

namespace PrivateMarkets.Metrics.Tests.Model;

public sealed class JCurveTests
{
    private static readonly Fund Fund = new Fund("Fund", 100m)
        .Fee(new DateOnly(2020, 2, 10), 2m)
        .Call(new DateOnly(2020, 2, 10), 40m)
        .Nav(new DateOnly(2020, 6, 30), 35m)
        .Call(new DateOnly(2020, 11, 5), 30m)
        .Nav(new DateOnly(2020, 12, 31), 72m)
        .Distribute(new DateOnly(2021, 8, 20), 50m)
        .Nav(new DateOnly(2021, 12, 31), 60m);

    [Fact]
    public void Quarterly_curve_starts_at_the_first_quarter_end_and_stops_at_the_last_activity()
    {
        var curve = Fund.JCurve();

        Assert.Equal(8, curve.Count);
        Assert.Equal(new DateOnly(2020, 3, 31), curve[0].AsOf);
        Assert.Equal(new DateOnly(2021, 12, 31), curve[^1].AsOf);
    }

    [Fact]
    public void Net_cash_flow_dips_then_recovers()
    {
        var curve = Fund.JCurve(ReportingFrequency.SemiAnnual);

        Assert.Equal([-42m, -72m, -72m, -22m], curve.Select(p => p.NetCashFlow));
        Assert.Equal([35m / 42m, 1m, 1m, 110m / 72m], curve.Select(p => p.Tvpi!.Value));
    }

    [Fact]
    public void Custom_dates_are_sorted_and_deduplicated()
    {
        var curve = Fund.JCurve([new DateOnly(2021, 1, 1), new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)]);

        Assert.Equal([new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)], curve.Select(p => p.AsOf));
        Assert.Null(curve[0].Tvpi);
    }

    [Fact]
    public void Empty_fund_has_an_empty_curve()
    {
        Assert.Empty(new Fund("Empty", 1m).JCurve());
    }

    [Theory]
    [InlineData(ReportingFrequency.Monthly, "2020-02-15", "2020-05-10", new[] { "2020-02-29", "2020-03-31", "2020-04-30", "2020-05-10" })]
    [InlineData(ReportingFrequency.Quarterly, "2020-02-15", "2020-12-31", new[] { "2020-03-31", "2020-06-30", "2020-09-30", "2020-12-31" })]
    [InlineData(ReportingFrequency.SemiAnnual, "2020-08-01", "2021-07-01", new[] { "2020-12-31", "2021-06-30", "2021-07-01" })]
    [InlineData(ReportingFrequency.Annual, "2020-08-01", "2020-09-01", new[] { "2020-09-01" })]
    public void Period_ends_follow_the_calendar(ReportingFrequency frequency, string start, string end, string[] expected)
    {
        var dates = PeriodSchedule.PeriodEnds(Parse(start), Parse(end), frequency);

        Assert.Equal(expected.Select(Parse), dates);
    }

    [Fact]
    public void Period_ends_are_empty_for_a_reversed_range_and_reject_unknown_frequencies()
    {
        Assert.Empty(PeriodSchedule.PeriodEnds(Parse("2021-01-01"), Parse("2020-01-01"), ReportingFrequency.Quarterly));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PeriodSchedule.PeriodEnds(Parse("2020-01-01"), Parse("2021-01-01"), (ReportingFrequency)5).ToList());
    }

    private static DateOnly Parse(string date) => DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
}
