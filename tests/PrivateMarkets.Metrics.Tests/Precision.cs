namespace PrivateMarkets.Metrics.Tests;

/// <summary>Numeric assertions with the parity tolerance of the project.</summary>
internal static class Precision
{
    /// <summary>Absolute tolerance required by the parity tests.</summary>
    public const double Parity = 1e-6;

    public static void Close(double? expected, decimal? actual, double tolerance = Parity, string? label = null)
    {
        if (expected is null)
        {
            Assert.True(actual is null, $"{label}: expected no value but got {actual}.");
            return;
        }

        Assert.True(actual is not null, $"{label}: expected {expected} but got no value.");
        var difference = Math.Abs(expected.Value - (double)actual!.Value);
        Assert.True(difference <= tolerance, $"{label}: expected {expected} but got {actual} (|Δ| = {difference:E2}).");
    }

    public static void Close(decimal? expected, decimal? actual, double tolerance = Parity, string? label = null) =>
        Close((double?)expected, actual, tolerance, label);

    /// <summary>Relative comparison for large amounts.</summary>
    public static void Relative(double expected, decimal actual, double tolerance = 1e-9, string? label = null)
    {
        var difference = Math.Abs(expected - (double)actual) / Math.Max(1d, Math.Abs(expected));
        Assert.True(difference <= tolerance, $"{label}: expected {expected} but got {actual} (relative Δ = {difference:E2}).");
    }
}
