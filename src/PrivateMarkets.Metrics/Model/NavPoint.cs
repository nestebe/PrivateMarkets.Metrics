namespace PrivateMarkets.Metrics;

/// <summary>
/// A reported net asset value (residual value) of the limited partner's interest in a fund.
/// The value is understood as end-of-day, after any cash flow dated the same day.
/// </summary>
public sealed record NavPoint
{
    /// <summary>Creates a NAV observation.</summary>
    /// <param name="date">Valuation date.</param>
    /// <param name="value">Non-negative net asset value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public NavPoint(DateOnly date, decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Date = date;
        Value = value;
    }

    /// <summary>Valuation date.</summary>
    public DateOnly Date { get; }

    /// <summary>Net asset value.</summary>
    public decimal Value { get; }
}
