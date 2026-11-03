using System.ComponentModel;

namespace PrivateMarkets.Metrics.Mcp.Contracts;

/// <summary>A fund position as sent by an MCP client.</summary>
/// <param name="Name">Fund name.</param>
/// <param name="CashFlows">Cash flows of the limited partner.</param>
/// <param name="Navs">Reported NAVs.</param>
/// <param name="Commitment">Committed capital.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="VintageYear">Vintage year; defaults to the year of the first contribution.</param>
[Description("A limited partner's position in a private markets fund.")]
public sealed record FundInput(
    [property: Description("Fund name.")] string Name,
    [property: Description("Cash flows. Amounts are positive; the direction comes from the type.")] IReadOnlyList<CashFlowInput> CashFlows,
    [property: Description("Reported net asset values (end of day, after same-day cash flows).")] IReadOnlyList<NavInput>? Navs = null,
    [property: Description("Committed capital, 0 when unknown.")] decimal Commitment = 0m,
    [property: Description("ISO 4217 currency code, e.g. EUR or USD.")] string Currency = "EUR",
    [property: Description("Vintage year; defaults to the year of the first contribution.")] int? VintageYear = null);

/// <summary>A cash flow as sent by an MCP client.</summary>
/// <param name="Date">Value date.</param>
/// <param name="Amount">Positive amount.</param>
/// <param name="Type">Cash flow type name.</param>
public sealed record CashFlowInput(
    [property: Description("Value date, ISO format yyyy-MM-dd.")] DateOnly Date,
    [property: Description("Positive amount in the fund currency.")] decimal Amount,
    [property: Description("One of: Call, Distribution, Fee, RecallableDistribution.")] string Type);

/// <summary>A reported NAV as sent by an MCP client.</summary>
/// <param name="Date">Valuation date.</param>
/// <param name="Value">Net asset value.</param>
public sealed record NavInput(
    [property: Description("Valuation date, ISO format yyyy-MM-dd.")] DateOnly Date,
    [property: Description("Net asset value, zero or positive.")] decimal Value);

/// <summary>A benchmark index level as sent by an MCP client.</summary>
/// <param name="Date">Observation date.</param>
/// <param name="Level">Index level.</param>
public sealed record IndexPointInput(
    [property: Description("Observation date, ISO format yyyy-MM-dd.")] DateOnly Date,
    [property: Description("Total-return index level, strictly positive.")] decimal Level);
