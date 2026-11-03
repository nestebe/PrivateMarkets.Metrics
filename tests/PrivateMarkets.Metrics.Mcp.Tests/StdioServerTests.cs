using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PrivateMarkets.Metrics.Mcp.Tests;

/// <summary>End-to-end tests: starts the real server process and talks MCP over stdio.</summary>
public sealed class StdioServerTests : IAsyncLifetime
{
    private const string FundJson = """
        {
          "name": "Fonds III",
          "commitment": 50000000,
          "currency": "EUR",
          "cashFlows": [
            { "date": "2021-03-15", "amount": 10000000, "type": "Call" },
            { "date": "2022-01-10", "amount": 15000000, "type": "Call" },
            { "date": "2024-06-30", "amount": 8000000, "type": "Distribution" }
          ],
          "navs": [ { "date": "2026-09-30", "value": 31500000 } ]
        }
        """;

    private McpClient? _client;

    private McpClient Client => _client ?? throw new InvalidOperationException("Server not started.");

    public async Task InitializeAsync()
    {
        var serverPath = Path.Combine(AppContext.BaseDirectory, "PrivateMarkets.Metrics.Mcp.dll");
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "private-markets",
            Command = "dotnet",
            Arguments = [serverPath],
        });
        _client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_lists_the_four_tools()
    {
        var tools = await Client.ListToolsAsync();

        Assert.Equal(
            ["compute_fund_metrics", "compute_j_curve", "compute_pme", "vintage_summary"],
            tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
    }

    [Fact]
    public async Task Compute_fund_metrics_returns_the_multiples_and_irr()
    {
        var result = await Client.CallToolAsync(
            "compute_fund_metrics",
            new Dictionary<string, object?> { ["fund"] = Json(FundJson), ["asOf"] = "2026-09-30" });

        var metrics = Payload(result);
        Assert.Equal(1.58m, metrics.GetProperty("tvpi").GetDecimal());
        Assert.Equal(0.32m, metrics.GetProperty("dpi").GetDecimal());
        Assert.Equal(0.105459, (double)metrics.GetProperty("irr").GetDecimal(), 6);
    }

    [Fact]
    public async Task Compute_pme_benchmarks_against_the_index()
    {
        var result = await Client.CallToolAsync(
            "compute_pme",
            new Dictionary<string, object?>
            {
                ["fund"] = Json(FundJson),
                ["index"] = Json("""[{ "date": "2021-01-01", "level": 100 }, { "date": "2024-01-01", "level": 125 }]"""),
                ["indexName"] = "test index",
            });

        var pme = Payload(result);
        Assert.Equal("test index", pme.GetProperty("indexName").GetString());
        Assert.True(pme.GetProperty("kaplanSchoar").GetDecimal() > 1m);
        Assert.Equal(JsonValueKind.Number, pme.GetProperty("directAlpha").ValueKind);
    }

    [Fact]
    public async Task Vintage_summary_and_j_curve_are_exposed()
    {
        var vintages = Payload(await Client.CallToolAsync(
            "vintage_summary",
            new Dictionary<string, object?> { ["funds"] = Json($"[{FundJson}]"), ["asOf"] = "2026-09-30" }));
        var curve = Payload(await Client.CallToolAsync(
            "compute_j_curve",
            new Dictionary<string, object?> { ["fund"] = Json(FundJson), ["frequency"] = "Annual" }));

        Assert.Equal(2021, vintages[0].GetProperty("vintageYear").GetInt32());
        Assert.Equal(6, curve.GetArrayLength());
    }

    [Fact]
    public async Task Invalid_input_returns_an_error_result_with_the_reason()
    {
        var result = await Client.CallToolAsync(
            "compute_fund_metrics",
            new Dictionary<string, object?> { ["fund"] = Json(FundJson.Replace("\"Distribution\"", "\"Dividend\"", StringComparison.Ordinal)) });

        Assert.True(result.IsError);
        Assert.Contains("Unknown cash flow type 'Dividend'", Text(result), StringComparison.Ordinal);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;

    private static JsonElement Payload(CallToolResult result)
    {
        Assert.True(result.IsError is not true, Text(result));
        return Json(Text(result));
    }
}
