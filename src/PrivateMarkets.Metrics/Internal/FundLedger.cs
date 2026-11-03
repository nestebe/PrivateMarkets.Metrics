namespace PrivateMarkets.Metrics.Internal;

/// <summary>Point-in-time queries over the cash flows and NAVs of a fund.</summary>
internal static class FundLedger
{
    /// <summary>Cash flows dated on or before <paramref name="asOf"/>.</summary>
    public static IEnumerable<CashFlow> FlowsUpTo(Fund fund, DateOnly asOf) =>
        fund.CashFlows.TakeWhile(f => f.Date <= asOf);

    /// <summary>Last reported NAV dated on or before <paramref name="asOf"/>.</summary>
    public static NavPoint? LastNav(Fund fund, DateOnly asOf) =>
        fund.NavPoints.LastOrDefault(n => n.Date <= asOf);

    /// <summary>
    /// Residual value at <paramref name="asOf"/>: last reported NAV, optionally rolled forward with the
    /// calls and distributions dated after it. Never negative.
    /// </summary>
    public static decimal ResidualValue(Fund fund, DateOnly asOf, MetricsOptions options)
    {
        var nav = LastNav(fund, asOf);
        var value = nav?.Value ?? 0m;
        if (!options.RollForwardNav)
        {
            return value;
        }

        foreach (var flow in FlowsUpTo(fund, asOf))
        {
            if (nav is not null && flow.Date <= nav.Date)
            {
                continue;
            }

            value += flow.Type switch
            {
                CashFlowType.Call => flow.Amount,
                CashFlowType.Fee => 0m,
                _ => -flow.Amount,
            };
        }

        return Math.Max(value, 0m);
    }

    /// <summary>Signed investor cash flows up to <paramref name="asOf"/> followed by the residual value as a terminal flow.</summary>
    public static IEnumerable<DatedAmount> FlowsWithTerminalValue(Fund fund, DateOnly asOf, decimal residualValue)
    {
        foreach (var flow in FlowsUpTo(fund, asOf))
        {
            yield return new DatedAmount(flow.Date, flow.SignedAmount);
        }

        if (residualValue > 0m)
        {
            yield return new DatedAmount(asOf, residualValue);
        }
    }
}
