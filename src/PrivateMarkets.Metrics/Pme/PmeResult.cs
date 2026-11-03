namespace PrivateMarkets.Metrics.Pme;

/// <summary>
/// Public market equivalent measures of a fund against a benchmark index, all evaluated at <see cref="AsOf"/>.
/// Measures that cannot be computed (no contribution, no distribution, no IRR root...) are <see langword="null"/>.
/// </summary>
public sealed record PmeResult
{
    /// <summary>Valuation date (terminal date T of every measure).</summary>
    public required DateOnly AsOf { get; init; }

    /// <summary>Name of the benchmark index.</summary>
    public string? IndexName { get; init; }

    /// <summary>Residual value of the fund at <see cref="AsOf"/>.</summary>
    public required decimal ResidualValue { get; init; }

    /// <summary>Since-inception IRR of the fund.</summary>
    public decimal? FundIrr { get; init; }

    /// <summary>
    /// Kaplan-Schoar PME: (Σ FV(distributions) + NAV) / Σ FV(contributions), each flow being compounded to T
    /// with the index. Above 1 the fund outperformed the index.
    /// </summary>
    public decimal? KaplanSchoar { get; init; }

    /// <summary>
    /// Direct Alpha (Gredil, Griffiths and Stucke, 2014): IRR of the index-compounded cash flows, i.e. the
    /// annualised excess return over the index, in discrete compounding.
    /// </summary>
    public decimal? DirectAlpha { get; init; }

    /// <summary>Direct Alpha in continuous compounding: ln(1 + <see cref="DirectAlpha"/>).</summary>
    public decimal? DirectAlphaContinuous { get; init; }

    /// <summary>Long-Nickels PME: IRR of the fund contributions and distributions with the shadow index position as terminal value.</summary>
    public decimal? LongNickelsIrr { get; init; }

    /// <summary>Value at T of the shadow index position (Σ FV(contributions) − Σ FV(distributions)).</summary>
    public required decimal LongNickelsTerminalValue { get; init; }

    /// <summary>Fund NAV divided by the shadow position value; <see langword="null"/> when the shadow position is not positive.</summary>
    public decimal? LongNickelsRatio { get; init; }

    /// <summary>
    /// Whether the shadow index position went negative at some point, the known failure mode of Long-Nickels
    /// for funds that distribute faster than the index grows. <see cref="PmePlusIrr"/> or <see cref="ModifiedPmeIrr"/> are then preferable.
    /// </summary>
    public required bool LongNickelsShadowNegative { get; init; }

    /// <summary>PME+ (Rouvinez, 2003): IRR of the contributions, the distributions scaled by <see cref="PmePlusScalingFactor"/> and the fund NAV.</summary>
    public decimal? PmePlusIrr { get; init; }

    /// <summary>PME+ scaling factor λ = (Σ FV(contributions) − NAV) / Σ FV(distributions).</summary>
    public decimal? PmePlusScalingFactor { get; init; }

    /// <summary>
    /// Modified PME (mPME, Cambridge Associates): IRR of an index investment that receives the fund contributions
    /// and sells, at each distribution, the same fraction of its value as the fund distributes of its own.
    /// </summary>
    public decimal? ModifiedPmeIrr { get; init; }

    /// <summary>Value at T of the mPME index investment.</summary>
    public required decimal ModifiedPmeTerminalValue { get; init; }

    /// <summary>Fund IRR minus Long-Nickels IRR.</summary>
    public decimal? LongNickelsSpread => FundIrr - LongNickelsIrr;

    /// <summary>Fund IRR minus PME+ IRR.</summary>
    public decimal? PmePlusSpread => FundIrr - PmePlusIrr;

    /// <summary>Fund IRR minus mPME IRR.</summary>
    public decimal? ModifiedPmeSpread => FundIrr - ModifiedPmeIrr;
}
