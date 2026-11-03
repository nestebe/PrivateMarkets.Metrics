namespace PrivateMarkets.Metrics.Aggregation;

/// <summary>Metrics of one fund, labelled for reporting.</summary>
/// <param name="FundName">Fund name.</param>
/// <param name="VintageYear">Vintage year of the fund, if known.</param>
/// <param name="Currency">Currency of the amounts.</param>
/// <param name="Metrics">Metrics of the fund.</param>
public sealed record FundPerformance(string FundName, int? VintageYear, string Currency, FundMetrics Metrics);

/// <summary>Performance of the funds of one vintage year: pooled figures and the distribution of the fund-level figures.</summary>
public sealed record VintageSummary
{
    /// <summary>Vintage year.</summary>
    public required int VintageYear { get; init; }

    /// <summary>Number of funds of the vintage.</summary>
    public int FundCount => Funds.Count;

    /// <summary>Metrics of the vintage pooled as a single fund (capital-weighted; IRR on combined cash flows).</summary>
    public required FundMetrics Pooled { get; init; }

    /// <summary>Distribution of fund TVPIs.</summary>
    public QuartileStatistics? Tvpi { get; init; }

    /// <summary>Distribution of fund DPIs.</summary>
    public QuartileStatistics? Dpi { get; init; }

    /// <summary>Distribution of fund RVPIs.</summary>
    public QuartileStatistics? Rvpi { get; init; }

    /// <summary>Distribution of fund IRRs.</summary>
    public QuartileStatistics? Irr { get; init; }

    /// <summary>Fund-level metrics of the vintage.</summary>
    public required IReadOnlyList<FundPerformance> Funds { get; init; }
}
