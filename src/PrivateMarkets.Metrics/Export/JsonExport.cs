using System.Text.Json;
using System.Text.Json.Serialization;
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics.Export;

/// <summary>
/// JSON export of metrics (camelCase, ISO dates, decimals as numbers). Uses source-generated
/// serialization, so it is trimming and Native AOT friendly.
/// </summary>
public static class JsonExport
{
    /// <summary>Serializes a snapshot.</summary>
    public static string ToJson(FundMetrics metrics) =>
        JsonSerializer.Serialize(metrics, MetricsJsonContext.Default.FundMetrics);

    /// <summary>Serializes a series of snapshots, typically a J-curve.</summary>
    public static string ToJson(IEnumerable<FundMetrics> series) =>
        JsonSerializer.Serialize(Materialize(series), MetricsJsonContext.Default.IReadOnlyListFundMetrics);

    /// <summary>Serializes fund-level metrics.</summary>
    public static string ToJson(IEnumerable<FundPerformance> funds) =>
        JsonSerializer.Serialize(Materialize(funds), MetricsJsonContext.Default.IReadOnlyListFundPerformance);

    /// <summary>Serializes vintage summaries.</summary>
    public static string ToJson(IEnumerable<VintageSummary> vintages) =>
        JsonSerializer.Serialize(Materialize(vintages), MetricsJsonContext.Default.IReadOnlyListVintageSummary);

    /// <summary>Serializes PME measures.</summary>
    public static string ToJson(PmeResult pme) =>
        JsonSerializer.Serialize(pme, MetricsJsonContext.Default.PmeResult);

    private static List<T> Materialize<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items];
    }
}

/// <summary>Source-generated serialization metadata for the exported types.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(FundMetrics))]
[JsonSerializable(typeof(IReadOnlyList<FundMetrics>))]
[JsonSerializable(typeof(IReadOnlyList<FundPerformance>))]
[JsonSerializable(typeof(IReadOnlyList<VintageSummary>))]
[JsonSerializable(typeof(PmeResult))]
internal sealed partial class MetricsJsonContext : JsonSerializerContext;
