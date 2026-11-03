namespace PrivateMarkets.Metrics;

/// <summary>
/// A dated cash flow between a limited partner and a fund. The amount is always a positive magnitude;
/// the direction is carried by <see cref="Type"/>.
/// </summary>
public sealed record CashFlow
{
    /// <summary>Creates a cash flow.</summary>
    /// <param name="date">Value date of the flow.</param>
    /// <param name="amount">Strictly positive amount, in the fund currency.</param>
    /// <param name="type">Nature of the flow.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is not strictly positive or the type is unknown.</exception>
    public CashFlow(DateOnly date, decimal amount, CashFlowType type)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown cash flow type.");
        }

        Date = date;
        Amount = amount;
        Type = type;
    }

    /// <summary>Value date of the flow.</summary>
    public DateOnly Date { get; }

    /// <summary>Positive magnitude of the flow.</summary>
    public decimal Amount { get; }

    /// <summary>Nature of the flow.</summary>
    public CashFlowType Type { get; }

    /// <summary>Amount seen from the limited partner: negative for contributions, positive for distributions.</summary>
    public decimal SignedAmount => Type.IsContribution() ? -Amount : Amount;
}
