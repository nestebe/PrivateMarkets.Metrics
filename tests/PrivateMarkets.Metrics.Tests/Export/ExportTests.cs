using System.Text.Json;
using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Export;
using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics.Tests.Export;

public sealed class ExportTests
{
    private static readonly DateOnly AsOf = new(2024, 12, 31);

    private static readonly Fund Fund = new Fund("Fund, \"A\"", 100m, "USD")
        .Call(new DateOnly(2020, 1, 15), 50m)
        .Distribute(new DateOnly(2023, 1, 15), 20m)
        .Nav(AsOf, 60m);

    [Fact]
    public void J_curve_csv_has_a_header_and_one_row_per_date()
    {
        var csv = CsvExport.ToCsv(Fund.JCurve(ReportingFrequency.Annual));
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("as_of,commitment,paid_in,", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(",irr", lines[0], StringComparison.Ordinal);
        Assert.Equal(6, lines.Length);
        Assert.StartsWith("2024-12-31,100,50,50,0,20,0,60,80,-30,50,1.6,0.4,1.2,0.5,1.6,", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Fund_csv_escapes_names_and_leaves_missing_values_empty()
    {
        var portfolio = new Portfolio("Book", [Fund, new Fund("Empty", 0m, "USD")]);

        var lines = CsvExport.ToCsv(portfolio.FundPerformances(AsOf)).Split("\r\n");

        Assert.StartsWith("\"Fund, \"\"A\"\"\",2020,USD,2024-12-31,", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",,,,,,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Vintage_csv_contains_pooled_and_quartile_columns()
    {
        var csv = CsvExport.ToCsv(new Portfolio("Book", [Fund]).ByVintage(AsOf));

        Assert.Contains("pooled_tvpi", csv, StringComparison.Ordinal);
        Assert.Contains("irr_upper_quartile", csv, StringComparison.Ordinal);
        Assert.Contains("\r\n2020,1,50,20,60,1.6,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_uses_camel_case_and_includes_computed_ratios()
    {
        using var json = JsonDocument.Parse(JsonExport.ToJson(Fund.Metrics(AsOf)));
        var root = json.RootElement;

        Assert.Equal("2024-12-31", root.GetProperty("asOf").GetString());
        Assert.Equal(1.6m, root.GetProperty("tvpi").GetDecimal());
        Assert.Equal(-30m, root.GetProperty("netCashFlow").GetDecimal());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("irr").ValueKind);
    }

    [Fact]
    public void Json_serializes_series_vintages_funds_and_pme()
    {
        var portfolio = new Portfolio("Book", [Fund]);
        var index = new IndexSeries([new IndexPoint(new DateOnly(2020, 1, 1), 100m)], "flat");

        Assert.Equal(JsonValueKind.Array, Parse(JsonExport.ToJson(Fund.JCurve())).ValueKind);
        Assert.Equal(2020, Parse(JsonExport.ToJson(portfolio.ByVintage(AsOf)))[0].GetProperty("vintageYear").GetInt32());
        Assert.Equal("Fund, \"A\"", Parse(JsonExport.ToJson(portfolio.FundPerformances(AsOf)))[0].GetProperty("fundName").GetString());
        Assert.Equal("flat", Parse(JsonExport.ToJson(Fund.Pme(index))).GetProperty("indexName").GetString());
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
