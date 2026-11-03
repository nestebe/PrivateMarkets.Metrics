using System.Globalization;
using System.Text.Json;
using PrivateMarkets.Metrics.Pme;
using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Tests.Parity;

/// <summary>Loads the datasets produced by <c>tests/parity/generate_parity.py</c>.</summary>
internal static class ParityData
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Parity", fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Empty parity file {path}.");
    }

    public static DateOnly Date(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static Fund ToFund(string name, IEnumerable<ParityFlow> flows, IEnumerable<ParityNav> navs, int? vintageYear = null) =>
        new Fund(name, commitment: 0m, currency: "USD", vintageYear)
            .WithCashFlows(flows.Select(f => new CashFlow(Date(f.Date), f.Amount, Enum.Parse<CashFlowType>(f.Type!))))
            .WithNavPoints(navs.Select(n => new NavPoint(Date(n.Date), n.Value)));

    public static IndexSeries ToIndex(IEnumerable<ParityIndexPoint> points) =>
        new(points.Select(p => new IndexPoint(Date(p.Date), p.Level)), "parity-index");

    public static DayCountConvention DayCount(string name) => Enum.Parse<DayCountConvention>(name);
}

internal sealed record ParityFlow(string Date, decimal Amount, string? Type);

internal sealed record ParityNav(string Date, decimal Value);

internal sealed record ParityIndexPoint(string Date, decimal Level);
