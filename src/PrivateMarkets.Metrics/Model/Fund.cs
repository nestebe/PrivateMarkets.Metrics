using System.Collections.Immutable;
using PrivateMarkets.Metrics.Internal;
using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics;

/// <summary>
/// A limited partner's position in a fund: commitment, dated cash flows and reported NAVs.
/// Instances are immutable; the fluent methods (<see cref="Call"/>, <see cref="Distribute"/>, ...) return a new fund.
/// </summary>
public sealed class Fund
{
    private readonly int? _vintageYear;

    /// <summary>Creates a fund without any cash flow.</summary>
    /// <param name="name">Display name of the fund.</param>
    /// <param name="commitment">Committed capital (zero when unknown).</param>
    /// <param name="currency">ISO 4217 currency code of every amount of the fund.</param>
    /// <param name="vintageYear">Vintage year; defaults to the year of the first contribution.</param>
    /// <exception cref="ArgumentException">The name or currency is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The commitment is negative.</exception>
    public Fund(string name, decimal commitment, string currency = "EUR", int? vintageYear = null)
        : this(name, commitment, currency, vintageYear, [], [])
    {
    }

    private Fund(
        string name,
        decimal commitment,
        string currency,
        int? vintageYear,
        ImmutableArray<CashFlow> cashFlows,
        ImmutableArray<NavPoint> navPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(commitment);

        Name = name;
        Commitment = commitment;
        Currency = currency.Trim().ToUpperInvariant();
        _vintageYear = vintageYear;
        CashFlows = cashFlows;
        NavPoints = navPoints;
    }

    /// <summary>Display name of the fund.</summary>
    public string Name { get; }

    /// <summary>Committed capital.</summary>
    public decimal Commitment { get; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Vintage year: the explicit value, otherwise the year of the first contribution, otherwise <see langword="null"/>.</summary>
    public int? VintageYear =>
        _vintageYear ?? CashFlows.FirstOrDefault(f => f.Type.IsContribution())?.Date.Year;

    /// <summary>Cash flows sorted by date (insertion order is kept for flows on the same date).</summary>
    public IReadOnlyList<CashFlow> CashFlows { get; }

    /// <summary>Reported NAVs sorted by date, at most one per date.</summary>
    public IReadOnlyList<NavPoint> NavPoints { get; }

    /// <summary>Latest date carrying a cash flow or a NAV, or <see langword="null"/> for an empty fund.</summary>
    public DateOnly? LastActivityDate
    {
        get
        {
            DateOnly? lastFlow = CashFlows.Count > 0 ? CashFlows[^1].Date : null;
            DateOnly? lastNav = NavPoints.Count > 0 ? NavPoints[^1].Date : null;
            return Max(lastFlow, lastNav);
        }
    }

    /// <summary>Adds a capital call.</summary>
    public Fund Call(DateOnly date, decimal amount) => With(new CashFlow(date, amount, CashFlowType.Call));

    /// <summary>Adds a distribution.</summary>
    /// <param name="date">Value date.</param>
    /// <param name="amount">Distributed amount.</param>
    /// <param name="recallable">Whether the distribution can be recalled by the general partner.</param>
    public Fund Distribute(DateOnly date, decimal amount, bool recallable = false) =>
        With(new CashFlow(date, amount, recallable ? CashFlowType.RecallableDistribution : CashFlowType.Distribution));

    /// <summary>Adds capital called for management fees or expenses.</summary>
    public Fund Fee(DateOnly date, decimal amount) => With(new CashFlow(date, amount, CashFlowType.Fee));

    /// <summary>Adds a reported NAV. A NAV already reported on the same date is replaced (restatement).</summary>
    public Fund Nav(DateOnly date, decimal value) => WithNavPoints([new NavPoint(date, value)]);

    /// <summary>Adds a cash flow.</summary>
    public Fund With(CashFlow cashFlow)
    {
        ArgumentNullException.ThrowIfNull(cashFlow);
        return WithCashFlows([cashFlow]);
    }

    /// <summary>Adds several cash flows at once.</summary>
    public Fund WithCashFlows(IEnumerable<CashFlow> cashFlows)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);
        var merged = CashFlows.Concat(cashFlows).OrderBy(f => f.Date).ToImmutableArray();
        return new Fund(Name, Commitment, Currency, _vintageYear, merged, [.. NavPoints]);
    }

    /// <summary>Adds several reported NAVs at once; the last value supplied for a date wins.</summary>
    public Fund WithNavPoints(IEnumerable<NavPoint> navPoints)
    {
        ArgumentNullException.ThrowIfNull(navPoints);
        var merged = NavPoints.Concat(navPoints)
            .GroupBy(n => n.Date)
            .Select(g => g.Last())
            .OrderBy(n => n.Date)
            .ToImmutableArray();
        return new Fund(Name, Commitment, Currency, _vintageYear, [.. CashFlows], merged);
    }

    /// <summary>Computes the multiples and the since-inception IRR at <paramref name="asOf"/>.</summary>
    /// <param name="asOf">Valuation date; later cash flows and NAVs are ignored.</param>
    /// <param name="options">Calculation conventions; <see cref="MetricsOptions.Default"/> when omitted.</param>
    public FundMetrics Metrics(DateOnly asOf, MetricsOptions? options = null) =>
        FundMetricsCalculator.Compute(this, asOf, options ?? MetricsOptions.Default);

    /// <summary>Residual value at <paramref name="asOf"/> following the NAV conventions of <paramref name="options"/>.</summary>
    public decimal ResidualValueAt(DateOnly asOf, MetricsOptions? options = null) =>
        FundLedger.ResidualValue(this, asOf, options ?? MetricsOptions.Default);

    /// <summary>
    /// IRR over a period: the residual value at <paramref name="start"/> is treated as a contribution,
    /// the one at <paramref name="end"/> as a distribution, plus every cash flow in (<paramref name="start"/>, <paramref name="end"/>].
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="end"/> is not after <paramref name="start"/>.</exception>
    public decimal? PeriodIrr(DateOnly start, DateOnly end, MetricsOptions? options = null) =>
        FundMetricsCalculator.PeriodIrr(this, start, end, options ?? MetricsOptions.Default);

    /// <summary>
    /// J-curve: metrics at each period end of <paramref name="frequency"/>, from the first cash flow to
    /// <paramref name="end"/> (default: <see cref="LastActivityDate"/>), which is always included.
    /// </summary>
    public IReadOnlyList<FundMetrics> JCurve(
        ReportingFrequency frequency = ReportingFrequency.Quarterly,
        DateOnly? end = null,
        MetricsOptions? options = null)
    {
        var lastDate = end ?? LastActivityDate;
        if (CashFlows.Count == 0 || lastDate is null)
        {
            return [];
        }

        return JCurve(PeriodSchedule.PeriodEnds(CashFlows[0].Date, lastDate.Value, frequency), options);
    }

    /// <summary>J-curve evaluated at the given dates.</summary>
    public IReadOnlyList<FundMetrics> JCurve(IEnumerable<DateOnly> dates, MetricsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var effectiveOptions = options ?? MetricsOptions.Default;
        return dates.Distinct().Order().Select(d => Metrics(d, effectiveOptions)).ToList();
    }

    /// <summary>
    /// Public market equivalent measures against a benchmark index.
    /// </summary>
    /// <param name="index">Total-return index of the benchmark.</param>
    /// <param name="asOf">Valuation date; defaults to <see cref="LastActivityDate"/>.</param>
    /// <param name="options">Calculation conventions.</param>
    /// <exception cref="InvalidOperationException">The fund has no cash flow.</exception>
    public PmeResult Pme(IndexSeries index, DateOnly? asOf = null, MetricsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        var valuationDate = asOf ?? LastActivityDate
            ?? throw new InvalidOperationException($"Fund '{Name}' has no cash flow.");
        return PmeCalculator.Compute(this, index, valuationDate, options ?? MetricsOptions.Default);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Currency} {Commitment:N0})";

    private static DateOnly? Max(DateOnly? left, DateOnly? right) =>
        left is null ? right : right is null ? left : (left > right ? left : right);
}
