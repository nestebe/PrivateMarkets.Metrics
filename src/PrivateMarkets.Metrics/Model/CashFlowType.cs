namespace PrivateMarkets.Metrics;

/// <summary>Nature of a cash flow between a limited partner and a fund.</summary>
public enum CashFlowType
{
    /// <summary>Capital call (drawdown) used to fund investments. Counts as paid-in and invested capital.</summary>
    Call,

    /// <summary>Distribution returned to the limited partner.</summary>
    Distribution,

    /// <summary>Capital called to pay management fees and fund expenses. Counts as paid-in but not as invested capital.</summary>
    Fee,

    /// <summary>Distribution that the general partner may call again; it is a distribution that also restores unfunded commitment.</summary>
    RecallableDistribution,
}

/// <summary>Classification helpers for <see cref="CashFlowType"/>.</summary>
public static class CashFlowTypeExtensions
{
    /// <summary>Whether the cash flow is money paid by the limited partner into the fund.</summary>
    public static bool IsContribution(this CashFlowType type) => type is CashFlowType.Call or CashFlowType.Fee;

    /// <summary>Whether the cash flow is money returned by the fund to the limited partner.</summary>
    public static bool IsDistribution(this CashFlowType type) =>
        type is CashFlowType.Distribution or CashFlowType.RecallableDistribution;
}
