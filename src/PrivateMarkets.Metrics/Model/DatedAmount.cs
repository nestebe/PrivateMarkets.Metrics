namespace PrivateMarkets.Metrics;

/// <summary>A signed amount at a date, as used by return calculations (negative = outflow for the investor).</summary>
/// <param name="Date">Value date.</param>
/// <param name="Amount">Signed amount.</param>
public readonly record struct DatedAmount(DateOnly Date, decimal Amount);
