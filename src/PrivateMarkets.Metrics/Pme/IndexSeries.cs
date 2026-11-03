using System.Collections.Immutable;
using System.Globalization;

namespace PrivateMarkets.Metrics.Pme;

/// <summary>A level of a benchmark index at a date.</summary>
public sealed record IndexPoint
{
    /// <summary>Creates an index observation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is not strictly positive.</exception>
    public IndexPoint(DateOnly date, decimal level)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        Date = date;
        Level = level;
    }

    /// <summary>Observation date.</summary>
    public DateOnly Date { get; }

    /// <summary>Index level (total-return levels should be used for PME).</summary>
    public decimal Level { get; }
}

/// <summary>
/// Time series of a benchmark index. Lookups are "as of": the level at a date is the last observation
/// on or before that date, so the series does not need a point on every cash flow date.
/// </summary>
public sealed class IndexSeries
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy-MM-ddTHH:mm:ss"];
    private static readonly char[] Separators = [';', '\t', ','];

    private readonly ImmutableArray<IndexPoint> _points;

    /// <summary>Creates a series from observations; when a date appears twice the last level wins.</summary>
    /// <param name="points">Observations, in any order.</param>
    /// <param name="name">Optional display name of the index.</param>
    /// <exception cref="ArgumentException">No observation was supplied.</exception>
    public IndexSeries(IEnumerable<IndexPoint> points, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.GroupBy(p => p.Date).Select(g => g.Last()).OrderBy(p => p.Date).ToImmutableArray();
        if (_points.IsEmpty)
        {
            throw new ArgumentException("An index series needs at least one observation.", nameof(points));
        }

        Name = name;
    }

    /// <summary>Optional display name of the index.</summary>
    public string? Name { get; }

    /// <summary>Observations sorted by date.</summary>
    public IReadOnlyList<IndexPoint> Points => _points;

    /// <summary>First observation date.</summary>
    public DateOnly Start => _points[0].Date;

    /// <summary>Last observation date.</summary>
    public DateOnly End => _points[^1].Date;

    /// <summary>Level of the index as of <paramref name="date"/> (last observation on or before it).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="date"/> is before the first observation.</exception>
    public decimal LevelAt(DateOnly date)
    {
        var low = 0;
        var high = _points.Length - 1;
        if (date < _points[0].Date)
        {
            throw new ArgumentOutOfRangeException(
                nameof(date), date, $"The index '{Name}' starts on {Start:yyyy-MM-dd}; no level is available on {date:yyyy-MM-dd}.");
        }

        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_points[middle].Date <= date)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return _points[low].Level;
    }

    /// <summary>Loads a series from a CSV file (see <see cref="Parse(TextReader, string?)"/> for the format).</summary>
    public static IndexSeries FromCsv(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var reader = new StreamReader(path);
        return Parse(reader, name ?? Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>
    /// Parses CSV content whose first two columns are an ISO date (<c>yyyy-MM-dd</c>) and a level.
    /// The separator (<c>,</c>, <c>;</c> or tab) is detected, an optional header line is skipped and,
    /// with <c>;</c> or tab separators, a comma is accepted as decimal separator.
    /// </summary>
    /// <exception cref="FormatException">A line cannot be parsed.</exception>
    public static IndexSeries Parse(TextReader reader, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var points = new List<IndexPoint>();
        var lineNumber = 0;
        char? separator = null;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            separator ??= DetectSeparator(line);
            var fields = line.Split(separator.Value);
            if (fields.Length < 2)
            {
                throw new FormatException($"Line {lineNumber}: expected '<date>{separator}<level>' but got '{line}'.");
            }

            if (!TryParseDate(fields[0], out var date))
            {
                if (points.Count == 0 && lineNumber == 1)
                {
                    continue; // header
                }

                throw new FormatException($"Line {lineNumber}: '{fields[0]}' is not an ISO date (yyyy-MM-dd).");
            }

            if (!TryParseLevel(fields[1], separator.Value, out var level))
            {
                throw new FormatException($"Line {lineNumber}: '{fields[1]}' is not a number.");
            }

            points.Add(new IndexPoint(date, level));
        }

        return new IndexSeries(points, name);
    }

    /// <summary>Parses CSV text (see <see cref="Parse(TextReader, string?)"/>).</summary>
    public static IndexSeries ParseCsv(string content, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var reader = new StringReader(content);
        return Parse(reader, name);
    }

    private static char DetectSeparator(string line) =>
        Separators.FirstOrDefault(line.Contains, ',');

    private static bool TryParseDate(string field, out DateOnly date) =>
        DateOnly.TryParseExact(Unquote(field), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseLevel(string field, char separator, out decimal level)
    {
        var text = Unquote(field);
        if (separator != ',')
        {
            text = text.Replace(',', '.');
        }

        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out level);
    }

    private static string Unquote(string field) => field.Trim().Trim('"').Trim();
}
