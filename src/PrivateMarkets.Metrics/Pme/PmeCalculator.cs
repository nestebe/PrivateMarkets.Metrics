using PrivateMarkets.Metrics.Internal;
using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Pme;

/// <summary>Computes the public market equivalent measures of a fund.</summary>
/// <remarks>
/// Contributions are capital calls plus fees; distributions include recallable ones. Flows of the same
/// date are netted per side, and every index level is looked up "as of" the flow date. T is the valuation date.
/// </remarks>
internal static class PmeCalculator
{
    public static PmeResult Compute(Fund fund, IndexSeries index, DateOnly asOf, MetricsOptions options)
    {
        var flows = DailyFlows(fund, asOf, index);
        var terminalLevel = index.LevelAt(asOf);
        var nav = FundLedger.ResidualValue(fund, asOf, options);

        var futureContributions = flows.Sum(f => f.Contribution * f.GrowthTo(terminalLevel));
        var futureDistributions = flows.Sum(f => f.Distribution * f.GrowthTo(terminalLevel));
        var directAlpha = DirectAlpha(flows, terminalLevel, nav, asOf, options.DayCount);
        var longNickels = LongNickels(flows, futureContributions, futureDistributions, asOf, options.DayCount);
        var pmePlus = PmePlus(flows, futureContributions, futureDistributions, nav, asOf, options.DayCount);
        var modified = ModifiedPme(fund, flows, terminalLevel, asOf, options);

        return new PmeResult
        {
            AsOf = asOf,
            IndexName = index.Name,
            ResidualValue = nav,
            FundIrr = Xirr.Calculate(FundLedger.FlowsWithTerminalValue(fund, asOf, nav), options.DayCount),
            KaplanSchoar = futureContributions == 0m ? null : (futureDistributions + nav) / futureContributions,
            DirectAlpha = directAlpha,
            DirectAlphaContinuous = directAlpha is { } alpha ? (decimal)Math.Log(1d + (double)alpha) : null,
            LongNickelsIrr = longNickels.Irr,
            LongNickelsTerminalValue = longNickels.ShadowValue,
            LongNickelsRatio = longNickels.ShadowValue > 0m ? nav / longNickels.ShadowValue : null,
            LongNickelsShadowNegative = longNickels.ShadowNegative,
            PmePlusIrr = pmePlus.Irr,
            PmePlusScalingFactor = pmePlus.Lambda,
            ModifiedPmeIrr = modified.Irr,
            ModifiedPmeTerminalValue = modified.TerminalValue,
        };
    }

    private static List<DailyFlow> DailyFlows(Fund fund, DateOnly asOf, IndexSeries index) =>
        FundLedger.FlowsUpTo(fund, asOf)
            .GroupBy(f => f.Date)
            .Select(g => new DailyFlow(
                g.Key,
                g.Where(f => f.Type.IsContribution()).Sum(f => f.Amount),
                g.Where(f => f.Type.IsDistribution()).Sum(f => f.Amount),
                index.LevelAt(g.Key)))
            .ToList();

    private static decimal? DirectAlpha(
        IEnumerable<DailyFlow> flows, decimal terminalLevel, decimal nav, DateOnly asOf, DayCountConvention dayCount)
    {
        var compounded = flows
            .Select(f => new DatedAmount(f.Date, f.Net * f.GrowthTo(terminalLevel)))
            .Append(new DatedAmount(asOf, nav));
        return Xirr.Calculate(compounded, dayCount);
    }

    private static (decimal? Irr, decimal ShadowValue, bool ShadowNegative) LongNickels(
        IReadOnlyList<DailyFlow> flows,
        decimal futureContributions,
        decimal futureDistributions,
        DateOnly asOf,
        DayCountConvention dayCount)
    {
        var units = 0m;
        var shadowNegative = false;
        foreach (var flow in flows)
        {
            units += (flow.Contribution - flow.Distribution) / flow.Level;
            shadowNegative |= units < 0m;
        }

        var shadowValue = futureContributions - futureDistributions;
        var irr = Xirr.Calculate(ActualFlows(flows).Append(new DatedAmount(asOf, shadowValue)), dayCount);
        return (irr, shadowValue, shadowNegative);
    }

    private static (decimal? Irr, decimal? Lambda) PmePlus(
        IReadOnlyList<DailyFlow> flows,
        decimal futureContributions,
        decimal futureDistributions,
        decimal nav,
        DateOnly asOf,
        DayCountConvention dayCount)
    {
        if (futureDistributions == 0m)
        {
            return (null, null);
        }

        var lambda = (futureContributions - nav) / futureDistributions;
        var scaled = flows
            .Select(f => new DatedAmount(f.Date, (lambda * f.Distribution) - f.Contribution))
            .Append(new DatedAmount(asOf, nav));
        return (Xirr.Calculate(scaled, dayCount), lambda);
    }

    /// <summary>
    /// Cambridge Associates mPME: the index investment sells at each distribution date the fraction
    /// D / (D + NAV after distribution) of its value, mirroring the fund.
    /// </summary>
    private static (decimal? Irr, decimal TerminalValue) ModifiedPme(
        Fund fund, IReadOnlyList<DailyFlow> flows, decimal terminalLevel, DateOnly asOf, MetricsOptions options)
    {
        var pmeFlows = new List<DatedAmount>(flows.Count + 1);
        var value = 0m;
        decimal? previousLevel = null;

        foreach (var flow in flows)
        {
            if (previousLevel is { } previous)
            {
                value *= flow.Level / previous;
            }

            value += flow.Contribution;
            var sale = 0m;
            if (flow.Distribution > 0m)
            {
                var navAfter = FundLedger.ResidualValue(fund, flow.Date, options);
                sale = value * flow.Distribution / (flow.Distribution + navAfter);
                value -= sale;
            }

            pmeFlows.Add(new DatedAmount(flow.Date, sale - flow.Contribution));
            previousLevel = flow.Level;
        }

        if (previousLevel is { } last)
        {
            value *= terminalLevel / last;
        }

        pmeFlows.Add(new DatedAmount(asOf, value));
        return (Xirr.Calculate(pmeFlows, options.DayCount), value);
    }

    private static IEnumerable<DatedAmount> ActualFlows(IEnumerable<DailyFlow> flows) =>
        flows.Select(f => new DatedAmount(f.Date, f.Net));

    /// <summary>Contributions and distributions of one date with the index level of that date.</summary>
    private readonly record struct DailyFlow(DateOnly Date, decimal Contribution, decimal Distribution, decimal Level)
    {
        public decimal Net => Distribution - Contribution;

        public decimal GrowthTo(decimal terminalLevel) => terminalLevel / Level;
    }
}
