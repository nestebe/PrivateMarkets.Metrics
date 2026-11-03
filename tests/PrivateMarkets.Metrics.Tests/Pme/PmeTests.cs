using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics.Tests.Pme;

/// <summary>Analytical properties every PME measure must satisfy.</summary>
public sealed class PmeTests
{
    private static readonly DateOnly Start = new(2018, 3, 31);
    private static readonly DateOnly Exit = new(2021, 6, 30);
    private static readonly DateOnly AsOf = new(2024, 12, 31);

    [Fact]
    public void Against_a_flat_index_the_benchmarks_earn_zero_and_ks_pme_equals_tvpi()
    {
        var fund = new Fund("Fund", 100m)
            .Call(Start, 60m)
            .Fee(Start.AddMonths(6), 5m)
            .Call(Start.AddMonths(12), 30m)
            .Distribute(Exit, 50m)
            .Nav(AsOf, 90m);
        var flat = new IndexSeries([new IndexPoint(Start, 100m)], "flat");

        var pme = fund.Pme(flat, AsOf);
        var metrics = fund.Metrics(AsOf);

        Assert.Equal(metrics.Tvpi, pme.KaplanSchoar);
        Precision.Close(metrics.Irr, pme.DirectAlpha, 1e-12);
        Precision.Close(0d, pme.LongNickelsIrr, 1e-9);
        Precision.Close(0d, pme.PmePlusIrr, 1e-9);
        Precision.Close(0d, pme.ModifiedPmeIrr, 1e-9);
        Assert.Equal(metrics.Irr, pme.FundIrr);
    }

    [Fact]
    public void A_fund_that_tracks_the_index_has_no_alpha()
    {
        const double growth = 0.08;
        var index = new IndexSeries(Enumerable.Range(0, 8 * 365).Select(d => Start.AddDays(d))
            .Select(d => new IndexPoint(d, Compound(100d, growth, Start, d))));
        var fund = new Fund("Tracker", 100m)
            .Call(Start, 100m)
            .Distribute(Exit, Compound(50d, growth, Start, Exit))
            .Nav(Exit, Compound(50d, growth, Start, Exit))
            .Nav(AsOf, Compound(50d, growth, Start, AsOf));

        var pme = fund.Pme(index, AsOf);

        Precision.Close(1d, pme.KaplanSchoar, 1e-9);
        Precision.Close(0d, pme.DirectAlpha, 1e-9);
        Precision.Close(0d, pme.DirectAlphaContinuous, 1e-9);
        Precision.Close(growth, pme.FundIrr, 1e-9);
        Precision.Close(growth, pme.LongNickelsIrr, 1e-9);
        Precision.Close(growth, pme.PmePlusIrr, 1e-9);
        Precision.Close(growth, pme.ModifiedPmeIrr, 1e-9);
        Precision.Close(1d, pme.LongNickelsRatio, 1e-9);
        Precision.Close(1d, pme.PmePlusScalingFactor, 1e-9);
        Assert.False(pme.LongNickelsShadowNegative);
        Precision.Close(0d, pme.ModifiedPmeSpread, 1e-9);
    }

    [Fact]
    public void Continuous_direct_alpha_is_the_log_of_the_discrete_one()
    {
        var fund = new Fund("Fund", 100m).Call(Start, 100m).Nav(AsOf, 180m);
        var index = new IndexSeries([new IndexPoint(Start, 100m), new IndexPoint(AsOf, 120m)]);

        var pme = fund.Pme(index, AsOf);

        Precision.Close(Math.Log(1d + (double)pme.DirectAlpha!.Value), pme.DirectAlphaContinuous, 1e-12);
        Precision.Close(1.5d, pme.KaplanSchoar, 1e-12);
    }

    [Fact]
    public void Pme_plus_is_undefined_without_distributions()
    {
        var fund = new Fund("Fund", 100m).Call(Start, 100m).Nav(AsOf, 120m);

        var pme = fund.Pme(new IndexSeries([new IndexPoint(Start, 100m)]), AsOf);

        Assert.Null(pme.PmePlusIrr);
        Assert.Null(pme.PmePlusScalingFactor);
    }

    [Fact]
    public void Defaults_to_the_last_activity_date()
    {
        var fund = new Fund("Fund", 100m).Call(Start, 100m).Nav(AsOf, 120m);

        Assert.Equal(AsOf, fund.Pme(new IndexSeries([new IndexPoint(Start, 100m)])).AsOf);
    }

    [Fact]
    public void Index_must_cover_the_first_cash_flow()
    {
        var fund = new Fund("Fund", 100m).Call(Start, 100m).Nav(AsOf, 120m);
        var lateIndex = new IndexSeries([new IndexPoint(Start.AddDays(1), 100m)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => fund.Pme(lateIndex, AsOf));
    }

    [Fact]
    public void Empty_fund_has_no_valuation_date()
    {
        Assert.Throws<InvalidOperationException>(() => new Fund("Empty", 1m).Pme(new IndexSeries([new IndexPoint(Start, 1m)])));
    }

    private static decimal Compound(double amount, double rate, DateOnly from, DateOnly to) =>
        (decimal)(amount * Math.Pow(1d + rate, (to.DayNumber - from.DayNumber) / 365d));
}
