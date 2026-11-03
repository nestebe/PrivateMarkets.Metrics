using PrivateMarkets.Metrics.Pme;

namespace PrivateMarkets.Metrics.Tests.Pme;

public sealed class IndexSeriesTests
{
    private static readonly IndexSeries Series = new(
    [
        new IndexPoint(new DateOnly(2020, 3, 31), 110m),
        new IndexPoint(new DateOnly(2020, 1, 31), 100m),
        new IndexPoint(new DateOnly(2020, 6, 30), 120m),
    ]);

    [Theory]
    [InlineData("2020-01-31", 100)]
    [InlineData("2020-02-15", 100)]
    [InlineData("2020-03-31", 110)]
    [InlineData("2020-06-29", 110)]
    [InlineData("2020-06-30", 120)]
    [InlineData("2030-01-01", 120)]
    public void Level_is_the_last_observation_on_or_before_the_date(string date, int expected)
    {
        Assert.Equal(expected, Series.LevelAt(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Dates_before_the_first_observation_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Series.LevelAt(new DateOnly(2020, 1, 30)));
    }

    [Fact]
    public void Points_are_sorted_and_deduplicated()
    {
        var series = new IndexSeries([new IndexPoint(new DateOnly(2020, 1, 2), 2m), new IndexPoint(new DateOnly(2020, 1, 1), 1m), new IndexPoint(new DateOnly(2020, 1, 2), 3m)]);

        Assert.Equal([1m, 3m], series.Points.Select(p => p.Level));
        Assert.Equal(new DateOnly(2020, 1, 1), series.Start);
        Assert.Equal(new DateOnly(2020, 1, 2), series.End);
    }

    [Fact]
    public void Requires_at_least_one_positive_point()
    {
        Assert.Throws<ArgumentException>(() => new IndexSeries([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexPoint(new DateOnly(2020, 1, 1), 0m));
    }

    [Fact]
    public void Parses_comma_separated_csv_with_header()
    {
        var series = IndexSeries.ParseCsv("date,level\n2020-01-31,100.5\n\n2020-02-28,101.25\n", "MSCI");

        Assert.Equal("MSCI", series.Name);
        Assert.Equal([100.5m, 101.25m], series.Points.Select(p => p.Level));
    }

    [Fact]
    public void Parses_semicolon_csv_with_decimal_commas_and_quotes()
    {
        var series = IndexSeries.ParseCsv("\"Date\";\"Close\"\r\n\"2020-01-31\";\"1234,56\"\r\n2020/02/28;1240\r\n");

        Assert.Equal([1234.56m, 1240m], series.Points.Select(p => p.Level));
    }

    [Theory]
    [InlineData("2020-01-31,100\nnot-a-date,1")]
    [InlineData("2020-01-31,abc")]
    [InlineData("2020-01-31")]
    public void Reports_malformed_lines(string csv)
    {
        Assert.Throws<FormatException>(() => IndexSeries.ParseCsv(csv));
    }

    [Fact]
    public void Loads_from_a_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"index-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "date,level\n2020-01-31,100\n");
        try
        {
            var series = IndexSeries.FromCsv(path);

            Assert.Equal(Path.GetFileNameWithoutExtension(path), series.Name);
            Assert.Single(series.Points);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
