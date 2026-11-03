namespace PrivateMarkets.Metrics;

/// <summary>
/// Performance snapshot of a fund (or of a pool of funds) at a given date, seen from the limited partner.
/// Ratios are <see langword="null"/> when their denominator is zero.
/// </summary>
public sealed record FundMetrics
{
    /// <summary>Valuation date of the snapshot.</summary>
    public required DateOnly AsOf { get; init; }

    /// <summary>Total commitment.</summary>
    public required decimal Commitment { get; init; }

    /// <summary>Paid-in capital: capital calls plus fees.</summary>
    public required decimal PaidIn { get; init; }

    /// <summary>Invested capital: capital calls excluding fees.</summary>
    public required decimal Invested { get; init; }

    /// <summary>Capital called to pay fees and expenses.</summary>
    public required decimal Fees { get; init; }

    /// <summary>Total distributions, recallable ones included.</summary>
    public required decimal Distributed { get; init; }

    /// <summary>Recallable part of <see cref="Distributed"/>.</summary>
    public required decimal RecallableDistributed { get; init; }

    /// <summary>Residual value (NAV) at <see cref="AsOf"/>, rolled forward when the options say so.</summary>
    public required decimal ResidualValue { get; init; }

    /// <summary>Date of the reported NAV the residual value is based on, if any.</summary>
    public DateOnly? NavDate { get; init; }

    /// <summary>Distributions plus residual value.</summary>
    public decimal TotalValue => Distributed + ResidualValue;

    /// <summary>Distributions minus paid-in capital (cumulative net cash flow, the J-curve).</summary>
    public decimal NetCashFlow => Distributed - PaidIn;

    /// <summary>Commitment that can still be called: commitment − paid-in + recallable distributions.</summary>
    public decimal Unfunded => Commitment - PaidIn + RecallableDistributed;

    /// <summary>Total value to paid-in: (distributions + NAV) / paid-in.</summary>
    public decimal? Tvpi => Ratio(TotalValue, PaidIn);

    /// <summary>Distributions to paid-in (realisation multiple).</summary>
    public decimal? Dpi => Ratio(Distributed, PaidIn);

    /// <summary>Residual value to paid-in.</summary>
    public decimal? Rvpi => Ratio(ResidualValue, PaidIn);

    /// <summary>Paid-in to committed capital.</summary>
    public decimal? Pic => Ratio(PaidIn, Commitment);

    /// <summary>Multiple on invested capital: (distributions + NAV) / invested capital (fees excluded).</summary>
    public decimal? Moic => Ratio(TotalValue, Invested);

    /// <summary>Since-inception IRR, with the residual value as a terminal flow at <see cref="AsOf"/>.</summary>
    public decimal? Irr { get; init; }

    private static decimal? Ratio(decimal numerator, decimal denominator) =>
        denominator == 0m ? null : numerator / denominator;
}
