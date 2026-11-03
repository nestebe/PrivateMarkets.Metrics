using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics;

/// <summary>Conventions applied when computing fund metrics.</summary>
public sealed record MetricsOptions
{
    /// <summary>Default conventions: ACT/365 fixed and NAV roll-forward enabled.</summary>
    public static MetricsOptions Default { get; } = new();

    /// <summary>Day count convention used to turn dates into year fractions for IRR-type measures.</summary>
    public DayCountConvention DayCount { get; init; } = DayCountConvention.Actual365Fixed;

    /// <summary>
    /// When <see langword="true"/> (default), the residual value at a date is the last reported NAV
    /// plus capital calls minus distributions dated after that NAV. Fees do not increase the NAV.
    /// When <see langword="false"/>, the last reported NAV is used as is (zero if none).
    /// </summary>
    public bool RollForwardNav { get; init; } = true;
}
