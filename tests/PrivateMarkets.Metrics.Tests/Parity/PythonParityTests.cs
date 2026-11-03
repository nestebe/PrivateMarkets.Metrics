using PrivateMarkets.Metrics.Aggregation;
using PrivateMarkets.Metrics.Returns;

namespace PrivateMarkets.Metrics.Tests.Parity;

/// <summary>
/// Compares the library with the Python reference implementations (xirr, privateassets, pypme)
/// on the datasets of <c>tests/parity</c>, to 1e-6.
/// </summary>
public sealed class PythonParityTests
{
    private static readonly XirrDataset XirrData = ParityData.Load<XirrDataset>("xirr.json");
    private static readonly PmeDataset Pme = ParityData.Load<PmeDataset>("pme.json");
    private static readonly ModifiedPmeDataset ModifiedPme = ParityData.Load<ModifiedPmeDataset>("mpme.json");
    private static readonly VintageDataset Vintage = ParityData.Load<VintageDataset>("vintage.json");

    public static TheoryData<string, string> XirrCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var testCase in XirrData.Cases)
        {
            foreach (var dayCount in testCase.Expected.Keys)
            {
                data.Add(testCase.Name, dayCount);
            }
        }

        return data;
    }

    public static TheoryData<string> PmeCases() => [.. Pme.Cases.Select(c => c.Name)];

    public static TheoryData<string> ModifiedPmeCases() => [.. ModifiedPme.Cases.Select(c => c.Name)];

    [Theory]
    [MemberData(nameof(XirrCases))]
    public void Xirr_matches_python(string name, string dayCount)
    {
        var testCase = XirrData.Cases.Single(c => c.Name == name);
        var flows = testCase.Flows.Select(f => new DatedAmount(ParityData.Date(f.Date), f.Amount));

        var irr = Xirr.Calculate(flows, ParityData.DayCount(dayCount));

        Precision.Close(testCase.Expected[dayCount], irr, label: $"{name} {dayCount}");
    }

    [Theory]
    [MemberData(nameof(PmeCases))]
    public void Pme_matches_privateassets_and_reference(string name)
    {
        var testCase = Pme.Cases.Single(c => c.Name == name);
        var fund = ParityData.ToFund(name, testCase.Flows, [testCase.Nav]);
        var options = new MetricsOptions { DayCount = ParityData.DayCount(Pme.DayCount) };
        var expected = testCase.Expected;

        var pme = fund.Pme(ParityData.ToIndex(testCase.Index), ParityData.Date(testCase.Nav.Date), options);

        Precision.Close(expected.KaplanSchoar, pme.KaplanSchoar, label: "KS-PME");
        Precision.Close(expected.DirectAlphaReference, pme.DirectAlpha, label: "Direct Alpha (reference)");
        if (expected.DirectAlpha is not null)
        {
            Precision.Close(expected.DirectAlpha, pme.DirectAlpha, label: "Direct Alpha (privateassets)");
        }
        Precision.Close(expected.FundIrr, pme.FundIrr, label: "fund IRR");
        Precision.Close(expected.LongNickelsRatio, pme.LongNickelsRatio, label: "LN ratio");
        Assert.Equal(expected.LongNickelsShadowNegative, pme.LongNickelsShadowNegative);
        Precision.Relative(expected.LongNickelsTerminalValue, pme.LongNickelsTerminalValue, label: "LN terminal value");
        if (expected.LongNickelsShadowNav is { } shadowNav)
        {
            Precision.Relative(shadowNav, pme.LongNickelsTerminalValue, label: "LN shadow NAV (privateassets)");
        }

        if (!expected.LongNickelsShadowNegative)
        {
            Precision.Close(expected.LongNickelsIrr, pme.LongNickelsIrr, label: "LN IRR");
        }

        Precision.Close(expected.PmePlusScalingFactor, pme.PmePlusScalingFactor, label: "PME+ lambda");
        Precision.Close(expected.PmePlusIrr, pme.PmePlusIrr, label: "PME+ IRR");
    }

    [Theory]
    [MemberData(nameof(ModifiedPmeCases))]
    public void Modified_pme_matches_pypme(string name)
    {
        var testCase = ModifiedPme.Cases.Single(c => c.Name == name);
        var fund = ParityData.ToFund(name, testCase.Flows, testCase.Navs);
        var options = new MetricsOptions { DayCount = ParityData.DayCount(ModifiedPme.DayCount) };

        var pme = fund.Pme(ParityData.ToIndex(testCase.Index), ParityData.Date(testCase.AsOf), options);

        Precision.Close(testCase.Expected.ModifiedPmeIrr, pme.ModifiedPmeIrr, label: "mPME IRR");
        Precision.Close(testCase.Expected.FundIrr, pme.FundIrr, label: "fund IRR");
    }

    [Fact]
    public void Fund_metrics_match_privateassets_vintage_statistics()
    {
        var asOf = ParityData.Date(Vintage.AsOf);
        var options = new MetricsOptions { DayCount = ParityData.DayCount(Vintage.DayCount) };

        foreach (var fundData in Vintage.Funds)
        {
            var metrics = ParityData.ToFund(fundData.Name, fundData.Flows, fundData.Navs).Metrics(asOf, options);

            Precision.Close(fundData.Expected.Dpi, metrics.Dpi, label: $"{fundData.Name} DPI");
            Precision.Close(fundData.Expected.Rvpi, metrics.Rvpi, label: $"{fundData.Name} RVPI");
            Precision.Close(fundData.Expected.Tvpi, metrics.Tvpi, label: $"{fundData.Name} TVPI");
            Precision.Close(fundData.Expected.Irr, metrics.Irr, label: $"{fundData.Name} IRR");
        }
    }

    [Fact]
    public void Vintage_aggregation_matches_numpy_and_pooled_reference()
    {
        var asOf = ParityData.Date(Vintage.AsOf);
        var options = new MetricsOptions { DayCount = ParityData.DayCount(Vintage.DayCount) };
        var portfolio = new Portfolio(
            "parity",
            Vintage.Funds.Select(f => ParityData.ToFund(f.Name, f.Flows, f.Navs, f.VintageYear)));

        var summaries = portfolio.ByVintage(asOf, options);

        Assert.Equal(Vintage.Vintages.Select(v => v.VintageYear), summaries.Select(s => s.VintageYear));
        foreach (var (expected, actual) in Vintage.Vintages.Zip(summaries))
        {
            var label = expected.VintageYear.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Precision.Close(expected.PooledTvpi, actual.Pooled.Tvpi, label: $"{label} pooled TVPI");
            Precision.Close(expected.PooledDpi, actual.Pooled.Dpi, label: $"{label} pooled DPI");
            Precision.Close(expected.PooledIrr, actual.Pooled.Irr, label: $"{label} pooled IRR");
            AssertQuartiles(expected.Tvpi, actual.Tvpi, $"{label} TVPI");
            AssertQuartiles(expected.Irr, actual.Irr, $"{label} IRR");
        }
    }

    private static void AssertQuartiles(ExpectedQuartiles expected, QuartileStatistics? actual, string label)
    {
        Assert.NotNull(actual);
        Precision.Close(expected.LowerQuartile, actual.LowerQuartile, label: $"{label} Q1");
        Precision.Close(expected.Median, actual.Median, label: $"{label} median");
        Precision.Close(expected.UpperQuartile, actual.UpperQuartile, label: $"{label} Q3");
        Precision.Close(expected.Mean, actual.Mean, label: $"{label} mean");
    }

    private sealed record XirrDataset(IReadOnlyList<XirrCase> Cases);

    private sealed record XirrCase(string Name, IReadOnlyList<ParityFlow> Flows, IReadOnlyDictionary<string, double?> Expected);

    private sealed record PmeDataset(string DayCount, IReadOnlyList<PmeCase> Cases);

    private sealed record PmeCase(
        string Name, IReadOnlyList<ParityFlow> Flows, ParityNav Nav, IReadOnlyList<ParityIndexPoint> Index, PmeExpected Expected);

    private sealed record PmeExpected(
        double KaplanSchoar,
        double? DirectAlpha,
        double? DirectAlphaReference,
        double? LongNickelsRatio,
        double? LongNickelsShadowNav,
        bool LongNickelsShadowNegative,
        double? FundIrr,
        double? LongNickelsIrr,
        double LongNickelsTerminalValue,
        double? PmePlusScalingFactor,
        double? PmePlusIrr);

    private sealed record ModifiedPmeDataset(string DayCount, IReadOnlyList<ModifiedPmeCase> Cases);

    private sealed record ModifiedPmeCase(
        string Name,
        IReadOnlyList<ParityFlow> Flows,
        IReadOnlyList<ParityNav> Navs,
        IReadOnlyList<ParityIndexPoint> Index,
        string AsOf,
        ModifiedPmeExpected Expected);

    private sealed record ModifiedPmeExpected(double ModifiedPmeIrr, double FundIrr);

    private sealed record VintageDataset(string DayCount, string AsOf, IReadOnlyList<VintageFund> Funds, IReadOnlyList<VintageExpected> Vintages);

    private sealed record VintageFund(
        string Name, int VintageYear, IReadOnlyList<ParityFlow> Flows, IReadOnlyList<ParityNav> Navs, FundExpected Expected);

    private sealed record FundExpected(double Dpi, double Rvpi, double Tvpi, double Irr);

    private sealed record VintageExpected(
        int VintageYear, double PooledTvpi, double PooledDpi, double PooledIrr, ExpectedQuartiles Tvpi, ExpectedQuartiles Irr);

    private sealed record ExpectedQuartiles(double LowerQuartile, double Median, double UpperQuartile, double Mean);
}
