using PrivateMarkets.Metrics.Pme;
using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Mcp.Contracts;

/// <summary>Converts MCP inputs into library types, with error messages meant for an agent.</summary>
internal static class InputMapper
{
    public static Fund ToFund(this FundInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new Fund(input.Name, input.Commitment, input.Currency, input.VintageYear)
            .WithCashFlows((input.CashFlows ?? []).Select(f => new CashFlow(f.Date, f.Amount, ParseEnum<CashFlowType>(f.Type, "cash flow type"))))
            .WithNavPoints((input.Navs ?? []).Select(n => new NavPoint(n.Date, n.Value)));
    }

    public static IndexSeries ToIndex(this IReadOnlyList<IndexPointInput> points, string? name) =>
        new(points.Select(p => new IndexPoint(p.Date, p.Level)), name);

    public static MetricsOptions ToOptions(string dayCount, bool rollForwardNav) => new()
    {
        DayCount = ParseEnum<DayCountConvention>(dayCount, "day count convention"),
        RollForwardNav = rollForwardNav,
    };

    public static TEnum ParseEnum<TEnum>(string value, string label)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException($"Unknown {label} '{value}'. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
}
