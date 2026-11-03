using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Tests.Returns;

public sealed class XirrTests
{
    private static readonly DatedAmount[] ExcelExample =
    [
        new(new DateOnly(2008, 1, 1), -10_000m),
        new(new DateOnly(2008, 3, 1), 2_750m),
        new(new DateOnly(2008, 10, 30), 4_250m),
        new(new DateOnly(2009, 2, 15), 3_250m),
        new(new DateOnly(2009, 4, 1), 2_750m),
    ];

    [Fact]
    public void Matches_the_excel_documentation_example()
    {
        // Excel documents 0.373362535 (its own stopping tolerance); SciPy brentq to machine precision gives the value below.
        Precision.Close(0.373362535, Xirr.Calculate(ExcelExample), 1e-8);
        Precision.Close(0.37336253351883153, Xirr.Calculate(ExcelExample), 1e-12);
    }

    [Fact]
    public void One_year_round_trip_returns_the_simple_return()
    {
        DatedAmount[] flows = [new(new DateOnly(2021, 1, 1), -100m), new(new DateOnly(2022, 1, 1), 110m)];

        Precision.Close(0.10, Xirr.Calculate(flows), 1e-12);
    }

    [Theory]
    [InlineData(DayCountConvention.Actual365Fixed, 365d)]
    [InlineData(DayCountConvention.Actual360, 360d)]
    [InlineData(DayCountConvention.Actual36525, 365.25d)]
    public void Day_count_changes_the_year_fraction(DayCountConvention dayCount, double daysPerYear)
    {
        DatedAmount[] flows = [new(new DateOnly(2020, 1, 1), -100m), new(new DateOnly(2021, 1, 1), 110m)];
        var expected = Math.Pow(1.10, daysPerYear / 366d) - 1d; // 2020 is a leap year

        Precision.Close(expected, Xirr.Calculate(flows, dayCount), 1e-12);
    }

    [Fact]
    public void Order_of_the_flows_does_not_matter()
    {
        var shuffled = ExcelExample.OrderByDescending(f => f.Date);

        Assert.Equal(Xirr.Calculate(ExcelExample), Xirr.Calculate(shuffled));
    }

    [Fact]
    public void Npv_is_zero_at_the_irr()
    {
        Assert.True(Xirr.TryCalculate(ExcelExample, out var rate));

        Assert.True(Math.Abs(Xirr.NetPresentValue(ExcelExample, rate)) < 1e-6m);
    }

    [Fact]
    public void Total_loss_converges_close_to_minus_one_hundred_percent()
    {
        DatedAmount[] flows = [new(new DateOnly(2020, 1, 1), -1_000_000m), new(new DateOnly(2025, 1, 1), 1m)];

        var irr = Xirr.Calculate(flows);

        Precision.Close(Math.Pow(1e-6, 365d / 1827d) - 1d, irr, 1e-9);
    }

    [Fact]
    public void Very_high_returns_are_found_by_the_bisection_fallback()
    {
        DatedAmount[] flows = [new(new DateOnly(2024, 1, 1), -1m), new(new DateOnly(2024, 1, 31), 2m)];
        var expected = Math.Pow(2d, 365d / 30d) - 1d;

        var irr = Xirr.Calculate(flows, guess: -0.99);

        Precision.Close(expected, irr, tolerance: 1e-9 * expected);
    }

    [Theory]
    [MemberData(nameof(FlowsWithoutRoot))]
    public void Returns_null_when_no_rate_exists(DatedAmount[] flows)
    {
        Assert.Null(Xirr.Calculate(flows));
        Assert.False(Xirr.TryCalculate(flows, out var rate));
        Assert.True(double.IsNaN(rate));
    }

    public static TheoryData<DatedAmount[]> FlowsWithoutRoot() => new()
    {
        Array.Empty<DatedAmount>(),
        new DatedAmount[] { new(new DateOnly(2020, 1, 1), -100m), new(new DateOnly(2021, 1, 1), -50m) },
        new DatedAmount[] { new(new DateOnly(2020, 1, 1), 100m), new(new DateOnly(2021, 1, 1), 50m) },
        new DatedAmount[] { new(new DateOnly(2020, 1, 1), -100m), new(new DateOnly(2020, 1, 1), 120m) },
    };

    [Fact]
    public void Unknown_day_count_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((DayCountConvention)9).DaysPerYear());
    }

    [Fact]
    public void Npv_rejects_rates_at_or_below_minus_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Xirr.NetPresentValue(ExcelExample, -1d));
    }
}
