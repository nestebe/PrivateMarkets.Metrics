using System.Collections.Immutable;
using PrivateMarkets.Metrics.Internal;

namespace PrivateMarkets.Metrics.Aggregation;

/// <summary>
/// A set of fund positions sharing one currency. Pooled metrics treat the funds as a single fund:
/// amounts are summed and the pooled IRR is computed on the combined cash flows.
/// </summary>
public sealed class Portfolio
{
    private readonly ImmutableArray<Fund> _funds;

    /// <summary>Creates a portfolio.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="funds">Fund positions, all in the same currency.</param>
    /// <exception cref="ArgumentException">The funds are not all in the same currency.</exception>
    public Portfolio(string name, IEnumerable<Fund> funds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(funds);

        _funds = [.. funds];
        var currencies = _funds.Select(f => f.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            throw new ArgumentException(
                $"All funds of a portfolio must share one currency; found {string.Join(", ", currencies)}. Convert the cash flows first.",
                nameof(funds));
        }

        Name = name;
        Currency = currencies.SingleOrDefault();
    }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Common currency of the funds, <see langword="null"/> for an empty portfolio.</summary>
    public string? Currency { get; }

    /// <summary>Fund positions.</summary>
    public IReadOnlyList<Fund> Funds => _funds;

    /// <summary>Returns a portfolio that also holds <paramref name="fund"/>.</summary>
    public Portfolio Add(Fund fund)
    {
        ArgumentNullException.ThrowIfNull(fund);
        return new Portfolio(Name, _funds.Add(fund));
    }

    /// <summary>Pooled metrics of every fund at <paramref name="asOf"/>.</summary>
    public FundMetrics Metrics(DateOnly asOf, MetricsOptions? options = null) =>
        FundMetricsCalculator.Pool(_funds, asOf, options ?? MetricsOptions.Default);

    /// <summary>Fund-level metrics at <paramref name="asOf"/>.</summary>
    public IReadOnlyList<FundPerformance> FundPerformances(DateOnly asOf, MetricsOptions? options = null)
    {
        var effectiveOptions = options ?? MetricsOptions.Default;
        return _funds
            .Select(f => new FundPerformance(f.Name, f.VintageYear, f.Currency, f.Metrics(asOf, effectiveOptions)))
            .ToList();
    }

    /// <summary>
    /// Vintage-year summaries at <paramref name="asOf"/>, ordered by vintage. Funds without a vintage year
    /// (no explicit vintage and no contribution) are left out.
    /// </summary>
    public IReadOnlyList<VintageSummary> ByVintage(DateOnly asOf, MetricsOptions? options = null)
    {
        var effectiveOptions = options ?? MetricsOptions.Default;
        return _funds
            .Where(f => f.VintageYear is not null)
            .GroupBy(f => f.VintageYear!.Value)
            .OrderBy(g => g.Key)
            .Select(g => Summarize(g.Key, [.. g], asOf, effectiveOptions))
            .ToList();
    }

    private static VintageSummary Summarize(int vintageYear, IReadOnlyCollection<Fund> funds, DateOnly asOf, MetricsOptions options)
    {
        var performances = funds
            .Select(f => new FundPerformance(f.Name, vintageYear, f.Currency, f.Metrics(asOf, options)))
            .ToList();

        return new VintageSummary
        {
            VintageYear = vintageYear,
            Pooled = FundMetricsCalculator.Pool(funds, asOf, options),
            Tvpi = Statistics(performances, m => m.Tvpi),
            Dpi = Statistics(performances, m => m.Dpi),
            Rvpi = Statistics(performances, m => m.Rvpi),
            Irr = Statistics(performances, m => m.Irr),
            Funds = performances,
        };
    }

    private static QuartileStatistics? Statistics(IEnumerable<FundPerformance> performances, Func<FundMetrics, decimal?> selector) =>
        QuartileStatistics.Compute(performances.Select(p => selector(p.Metrics)).OfType<decimal>());
}
