using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Internal;

/// <summary>Computes <see cref="FundMetrics"/> for a single fund or a pool of funds.</summary>
internal static class FundMetricsCalculator
{
    public static FundMetrics Compute(Fund fund, DateOnly asOf, MetricsOptions options) => Pool([fund], asOf, options);

    /// <summary>
    /// Pools the funds as if they were one: amounts are summed and the IRR is computed on the combined
    /// cash flows with each fund's residual value as a terminal flow.
    /// </summary>
    public static FundMetrics Pool(IReadOnlyCollection<Fund> funds, DateOnly asOf, MetricsOptions options)
    {
        decimal invested = 0m, fees = 0m, distributed = 0m, recallable = 0m, residual = 0m;
        var irrFlows = new List<DatedAmount>();

        foreach (var fund in funds)
        {
            foreach (var flow in FundLedger.FlowsUpTo(fund, asOf))
            {
                switch (flow.Type)
                {
                    case CashFlowType.Call:
                        invested += flow.Amount;
                        break;
                    case CashFlowType.Fee:
                        fees += flow.Amount;
                        break;
                    case CashFlowType.RecallableDistribution:
                        recallable += flow.Amount;
                        distributed += flow.Amount;
                        break;
                    default:
                        distributed += flow.Amount;
                        break;
                }
            }

            var fundResidual = FundLedger.ResidualValue(fund, asOf, options);
            residual += fundResidual;
            irrFlows.AddRange(FundLedger.FlowsWithTerminalValue(fund, asOf, fundResidual));
        }

        return new FundMetrics
        {
            AsOf = asOf,
            Commitment = funds.Sum(f => f.Commitment),
            Invested = invested,
            Fees = fees,
            PaidIn = invested + fees,
            Distributed = distributed,
            RecallableDistributed = recallable,
            ResidualValue = residual,
            NavDate = funds.Count == 1 ? FundLedger.LastNav(funds.First(), asOf)?.Date : null,
            Irr = Xirr.Calculate(irrFlows, options.DayCount),
        };
    }

    /// <summary>IRR between two dates: the residual value at <paramref name="start"/> is an entry flow and the one at <paramref name="end"/> an exit flow.</summary>
    public static decimal? PeriodIrr(Fund fund, DateOnly start, DateOnly end, MetricsOptions options)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "The period end must be after its start.");
        }

        var flows = new List<DatedAmount>();
        var opening = FundLedger.ResidualValue(fund, start, options);
        if (opening > 0m)
        {
            flows.Add(new DatedAmount(start, -opening));
        }

        flows.AddRange(fund.CashFlows
            .Where(f => f.Date > start && f.Date <= end)
            .Select(f => new DatedAmount(f.Date, f.SignedAmount)));

        var closing = FundLedger.ResidualValue(fund, end, options);
        if (closing > 0m)
        {
            flows.Add(new DatedAmount(end, closing));
        }

        return Xirr.Calculate(flows, options.DayCount);
    }
}
