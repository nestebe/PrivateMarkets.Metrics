namespace PrivateMarkets.Metrics.Returns;

/// <summary>Day count conventions used to compute year fractions between two dates.</summary>
public enum DayCountConvention
{
    /// <summary>Actual days / 365. Matches Excel <c>XIRR</c>.</summary>
    Actual365Fixed,

    /// <summary>Actual days / 360 (money-market convention).</summary>
    Actual360,

    /// <summary>Actual days / 365.25 (average Julian year, used by several academic PME implementations).</summary>
    Actual36525,
}

/// <summary>Year fraction computations for <see cref="DayCountConvention"/>.</summary>
public static class DayCountConventionExtensions
{
    /// <summary>Returns the number of days in a year for the convention.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Unknown convention.</exception>
    public static double DaysPerYear(this DayCountConvention convention) => convention switch
    {
        DayCountConvention.Actual365Fixed => 365d,
        DayCountConvention.Actual360 => 360d,
        DayCountConvention.Actual36525 => 365.25d,
        _ => throw new ArgumentOutOfRangeException(nameof(convention), convention, "Unknown day count convention."),
    };

    /// <summary>Returns the signed year fraction from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static double YearFraction(this DayCountConvention convention, DateOnly start, DateOnly end) =>
        (end.DayNumber - start.DayNumber) / convention.DaysPerYear();
}
