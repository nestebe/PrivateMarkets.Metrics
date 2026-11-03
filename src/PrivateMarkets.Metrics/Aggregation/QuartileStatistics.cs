namespace PrivateMarkets.Metrics.Aggregation;

/// <summary>
/// Distribution statistics of a sample. Quantiles use linear interpolation between order statistics
/// (Excel <c>QUARTILE.INC</c>, NumPy <c>percentile</c> default, Hyndman-Fan type 7).
/// </summary>
public sealed record QuartileStatistics
{
    /// <summary>Sample size.</summary>
    public required int Count { get; init; }

    /// <summary>Smallest value.</summary>
    public required decimal Minimum { get; init; }

    /// <summary>25th percentile, the threshold of the bottom quartile.</summary>
    public required decimal LowerQuartile { get; init; }

    /// <summary>50th percentile.</summary>
    public required decimal Median { get; init; }

    /// <summary>75th percentile, the threshold of the top quartile.</summary>
    public required decimal UpperQuartile { get; init; }

    /// <summary>Largest value.</summary>
    public required decimal Maximum { get; init; }

    /// <summary>Arithmetic (equal-weighted) mean.</summary>
    public required decimal Mean { get; init; }

    /// <summary>Computes the statistics of <paramref name="values"/>; <see langword="null"/> for an empty sample.</summary>
    public static QuartileStatistics? Compute(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        return new QuartileStatistics
        {
            Count = sorted.Length,
            Minimum = sorted[0],
            LowerQuartile = Percentile(sorted, 0.25m),
            Median = Percentile(sorted, 0.5m),
            UpperQuartile = Percentile(sorted, 0.75m),
            Maximum = sorted[^1],
            Mean = sorted.Average(),
        };
    }

    /// <summary>
    /// Private-equity quartile rank of <paramref name="value"/> in this sample: 1 is the top quartile
    /// (at or above <see cref="UpperQuartile"/>), 4 the bottom one (below <see cref="LowerQuartile"/>).
    /// </summary>
    public int QuartileOf(decimal value) =>
        value >= UpperQuartile ? 1 : value >= Median ? 2 : value >= LowerQuartile ? 3 : 4;

    /// <summary>Linear-interpolation percentile of an ascending sample.</summary>
    internal static decimal Percentile(IReadOnlyList<decimal> sorted, decimal probability)
    {
        var position = probability * (sorted.Count - 1);
        var lower = (int)decimal.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        var weight = position - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * weight);
    }
}
