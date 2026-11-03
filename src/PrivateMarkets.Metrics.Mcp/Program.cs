using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrivateMarkets.Metrics.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol: every log line must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FundMetricsTools>();

await builder.Build().RunAsync();
